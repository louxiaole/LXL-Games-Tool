using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using LXLGameToolbox.Core;

namespace LXLGameToolbox.UI;

public enum DownloadState
{
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>一个在后台进行的下载 / 更新任务（与具体页面无关，切换页面后仍然继续）。</summary>
public sealed class DownloadTask : INotifyPropertyChanged
{
    internal readonly CancellationTokenSource Cts = new();

    public DownloadTask(ToolItem item, string version, string stage)
    {
        Item = item;
        ToolId = item.Tool.Id;
        Name = item.Name;
        Version = version;
        StageText = stage;
    }

    public ToolItem Item { get; }
    public string ToolId { get; }
    public string Name { get; }
    public string Version { get; }

    /// <summary>下载到的官方安装包路径（第三方软件下载完成后有值）。</summary>
    public string InstallerPath { get; internal set; } = "";

    public DownloadState State { get; private set; } = DownloadState.Running;
    public double Percent { get; private set; }
    public string StageText { get; private set; }
    public string DetailText { get; private set; } = "正在连接下载源 ...";
    public string Message { get; private set; } = "";

    public bool IsRunning => State == DownloadState.Running;
    public bool IsFinished => !IsRunning;

    public Visibility RunningVisibility => IsRunning ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CancelVisibility => IsRunning ? Visibility.Visible : Visibility.Collapsed;
    public Visibility InstallerVisibility =>
        State == DownloadState.Completed && !string.IsNullOrEmpty(InstallerPath)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string PercentText => IsRunning ? $"{Percent:F0}%" : "";

