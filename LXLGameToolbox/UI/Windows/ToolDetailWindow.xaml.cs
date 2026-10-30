using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LXLGameToolbox.Core;

namespace LXLGameToolbox.UI.Windows;

public partial class ToolDetailWindow : Window
{
    private readonly ToolItem _item;
    private DownloadTask? _task;

    public ToolDetailWindow(ToolItem item)
    {
        InitializeComponent();
        _item = item;

        Title = $"{item.Name} - 工具详情";
        TitleText.Text = $"{item.Name} · 工具详情";
        GlyphText.Text = item.Glyph;
        GlyphText.Visibility = item.GlyphVisibility;
        IconImage.Source = item.IconImage;
        IconImage.Visibility = item.IconVisibility;
        IconBorder.Background = item.IconBackground;
        NameText.Text = item.Name;
        CategoryText.Text = item.Category;
        ZoneText.Text = item.ZoneName;
        ZoneText.Foreground = item.ZoneBrush;
        ZoneBadge.Background = new SolidColorBrush(Color.FromArgb(0x1A,
            ((SolidColorBrush)item.ZoneBrush).Color.R,
            ((SolidColorBrush)item.ZoneBrush).Color.G,
            ((SolidColorBrush)item.ZoneBrush).Color.B));
        DescriptionText.Text = item.Description;
        RepoText.Text = item.Tool.HasGitHub ? item.Tool.RepoUrl : item.Tool.HomepageUrl;

        Loaded += async (_, _) => await LoadInfoAsync();
        Closed += (_, _) =>
        {
            // 关闭详情窗口不取消后台下载，任务会继续并在「下载中心」显示
            UnbindTask();
            _item.PropertyChanged -= OnItemChanged;
        };
        _item.PropertyChanged += OnItemChanged;
        UpdateUi();
    }

    private void OnItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => UpdateUi();

    // ---------- 版本信息 ----------

    private async Task LoadInfoAsync()
    {
        if (_item.IsThirdParty)
        {
            var isLan = _item.Tool.ZoneId == ZoneRegistry.Lan;
            ChangelogText.Text = isLan
                ? "本专区收录联机用的第三方工具，安装包与版本更新由官方提供。樱花内网穿透的安装包需要登录官网后下载，" +
                  "装好后回到工具箱点击「扫描本地」即可自动接管并一键启动。\n\n" +
                  "使用提示：开启隧道前请确认端口与本地服务已启动，联机时只把端口分享给信任的好友。"
                : _item.Tool.CanDirectInstall
                    ? "本专区收录第三方软件，安装包与版本更新由官方提供。工具箱内可直接下载官方安装包并启动安装向导，" +
                      "安装完成后点击「扫描本地」即可自动接管本机已装版本。\n\n" +
                      "使用提示：修改器请在单机 / 离线模式下使用，避免在联机或反作弊环境开启。"
                    : "本专区收录第三方软件，安装包与版本更新由官方提供，工具箱负责快速跳转与本机已装版本的启动管理。\n\n" +
                      "使用提示：修改器请在单机 / 离线模式下使用，避免在联机或反作弊环境开启。";
            UpdateUi();
            return;
        }

        // 自制但未接入自动更新的工具（如小乐商店百科）：只做本地检测与启动
        if (!_item.Tool.HasGitHub && _item.Tool.IsSelf)
        {
            ChangelogText.Text = "该工具为小乐自制，尚未接入在线自动更新，工具箱负责检测本机安装情况并一键启动。\n\n" +
                                 "如果工具箱没有自动检测到程序，可点击「选择本地程序」手动指定主程序路径，之后会一直记住。";
            UpdateUi();
            return;
        }

        if (_item.Release != null)
        {
            UpdateUi();
            return;
        }

        ActionButton.IsEnabled = false;
        AppState.SetStatus($"正在获取 {_item.Name} 最新版本 ...");
        var release = await GitHubClient.GetLatestAsync(_item.Tool, true);
        _item.SetRelease(release);
        _item.Refresh();
        UpdateUi();
        AppState.SetStatus(release == null
            ? $"{_item.Name} 版本信息获取失败"
            : $"{_item.Name} 最新版本 {release.Tag}");
    }

