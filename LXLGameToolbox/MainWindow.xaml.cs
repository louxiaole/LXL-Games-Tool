using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LXLGameToolbox.Core;
using LXLGameToolbox.UI;
using LXLGameToolbox.UI.Pages;
using LXLGameToolbox.UI.Windows;

namespace LXLGameToolbox;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, UserControl> _pages = new();

    public MainWindow()
    {
        InitializeComponent();
        AppState.StatusChanged += OnStatusChanged;
        ThemeManager.Changed += ApplyAppearance;
        Closed += (_, _) =>
        {
            AppState.StatusChanged -= OnStatusChanged;
            ThemeManager.Changed -= ApplyAppearance;
        };

        ApplyAppearance();
        NavTools.IsChecked = true;
        AppState.SetStatus("就绪");
        Loaded += async (_, _) => await RunStartupAsync();
    }

    /// <summary>启动时：版本变化则弹出本次更新内容，并按设置检查线上新版本。</summary>
    private async Task RunStartupAsync()
    {
        // 先记录配置文件是否已存在：全新安装不弹「更新内容」，升级上来的用户弹一次
        var isExistingInstall = System.IO.File.Exists(AppState.ConfigPath);
        var config = ConfigStore.Current;
        var lastSeen = config.LastSeenVersion;
        var versionChanged = isExistingInstall && lastSeen != AppState.VersionText;
        Logger.Info($"启动检查：上次版本={lastSeen}，当前版本={AppState.VersionText}，需要提示={versionChanged}");

        if (versionChanged)
        {
            var notes = UpdateService.BuiltInNotes;
            try
            {
                var release = await UpdateService.FindReleaseAsync(AppState.VersionText);
                if (release != null && !string.IsNullOrWhiteSpace(release.Body)) notes = release.Body;
            }
            catch (Exception ex)
            {
                Logger.Info("读取本次更新说明失败：" + ex.Message);
            }

            var window = new ChangelogWindow(AppState.VersionText, notes) { Owner = this };
            Logger.Info("准备显示更新内容弹窗");
            window.ShowDialog();
            Logger.Info($"更新内容弹窗已关闭，查看日志={window.OpenLogRequested}");
            if (window.OpenLogRequested) NavLog.IsChecked = true;
        }

        if (lastSeen != AppState.VersionText)
        {
            config.LastSeenVersion = AppState.VersionText;
            ConfigStore.Save();
        }

        if (!config.CheckUpdateOnStart) return;

        try
        {
            var latest = await UpdateService.CheckLatestAsync();
            if (UpdateService.IsNewer(latest))
                AppState.SetStatus($"发现工具箱新版本 {latest!.DisplayVersion}，可在「更新日志」中更新");
        }
        catch (Exception ex)
        {
            Logger.Info("检查工具箱更新失败：" + ex.Message);
        }
    }

    /// <summary>按当前配置刷新窗口背景图与遮罩。</summary>
    private void ApplyAppearance()
    {
        var config = ConfigStore.Current;
        var image = ThemeManager.LoadBackgroundImage(config.BackgroundId, config.BackgroundPath);
        if (image == null)
        {
            BackgroundImage.Source = null;
            BackgroundImage.Visibility = Visibility.Collapsed;
            BackgroundScrim.Fill = Brushes.Transparent;
            return;
        }

        BackgroundImage.Source = image;
        BackgroundImage.Visibility = Visibility.Visible;
        BackgroundScrim.Fill = ThemeManager.CreateScrim();
    }

    private void OnStatusChanged(string text)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => StatusLeft.Text = text));
            return;
        }
        StatusLeft.Text = text;
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PageHost == null) return;
        var tag = (sender as FrameworkElement)?.Tag as string ?? "tools";

        if (!_pages.TryGetValue(tag, out var page))
        {
            page = tag switch
            {
                "downloads" => new DownloadsPage(),
                "log" => new UpdateLogPage(),
                "settings" => new SettingsPage(),
                "about" => new AboutPage(),
                _ => new ToolsPage()
            };
            _pages[tag] = page;
        }

        PageHost.Content = page;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaximizeButton.Content = "\uE922";
            MaximizeButton.ToolTip = "最大化";
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeButton.Content = "\uE923";
            MaximizeButton.ToolTip = "还原";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
