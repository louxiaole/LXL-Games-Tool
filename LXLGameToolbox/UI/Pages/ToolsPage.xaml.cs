using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using LXLGameToolbox.Core;
using LXLGameToolbox.UI.Windows;

namespace LXLGameToolbox.UI.Pages;

public partial class ToolsPage : UserControl
{
    private const string AllZones = "all";
    private const string AllCategories = "全部";

    private readonly ObservableCollection<ToolItem> _all = new();
    private readonly ICollectionView _view;
    private string _query = "";
    private string _zone = AllZones;
    private string _category = AllCategories;
    private bool _started;

    public ToolsPage()
    {
        InitializeComponent();

        foreach (var tool in ToolRegistry.Tools) _all.Add(new ToolItem(tool));

        _view = CollectionViewSource.GetDefaultView(_all);
        _view.Filter = FilterItem;
        _view.GroupDescriptions.Add(new PropertyGroupDescription("ZoneName"));
        _view.SortDescriptions.Add(new SortDescription("ZoneOrder", ListSortDirection.Ascending));
        _view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
        ToolList.ItemsSource = _view;

        BuildZoneChips();
        BuildCategoryChips();
        UpdateSummary();

        Loaded += async (_, _) =>
        {
            if (_started) return;
            _started = true;
            if (ConfigStore.Current.CheckUpdateOnStart) await CheckUpdatesAsync();
            else AppState.SetStatus("已跳过自动检查（可在设置中开启）");
        };
    }

    // ---------- 列表 / 筛选 ----------