    private void UpdateUi()
    {
        var release = _item.Release;
        var local = _item.Local;

        SourceText.Text = local == null
            ? _item.Tool.Vendor
            : $"{local.Source} · {_item.Tool.Vendor}";
        LocalVersionText.Text = local == null
            ? "未安装"
            : string.IsNullOrEmpty(local.Version) ? "未知版本" : local.Version;
        PathText.Text = local?.ExePath ?? "—";
        FolderButton.Visibility = local == null ? Visibility.Collapsed : Visibility.Visible;
        UninstallButton.Visibility = local == null ? Visibility.Collapsed : Visibility.Visible;
        MigrateButton.Visibility = _item.MigrateVisibility;
        ChooseButton.IsEnabled = !_item.IsBusy;
        RepoButton.IsEnabled = !_item.IsBusy;

        if (!_item.Tool.HasGitHub && _item.Tool.IsSelf)
        {
            LatestVersionText.Text = local == null ? "未检测到本机安装" : "以本地程序为准";
            SizeText.Text = "—";
            DateText.Text = "—";
            ChangelogTitle.Text = "说明与提示";
            RepoButton.Visibility = Visibility.Collapsed;
            ReinstallButton.Visibility = Visibility.Collapsed;
            UpdateButton.Visibility = Visibility.Collapsed;

            ActionButton.Content = local == null ? "指定程序" : "启动";
            ActionButton.IsEnabled = !_item.IsBusy;
            ActionButton.ToolTip = local == null ? "选择本机已安装的主程序" : null;

            if (_item.IsBusy)
            {
                ActionButton.Content = "处理中 ...";
                ActionButton.IsEnabled = false;
            }
            return;
        }

        if (_item.IsThirdParty)
        {
            var installerVersion = Installer.ReadInstallerVersion(_item.Tool);
            var official = VersionUtil.IsNewer(installerVersion, _item.OnlineVersion)
                ? installerVersion
                : _item.OnlineVersion;
            LatestVersionText.Text = string.IsNullOrEmpty(official)
                ? "以官网为准（尚未获取到线上版本）"
                : $"{official}（官方最新版本）";
            SizeText.Text = _item.Tool.CanDirectInstall
                ? "官方安装包直链"
                : _item.Tool.ZoneId == ZoneRegistry.Lan ? "官网下载（需登录）" : "官网下载";
            DateText.Text = "—";
            ChangelogTitle.Text = "说明与提示";
            RepoButton.Content = "打开官网";
            ReinstallButton.Visibility = Visibility.Collapsed;

            if (_item.HasUpdate)
                ChangelogText.Text = $"检测到本机安装的是旧版本 {local?.Version}，官方安装包版本为 {official}，" +
                                     "点击下方按钮会重新下载最新官方安装包并启动安装程序。\n\n" +
                                     "使用提示：修改器请在单机 / 离线模式下使用，避免在联机或反作弊环境开启。";

            ActionButton.Content = local == null
                ? (_item.Tool.CanDirectInstall ? "下载并安装" : "前往官网下载")
                : "启动";
            ActionButton.IsEnabled = !_item.IsBusy;
            ActionButton.ToolTip = local == null
                ? _item.Tool.CanDirectInstall
                    ? "在工具箱内直接下载官方安装包并启动安装程序"
                    : "在浏览器中打开官方下载页面"
                : null;

            var canUpdate = local != null && _item.HasUpdate && _item.Tool.CanDirectInstall;
            UpdateButton.Visibility = canUpdate ? Visibility.Visible : Visibility.Collapsed;
            UpdateButton.Content = $"更新到 {official}";
            UpdateButton.IsEnabled = canUpdate && !_item.IsBusy;

            if (_item.IsBusy)
            {
                ActionButton.Content = "处理中 ...";
                ActionButton.IsEnabled = false;
                UpdateButton.Visibility = Visibility.Collapsed;
            }
            return;
        }

        ChangelogTitle.Text = "更新说明";
        RepoButton.Content = "Release 页面";
        LatestVersionText.Text = release == null ? "获取失败" : $"{release.DisplayVersion}（{release.Tag}）";
        SizeText.Text = release == null ? "—" : VersionUtil.FormatSize(release.AssetSize);
        DateText.Text = release == null ? "—" : release.PublishedAt;
        ChangelogText.Text = release == null
            ? "暂时无法获取最新版本信息，请检查网络后重新打开详情页。"
            : string.IsNullOrWhiteSpace(release.Body)
                ? "该版本没有填写更新说明。"
                : release.Body.Trim();

        ReinstallButton.Visibility = local != null && !_item.HasUpdate && !_item.IsBusy &&
                                     release?.HasAsset == true
            ? Visibility.Visible
            : Visibility.Collapsed;
        ReinstallButton.IsEnabled = release?.HasAsset == true;

        var hasUpdateButton = local != null && _item.HasUpdate && !_item.IsBusy && release?.HasAsset == true;
        UpdateButton.Visibility = hasUpdateButton ? Visibility.Visible : Visibility.Collapsed;
        UpdateButton.Content = $"更新到 {release?.DisplayVersion}";
        UpdateButton.IsEnabled = hasUpdateButton;

        if (_item.IsBusy)
        {
            ActionButton.Content = "处理中 ...";
            ActionButton.IsEnabled = false;
        }
        else if (local == null)
        {
            ActionButton.Content = "下载并安装";
            ActionButton.IsEnabled = release?.HasAsset == true;
            ActionButton.ToolTip = release == null
                ? "暂时无法获取最新版本信息"
                : release.HasAsset ? null : "该版本没有提供 zip 安装包";
        }
        else
        {
            // 已安装时主按钮固定为「启动」，有新版本则用单独的更新按钮
            ActionButton.Content = "启动";
            ActionButton.IsEnabled = true;
            ActionButton.ToolTip = null;
        }
    }

