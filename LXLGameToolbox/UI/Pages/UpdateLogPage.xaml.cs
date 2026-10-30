using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using LXLGameToolbox.Core;

namespace LXLGameToolbox.UI.Pages;

/// <summary>左侧导航「更新日志」：查看工具箱自身的历史版本并在线更新。</summary>
public partial class UpdateLogPage : UserControl
{
    private readonly ObservableCollection<ReleaseInfo> _releases = new();
    private CancellationTokenSource? _cts;

    public UpdateLogPage()
    {
        InitializeComponent();
        CurrentVersionText.Text = "v" + AppState.VersionText;
        ReleaseList.ItemsSource = _releases;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync(bool forceRefresh = false)
    {
        if (forceRefresh)
        {
            StatusText.Text = "正在获取线上版本 ...";
            CheckButton.IsEnabled = false;
        }

        try
        {
            var releases = await UpdateService.GetReleasesAsync(20, forceRefresh);
            _releases.Clear();
            foreach (var release in releases) _releases.Add(release);

            var latest = releases.FirstOrDefault();
            var newer = latest != null && UpdateService.IsNewer(latest);

            if (releases.Count == 0)
            {
                StatusText.Text = "暂无线上版本记录：在 GitHub 发布 Release 后，这里会自动同步。";
                EmptyText.Text = "该仓库还没有发布任何 Release。";
                EmptyText.Visibility = Visibility.Visible;
                UpdateButton.Visibility = Visibility.Collapsed;
            }
            else if (newer)
            {
                StatusText.Text = $"发现新版本 {latest!.DisplayVersion}（当前 {AppState.VersionText}），可点击右侧立即更新。";
                UpdateButton.Visibility = Visibility.Visible;
            }
            else
            {
                StatusText.Text = $"已是最新版本（{latest!.DisplayVersion}）。";
                UpdateButton.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("读取更新日志失败：" + ex.Message);
            StatusText.Text = "获取更新日志失败，请检查网络后重试。";
            EmptyText.Text = ex.Message;
            EmptyText.Visibility = Visibility.Visible;
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }

    private async void Check_Click(object sender, RoutedEventArgs e)
    {
        GitHubClient.ClearCache();
        await LoadAsync(true);
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        var latest = _releases.FirstOrDefault();
        if (latest == null || !UpdateService.IsNewer(latest))
        {
            await LoadAsync(true);
            return;
        }

        if (!latest.HasAsset)
        {
            MessageBox.Show(Window.GetWindow(this),
                $"版本 {latest.DisplayVersion} 没有提供 zip 更新包，请在 Releases 页面手动下载。",
                "无法自动更新", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(Window.GetWindow(this),
            $"将下载并更新到 {latest.DisplayVersion}，更新过程中工具箱会关闭并自动重启。\n\n是否继续？",
            "更新工具箱", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        _cts = new CancellationTokenSource();
        ProgressPanel.Visibility = Visibility.Visible;
        DownloadBar.Value = 0;
        ProgressTitle.Text = $"正在下载 {latest.DisplayVersion} ...";
        ProgressInfo.Text = "正在连接下载源 ...";
        UpdateButton.IsEnabled = false;
        CheckButton.IsEnabled = false;

        var progress = new Progress<DownloadInfo>(info =>
        {
            DownloadBar.Value = info.Percent;
            ProgressTitle.Text = $"正在下载更新包（{info.Percent:F0}%）";
            ProgressInfo.Text = $"已下载 {VersionUtil.FormatSize(info.Received)}" +
                                (info.Total > 0 ? $" / {VersionUtil.FormatSize(info.Total)}" : "") +
                                $" · {info.SpeedText}" +
                                (info.Total > 0 ? $" · 剩余 {info.EtaText}" : "");
        });

        try
        {
            await UpdateService.ApplyAsync(latest, progress, _cts.Token);
            DownloadBar.Value = 100;
            ProgressTitle.Text = "更新准备完成，工具箱即将重启";
            ProgressInfo.Text = "正在退出并替换文件 ...";
            App.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Logger.Error("工具箱更新失败：" + ex.Message);
            MessageBox.Show(Window.GetWindow(this), "更新失败：" + ex.Message, "更新日志",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            ProgressPanel.Visibility = Visibility.Collapsed;
            UpdateButton.IsEnabled = true;
            CheckButton.IsEnabled = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        ProgressPanel.Visibility = Visibility.Collapsed;
        UpdateButton.IsEnabled = true;
        CheckButton.IsEnabled = true;
        AppState.SetStatus("已取消更新");
    }
}