    public string StateText => State switch
    {
        DownloadState.Running => "下载中",
        DownloadState.Completed => "已完成",
        DownloadState.Failed => "失败",
        DownloadState.Cancelled => "已取消",
        _ => ""
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void SetProgress(double percent, string stage, string detail)
    {
        Percent = Math.Clamp(percent, 0, 100);
        StageText = stage;
        DetailText = detail;
        Raise(nameof(Percent), nameof(StageText), nameof(DetailText), nameof(PercentText));
    }

    internal void SetStage(string stage, string detail)
    {
        StageText = stage;
        DetailText = detail;
        Raise(nameof(StageText), nameof(DetailText));
    }

    internal void Complete(string message)
    {
        State = DownloadState.Completed;
        Percent = 100;
        Message = message;
        StageText = "已完成";
        DetailText = message;
        RaiseAll();
    }

    internal void Fail(string message)
    {
        State = DownloadState.Failed;
        Message = message;
        DetailText = message;
        RaiseAll();
    }

    internal void MarkCancelled()
    {
        State = DownloadState.Cancelled;
        StageText = "已取消";
        DetailText = "下载已取消";
        RaiseAll();
    }

    private void RaiseAll() =>
        Raise(nameof(State), nameof(IsRunning), nameof(IsFinished), nameof(RunningVisibility),
            nameof(CancelVisibility), nameof(InstallerVisibility), nameof(Percent),
            nameof(StageText), nameof(DetailText), nameof(Message), nameof(StateText), nameof(PercentText));

    private void Raise(params string[] names)
    {
        foreach (var name in names)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>全局下载中心：所有下载 / 更新都在这里跑，界面只订阅进度。</summary>
public static class DownloadCenter
{
    public static ObservableCollection<DownloadTask> Tasks { get; } = new();

    public static DownloadTask? Find(string toolId) =>
        Tasks.FirstOrDefault(task => task.ToolId == toolId && task.IsRunning);

    /// <summary>第三方软件：后台下载官方安装包，下载完成后自动启动安装程序。</summary>
    public static DownloadTask? StartInstaller(ToolItem item)
    {
        if (Find(item.Tool.Id) != null) return null;

        var task = new DownloadTask(item, "官方安装包", "正在连接官方下载源 ...");
        Tasks.Insert(0, task);
        item.SetBusy(true, "正在连接官方下载源 ...");
        _ = RunInstallerAsync(task);
        return task;
    }

    /// <summary>自制工具：后台下载 zip 并解压安装到工具箱目录。</summary>
    public static DownloadTask? StartInstall(ToolItem item, ReleaseInfo release)
    {
        if (Find(item.Tool.Id) != null) return null;

        var task = new DownloadTask(item, release.DisplayVersion, $"正在下载 {release.DisplayVersion} ...");
        Tasks.Insert(0, task);
        item.SetBusy(true, $"正在下载 {release.DisplayVersion} ...");
        _ = RunInstallAsync(task, release);
        return task;
    }

    public static void Cancel(DownloadTask task)
    {
        if (!task.IsRunning) return;
        try { task.Cts.Cancel(); }
        catch (Exception ex) { Logger.Info($"取消下载失败：{ex.Message}"); }
    }

    public static void RemoveFinished()
    {
        var finished = Tasks.Where(task => !task.IsRunning).ToList();
        foreach (var task in finished) Tasks.Remove(task);
    }

    private static async Task RunInstallerAsync(DownloadTask task)
    {
        var item = task.Item;
        var progress = new Progress<DownloadInfo>(info =>
        {
            var detail = info.Total > 0
                ? $"已下载 {VersionUtil.FormatSize(info.Received)} / {VersionUtil.FormatSize(info.Total)} · {info.SpeedText} · 剩余 {info.EtaText}"
                : $"已下载 {VersionUtil.FormatSize(info.Received)} · {info.SpeedText}";
            task.SetProgress(info.Percent, $"正在下载 {item.Name} 官方安装包", detail);
            item.SetProgress(info.Percent, $"下载 {item.Name} {info.Percent:F0}% · {info.SpeedText} · 剩余 {info.EtaText}");
        });

        try
        {
            var installer = await Installer.EnsureInstallerAsync(item.Tool, progress, task.Cts.Token);
            task.InstallerPath = installer;
            task.SetStage("正在启动安装程序", installer);
            item.SetBusy(false);
            item.SetBusy(true, "正在启动安装程序 ...");

            var process = Installer.RunInstaller(installer);
            WatchInstaller(item, process);
            task.Complete($"安装程序已启动：{System.IO.Path.GetFileName(installer)}");
            AppState.SetStatus($"{item.Name} 安装程序已启动，安装完成后会自动刷新");

            item.Refresh();
            AppState.SetStatus($"{item.Name} 安装包下载完成");
        }
        catch (OperationCanceledException)
        {
            task.MarkCancelled();
            AppState.SetStatus($"{item.Name} 下载已取消");
        }
        catch (Exception ex)
        {
            Logger.Error($"{item.Name} 安装包下载失败：{ex.Message}");
            task.Fail(ex.Message);
            AppState.SetStatus($"{item.Name} 下载失败：{ex.Message}");
        }
        finally
        {
            if (task.IsRunning) task.MarkCancelled();
            item.SetBusy(false);
        }
    }

    private static async Task RunInstallAsync(DownloadTask task, ReleaseInfo release)
    {
        var item = task.Item;
        var progress = new Progress<DownloadInfo>(info =>
        {
            var detail = info.Total > 0
                ? $"已下载 {VersionUtil.FormatSize(info.Received)} / {VersionUtil.FormatSize(info.Total)} · {info.SpeedText} · 剩余 {info.EtaText}"
                : $"已下载 {VersionUtil.FormatSize(info.Received)} · {info.SpeedText}";
            task.SetProgress(info.Percent, $"正在下载 {item.Name} {release.DisplayVersion}", detail);
            item.SetProgress(info.Percent, $"下载 {item.Name} {release.DisplayVersion} · {info.Percent:F0}% · {info.SpeedText}");
        });

        try
        {
            task.SetStage("正在下载安装包", release.AssetName);
            await Installer.InstallAsync(item.Tool, release, progress, task.Cts.Token);

            task.SetStage("正在解压安装包", "解压完成后即可启动");
            item.SetProgress(100, "正在解压安装包 ...");

            item.Refresh();
            task.Complete($"{item.Name} {release.DisplayVersion} 安装完成");
            AppState.SetStatus($"{item.Name} {release.DisplayVersion} 安装完成");
        }
        catch (OperationCanceledException)
        {
            task.MarkCancelled();
            AppState.SetStatus($"{item.Name} 更新已取消");
        }
        catch (Exception ex)
        {
            Logger.Error($"{item.Name} 更新失败：{ex.Message}");
            task.Fail(ex.Message);
            AppState.SetStatus($"{item.Name} 更新失败：{ex.Message}");
        }
        finally
        {
            if (task.IsRunning) task.MarkCancelled();
            item.SetBusy(false);
        }
    }

    private static void WatchInstaller(ToolItem item, System.Diagnostics.Process? process)
    {
        if (process == null) return;
        try
        {
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                LocalScanner.Invalidate();
                item.Refresh();
            }));
        }
        catch (Exception ex)
        {
            Logger.Info($"监听安装程序退出失败：{ex.Message}");
        }
    }
}