    // ---------- 操作 ----------

    private void Update_Click(object sender, RoutedEventArgs e)
    {
        if (_item.IsBusy) return;

        if (_item.IsThirdParty)
        {
            if (!_item.Tool.CanDirectInstall)
            {
                OpenUrl(_item.Tool.SiteUrl, "官网下载页");
                return;
            }

            DownloadInstallerAsync();
            return;
        }

        var release = _item.Release;
        if (release == null || !release.HasAsset)
        {
            MessageBox.Show(this, "暂时无法获取最新版本信息，请稍后重试。", _item.Name,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        InstallAsync(release);
    }

    private void Action_Click(object sender, RoutedEventArgs e)
    {
        if (!_item.Tool.HasGitHub && _item.Tool.IsSelf)
        {
            if (_item.Local == null) Choose_Click(sender, e);
            else Launch();
            return;
        }

        if (_item.IsThirdParty)
        {
            if (_item.Local == null)
            {
                if (_item.Tool.CanDirectInstall) DownloadInstallerAsync();
                else OpenUrl(_item.Tool.SiteUrl, "官网下载页");
                return;
            }
            Launch();
            return;
        }

        if (_item.Local != null)
        {
            Launch();
            return;
        }

        var release = _item.Release;
        if (release == null)
        {
            MessageBox.Show(this, "暂时无法获取最新版本信息，请稍后重试。", _item.Name,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        InstallAsync(release);
    }

    /// <summary>第三方软件：后台下载官方安装包，完成后自动启动安装向导。</summary>
    private void DownloadInstallerAsync()
    {
        if (_task != null && _task.IsRunning) return;

        var task = DownloadCenter.Find(_item.Tool.Id) ?? DownloadCenter.StartInstaller(_item);
        if (task == null) return;

        BindTask(task, $"正在下载 {_item.Name} 安装包 ...");
    }

    private void InstallAsync(ReleaseInfo release)
    {
        if (_task != null && _task.IsRunning) return;

        var task = DownloadCenter.Find(_item.Tool.Id) ?? DownloadCenter.StartInstall(_item, release);
        if (task == null) return;

        BindTask(task, $"正在下载 {_item.Name} {release.DisplayVersion} ...");
    }

    /// <summary>把详情页的进度条绑定到后台任务，关闭窗口后任务照常继续。</summary>
    private void BindTask(DownloadTask task, string title)
    {
        UnbindTask();
        _task = task;

        ProgressPanel.Visibility = Visibility.Visible;
        DownloadBar.Value = task.Percent;
        ProgressTitle.Text = title;
        ProgressInfo.Text = task.DetailText;
        CancelButton.IsEnabled = true;
        ActionButton.IsEnabled = false;
        ChooseButton.IsEnabled = false;

        task.PropertyChanged += OnTaskChanged;
        UpdateTaskUi();
    }

    private void UnbindTask()
    {
        if (_task == null) return;
        _task.PropertyChanged -= OnTaskChanged;
        _task = null;
    }

    private void OnTaskChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DownloadTask.State)) UpdateTaskUi();
        else if (e.PropertyName is nameof(DownloadTask.Percent) or nameof(DownloadTask.StageText)
                 or nameof(DownloadTask.DetailText)) UpdateTaskUi();
    }

    private void UpdateTaskUi()
    {
        var task = _task;
        if (task == null) return;

        DownloadBar.Value = task.Percent;
        ProgressTitle.Text = task.IsRunning
            ? $"{task.StageText}（{task.Percent:F0}%）"
            : task.StageText;
        ProgressInfo.Text = task.DetailText;
        CancelButton.IsEnabled = task.IsRunning;

        if (!task.IsFinished) return;

        _item.Refresh();
        UnbindTask();
        CancelButton.IsEnabled = false;

        if (task.State == DownloadState.Failed)
        {
            AppState.SetStatus($"{_item.Name} 下载失败：{task.Message}");
            var result = MessageBox.Show(this,
                $"下载失败：{task.Message}\n\n是否改为打开官网手动下载？",
                _item.Name, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes) OpenUrl(_item.Tool.SiteUrl, "官网下载页");
        }
        else
        {
            AppState.SetStatus(task.Message);
        }

        UpdateUi();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_task == null) return;
        DownloadCenter.Cancel(_task);
        CancelButton.IsEnabled = false;
        ProgressTitle.Text = "正在取消 ...";
    }