    private bool FilterItem(object obj)
    {
        if (obj is not ToolItem item) return false;
        if (_zone != AllZones && item.Tool.ZoneId != _zone) return false;
        if (_category != AllCategories && item.Category != _category) return false;
        if (string.IsNullOrWhiteSpace(_query)) return true;

        var query = _query.Trim();
        return item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
               || item.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
               || item.ZoneName.Contains(query, StringComparison.OrdinalIgnoreCase)
               || item.Tool.Vendor.Contains(query, StringComparison.OrdinalIgnoreCase)
               || item.Tool.Repo.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void BuildZoneChips()
    {
        ZonePanel.Children.Clear();
        ZonePanel.Children.Add(CreateZoneChip(AllZones, "全部专区", null, "显示两个专区的全部工具", true));

        foreach (var zone in ZoneRegistry.All)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(zone.ColorHex));
            var count = ToolRegistry.InZone(zone.Id).Count();
            var chip = CreateZoneChip(zone.Id, $"{zone.Name}（{count}）", brush, zone.Description, false);
            ZonePanel.Children.Add(chip);
        }
    }

    private RadioButton CreateZoneChip(string id, string text, Brush? dot, string tooltip, bool selected)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        if (dot != null)
        {
            content.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = dot,
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        content.Children.Add(new TextBlock { Text = text });

        var chip = new RadioButton
        {
            Content = content,
            Tag = id,
            GroupName = "ToolZone",
            Style = (Style)FindResource("FilterChip"),
            Margin = new Thickness(0, 0, 8, 8),
            ToolTip = tooltip
        };
        System.Windows.Automation.AutomationProperties.SetName(chip, text);
        chip.IsChecked = selected;
        chip.Checked += ZoneChip_Checked;
        return chip;
    }

    private void ZoneChip_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton chip) return;
        _zone = chip.Tag as string ?? AllZones;
        _category = AllCategories;
        BuildCategoryChips();
        _view.Refresh();
    }

    private void BuildCategoryChips()
    {
        CategoryPanel.Children.Clear();
        CategoryPanel.Children.Add(CreateCategoryChip(AllCategories, true));

        var categories = _all
            .Where(item => _zone == AllZones || item.Tool.ZoneId == _zone)
            .Select(item => item.Category)
            .Distinct();
        foreach (var category in categories)
            CategoryPanel.Children.Add(CreateCategoryChip(category, false));
    }

    private RadioButton CreateCategoryChip(string text, bool selected)
    {
        var chip = new RadioButton
        {
            Content = text,
            Tag = text,
            GroupName = "ToolCategory",
            Style = (Style)FindResource("FilterChip"),
            Margin = new Thickness(0, 0, 8, 8)
        };
        System.Windows.Automation.AutomationProperties.SetName(chip, text);
        chip.IsChecked = selected;
        chip.Checked += CategoryChip_Checked;
        return chip;
    }

    private void CategoryChip_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton chip) return;
        _category = chip.Tag as string ?? AllCategories;
        _view.Refresh();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchPlaceholder == null) return;
        _query = SearchBox.Text;
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(_query) ? Visibility.Visible : Visibility.Collapsed;
        _view.Refresh();
    }

    private void UpdateSummary()
    {
        if (SummaryText == null) return;
        var total = _all.Count;
        var installed = _all.Count(item => item.Local != null);
        var updates = _all.Count(item => item.HasUpdate);
        SummaryText.Text = $"已就绪 {installed} / {total} 个工具" +
                           (updates > 0 ? $" · {updates} 个可更新" : " · 自制工具均为最新版本");
    }

    // ---------- 版本检查 ----------

    private async Task CheckUpdatesAsync()
    {
        AppState.SetStatus("正在检查工具版本 ...");
        var targets = _all.Where(item => item.Tool.HasGitHub).ToList();
        var onlineTargets = _all.Where(item => !item.Tool.HasGitHub && item.Tool.HasOnlineVersion).ToList();

        var tasks = targets.Select(async item =>
        {
            var release = await GitHubClient.GetLatestAsync(item.Tool);
            return (item, release);
        }).ToList();

        var onlineTasks = onlineTargets.Select(async item =>
        {
            var version = await GitHubClient.GetLatestTagAsync(item.Tool.OnlineVersionOwner,
                item.Tool.OnlineVersionRepo, item.Name);
            return (item, version);
        }).ToList();

        var results = await Task.WhenAll(tasks);
        foreach (var (item, release) in results)
        {
            item.SetRelease(release);
            item.Refresh();
        }

        var onlineResults = await Task.WhenAll(onlineTasks);
        foreach (var (item, version) in onlineResults) item.SetOnlineVersion(version);

        UpdateSummary();
        _view.Refresh();

        var updates = _all.Count(item => item.HasUpdate);
        var failed = targets.Count(item => item.Release == null);
        AppState.SetStatus(updates > 0
            ? $"发现 {updates} 个工具可更新"
            : failed > 0
                ? "部分工具版本信息获取失败，可稍后重试"
                : "所有自制工具均为最新版本");
    }

    // ---------- 卡片操作 ----------

    private void CardPrimary_Click(object sender, RoutedEventArgs e)
    {
        var item = (sender as FrameworkElement)?.DataContext as ToolItem;
        if (item == null) return;

        switch (item.PrimaryAction)
        {
            case ToolAction.Launch when item.Local != null:
                Launch(item);
                break;
            case ToolAction.OpenSite:
                OpenUrl(item.Tool.SiteUrl, item.Name);
                break;
            case ToolAction.DownloadInstaller:
                StartBackgroundDownload(item);
                break;
            case ToolAction.Update:
                StartBackgroundDownload(item);
                break;
            case ToolAction.PickProgram:
                PickProgram(item);
                break;
            default:
                OpenDetail(item);
                break;
        }
    }

    /// <summary>把下载 / 更新放到后台执行，卡片上直接显示进度，不阻塞界面。</summary>
    private void StartBackgroundDownload(ToolItem item)
    {
        if (item.IsBusy || DownloadCenter.Find(item.Tool.Id) != null) return;

        if (!item.Tool.HasGitHub)
        {
            if (!item.Tool.CanDirectInstall)
            {
                OpenUrl(item.Tool.SiteUrl, item.Name);
                return;
            }

            DownloadCenter.StartInstaller(item);
            AppState.SetStatus($"{item.Name} 已加入后台下载");
            return;
        }

        if (item.Release == null || !item.Release.HasAsset)
        {
            OpenDetail(item);
            return;
        }

        DownloadCenter.StartInstall(item, item.Release);
        AppState.SetStatus($"{item.Name} 已加入后台更新");
    }

    /// <summary>自制工具：手动指定本机已安装的主程序。</summary>
    private void PickProgram(ToolItem item)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"选择 {item.Name} 主程序",
            Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        ConfigStore.SetManualPath(item.Tool.Id, dialog.FileName);
        item.Refresh();
        UpdateSummary();
        _view.Refresh();
        AppState.SetStatus($"已为 {item.Name} 指定本地程序");
    }

    private void CardUpdate_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ToolItem item) return;
        StartBackgroundDownload(item);
    }

    private void CardDetail_Click(object sender, RoutedEventArgs e)
    {
        var item = (sender as FrameworkElement)?.DataContext as ToolItem;
        if (item != null) OpenDetail(item);
    }

    private void CardUninstall_Click(object sender, RoutedEventArgs e)
    {
        var item = (sender as FrameworkElement)?.DataContext as ToolItem;
        if (item == null) return;

        ToolOperations.Uninstall(Window.GetWindow(this), item);
        UpdateSummary();
        _view.Refresh();
    }

    private void OpenDetail(ToolItem item)
    {
        var window = new ToolDetailWindow(item) { Owner = Window.GetWindow(this) };
        window.ShowDialog();

        item.Refresh();
        UpdateSummary();
        _view.Refresh();
    }

    private void Launch(ToolItem item)
    {
        if (item.Local == null) return;
        try
        {
            Process.Start(new ProcessStartInfo(item.Local.ExePath)
            {
                WorkingDirectory = Path.GetDirectoryName(item.Local.ExePath) ?? AppState.BaseDir,
                UseShellExecute = true
            });
            AppState.SetStatus($"已启动 {item.Name}");
            Logger.Info($"启动 {item.Name}：{item.Local.ExePath}");
        }
        catch (Exception ex)
        {
            Logger.Error($"启动 {item.Name} 失败：{ex.Message}");
            MessageBox.Show(Window.GetWindow(this), $"启动失败：{ex.Message}", item.Name,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void OpenUrl(string url, string name)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            AppState.SetStatus($"正在打开 {name} 官网");
            Logger.Info($"打开 {name} 官网：{url}");
        }
        catch (Exception ex)
        {
            Logger.Error($"打开 {name} 官网失败：{ex.Message}");
        }
    }

    // ---------- 批量操作 ----------

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        LocalScanner.Invalidate();
        foreach (var item in _all) item.Refresh();
        UpdateSummary();
        _view.Refresh();
        var installed = _all.Count(item => item.Local != null);
        AppState.SetStatus($"本地扫描完成，找到 {installed} 个可用工具");
    }

    private void UpdateAll_Click(object sender, RoutedEventArgs e)
    {
        var targets = _all.Where(item => item.HasUpdate && item.Release != null).ToList();
        var thirdParty = _all.Where(item => item.HasUpdate && item.Release == null).ToList();

        if (targets.Count == 0 && thirdParty.Count == 0)
        {
            AppState.SetStatus("没有需要更新的工具");
            return;
        }

        var queued = 0;
        foreach (var item in targets)
        {
            if (DownloadCenter.StartInstall(item, item.Release!) != null) queued++;
        }

        foreach (var item in thirdParty)
        {
            if (!item.Tool.CanDirectInstall) continue;
            if (DownloadCenter.StartInstaller(item) != null) queued++;
        }

        AppState.SetStatus(queued > 0
            ? $"已加入后台更新：{queued} 个工具，可在「下载中心」查看进度"
            : "所有可自动更新的工具都在下载中");
    }
}