    private void Reinstall_Click(object sender, RoutedEventArgs e)
    {
        var release = _item.Release;
        if (release == null || _item.IsThirdParty)
        {
            MessageBox.Show(this, "暂时无法获取最新版本信息，请稍后重试。", _item.Name,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(this,
            $"将重新下载并覆盖安装 {_item.Name}。\n请先关闭正在运行的程序，否则文件可能被占用。\n\n继续？",
            "重新安装", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        InstallAsync(release);
    }

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        ToolOperations.Uninstall(this, _item);
        UpdateUi();
    }

    private void Migrate_Click(object sender, RoutedEventArgs e)
    {
        ToolOperations.Migrate(this, _item);
        UpdateUi();
    }

    private void Launch()
    {
        var local = _item.Local;
        if (local == null) return;

        try
        {
            Process.Start(new ProcessStartInfo(local.ExePath)
            {
                WorkingDirectory = Path.GetDirectoryName(local.ExePath) ?? AppState.BaseDir,
                UseShellExecute = true
            });
            AppState.SetStatus($"已启动 {_item.Name}");
            Logger.Info($"启动 {_item.Name}：{local.ExePath}");
        }
        catch (Exception ex)
        {
            Logger.Error($"启动 {_item.Name} 失败：{ex.Message}");
            MessageBox.Show(this, $"启动失败：{ex.Message}", _item.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Choose_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"选择 {_item.Name} 主程序",
            Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true) return;

        ConfigStore.SetManualPath(_item.Tool.Id, dialog.FileName);
        _item.Refresh();
        UpdateUi();
        AppState.SetStatus($"已为 {_item.Name} 指定本地程序");
    }

    private void Folder_Click(object sender, RoutedEventArgs e)
    {
        var local = _item.Local;
        if (local == null) return;
        var directory = Path.GetDirectoryName(local.ExePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;

        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = true });
    }

    private void OpenDownload_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppState.DownloadDir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppState.DownloadDir}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error("打开下载目录失败：" + ex.Message);
        }
    }

    private void Repo_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl(_item.Tool.HasGitHub
            ? _item.Release?.PageUrl ?? _item.Tool.ReleasesUrl
            : _item.Tool.HomepageUrl, "官方网站");
    }

    private static void OpenUrl(string url, string name)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            Logger.Info($"打开 {name}：{url}");
        }
        catch (Exception ex)
        {
            Logger.Error("打开链接失败：" + ex.Message);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
