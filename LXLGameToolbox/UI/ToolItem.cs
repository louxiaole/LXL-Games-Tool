using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LXLGameToolbox.Core;

namespace LXLGameToolbox.UI;

public enum ToolAction
{
    Install,
    Update,
    Launch,
    OpenSite,

    /// <summary>第三方软件：工具箱内直接下载官方安装包并启动安装。</summary>
    DownloadInstaller,

    /// <summary>自制工具未接入自动更新：手动指定本机已安装的程序。</summary>
    PickProgram
}

/// <summary>工具卡片 / 详情窗口共用的视图模型。</summary>
public sealed class ToolItem : INotifyPropertyChanged
{
    private static readonly Brush InstallBrush = Brush(0x9A, 0xA1, 0xAC);
    private static readonly Brush InstallBackground = Brush(0x1A, 0x9A, 0xA1, 0xAC);
    private static readonly Brush UpdateBrush = Brush(0x2E, 0x7C, 0xF6);
    private static readonly Brush UpdateBackground = Brush(0x1A, 0x2E, 0x7C, 0xF6);
    private static readonly Brush ReadyBrush = Brush(0x23, 0xA5, 0x5A);
    private static readonly Brush ReadyBackground = Brush(0x1A, 0x23, 0xA5, 0x5A);

    public ToolItem(ToolDefinition tool)
    {
        Tool = tool;
        IconBrush = CreateIconBrush(tool.ColorHex);
        IconImage = ToolIcon.Load(tool);
        ZoneBrush = CreateIconBrush(tool.Zone.ColorHex);
        ZoneOrder = ZoneRegistry.All.ToList().FindIndex(zone => zone.Id == tool.ZoneId);
        Refresh();
    }

    public ToolDefinition Tool { get; }

    public ReleaseInfo? Release { get; private set; }
    public LocalToolInfo? Local { get; private set; }

    /// <summary>第三方软件的线上最新版本（用于判断本机是否为旧版）。</summary>
    public string OnlineVersion { get; private set; } = "";

    public string Name => Tool.Name;
    public string Description => Tool.Description;
    public string Category => Tool.Category;
    public string Glyph => Tool.Glyph;
    public string SourceLabel => Tool.HasGitHub ? Tool.Repo : Tool.Vendor;
    public Brush IconBrush { get; }

    /// <summary>软件自带的图标；为空时回退到文字字形。</summary>
    public BitmapSource? IconImage { get; }
    public Visibility IconVisibility => IconImage == null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility GlyphVisibility => IconImage == null ? Visibility.Visible : Visibility.Collapsed;
    public Brush IconBackground => IconImage == null ? IconBrush : Brushes.Transparent;

    public string ZoneName => Tool.Zone.Name;
    public string ZoneDescription => Tool.Zone.Description;
    public Brush ZoneBrush { get; }
    public int ZoneOrder { get; }

    /// <summary>第三方专区软件（自制专区里没有 GitHub 仓库的工具不算第三方）。</summary>
    public bool IsThirdParty => !Tool.HasGitHub && !Tool.IsSelf;

    public string StatusText { get; private set; } = "检测中";
    public Brush StatusBrush { get; private set; } = InstallBrush;
    public Brush StatusBackground { get; private set; } = InstallBackground;
    public string VersionText { get; private set; } = "正在检测 ...";
    public string PrimaryText { get; private set; } = "安装";
    public ToolAction PrimaryAction { get; private set; } = ToolAction.Install;
    public bool HasUpdate { get; private set; }

    /// <summary>单独显示的更新按钮文案（有更新时才有意义）。</summary>
    public string UpdateText { get; private set; } = "更新";

    /// <summary>已安装且有新版本时，额外显示一个独立的「更新」按钮，主按钮仍然是启动。</summary>
    public Visibility UpdateVisibility => HasUpdate && Local != null && !IsBusy
        ? Visibility.Visible
        : Visibility.Collapsed;

    public bool IsBusy { get; private set; }
    public string BusyText { get; private set; } = "";
    public double Progress { get; private set; }
    public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>已安装时才显示卸载按钮。</summary>
    public Visibility UninstallVisibility => Local != null ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>安装目录不在工具箱管理目录内、且支持迁移时才显示迁移按钮。</summary>
    public Visibility MigrateVisibility => Local != null && !Local.IsManaged && Tool.SupportsMigration
        ? Visibility.Visible
        : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetRelease(ReleaseInfo? release)
    {
        Release = release;
    }

    /// <summary>写入线上版本号，并重新计算是否为旧版。</summary>
    public void SetOnlineVersion(string version)
    {
        if (string.IsNullOrEmpty(version) || version == OnlineVersion) return;
        OnlineVersion = version;
        Refresh();
    }

    /// <summary>重新读取本地安装情况并刷新卡片状态。</summary>
    public void Refresh()
    {
        Local = LocalScanner.Resolve(Tool);
        var localVersion = Local?.Version ?? "";

        // 第三方软件不做版本比对，只区分「已安装 / 未安装」
        if (!Tool.HasGitHub)
        {
            HasUpdate = false;

            // 自制工具但未接入 GitHub 自动更新（如小乐商店百科）：只做本机启动与手动指定
            if (Tool.IsSelf)
            {
                if (Local == null)
                {
                    StatusText = "未检测到";
                    StatusBrush = InstallBrush;
                    StatusBackground = InstallBackground;
                    PrimaryText = "指定程序";
                    PrimaryAction = ToolAction.PickProgram;
                    VersionText = "尚未检测到本机安装，可指定程序路径";
                }
                else
                {
                    StatusText = "已就绪";
                    StatusBrush = ReadyBrush;
                    StatusBackground = ReadyBackground;
                    PrimaryText = "启动";
                    PrimaryAction = ToolAction.Launch;
                    VersionText = string.IsNullOrEmpty(localVersion)
                        ? $"本地版本未知 · {Local.Source}"
                        : $"本地 {localVersion} · {Local.Source}";
                }

                RaiseAll();
                return;
            }

            if (Local == null)
            {
                StatusText = "未安装";
                StatusBrush = InstallBrush;
                StatusBackground = InstallBackground;
                PrimaryText = Tool.CanDirectInstall ? "下载并安装" : "官网下载";
                PrimaryAction = Tool.CanDirectInstall ? ToolAction.DownloadInstaller : ToolAction.OpenSite;
                VersionText = Tool.CanDirectInstall
                    ? "官方安装包 · 工具箱内直接下载安装"
                    : Tool.ZoneId == ZoneRegistry.Lan
                        ? "前往官网登录后获取安装包"
                        : "前往官网获取安装包与版本更新";
            }
            else
            {
                // 官方版本以线上版本或已下载安装包中的版本为准，取较高的一个
                var installerVersion = Installer.ReadInstallerVersion(Tool);
                var officialVersion = VersionUtil.IsNewer(installerVersion, OnlineVersion) ? installerVersion : OnlineVersion;
                var hasOfficial = !string.IsNullOrEmpty(officialVersion);
                var outdated = hasOfficial && !string.IsNullOrEmpty(localVersion) &&
                               VersionUtil.IsNewer(officialVersion, localVersion);

                if (outdated)
                {
                    // 旧版提示放在徽标上，主按钮保持「启动」，更新单独给一个按钮
                    HasUpdate = true;
                    StatusText = "旧版";
                    StatusBrush = UpdateBrush;
                    StatusBackground = UpdateBackground;
                    PrimaryText = "启动";
                    PrimaryAction = ToolAction.Launch;
                    UpdateText = $"更新到 {officialVersion}";
                    VersionText = $"本地 {localVersion} · 官方 {officialVersion}";
                }
                else
                {
                    HasUpdate = false;
                    StatusText = hasOfficial && !string.IsNullOrEmpty(localVersion) ? "已是最新" : "已安装";
                    StatusBrush = ReadyBrush;
                    StatusBackground = ReadyBackground;
                    PrimaryText = "启动";
                    PrimaryAction = ToolAction.Launch;
                    UpdateText = "更新";
                    VersionText = hasOfficial && !string.IsNullOrEmpty(localVersion)
                        ? $"本地 {localVersion} · 官方 {officialVersion}"
                        : string.IsNullOrEmpty(localVersion)
                            ? $"本地版本未知 · {Local.Source}"
                            : $"本地 {localVersion} · {Local.Source}";
                }
            }

            RaiseAll();
            return;
        }

        var latestVersion = Release?.DisplayVersion ?? "";
        HasUpdate = Local != null && Release != null && !string.IsNullOrEmpty(localVersion) &&
                    VersionUtil.IsNewer(latestVersion, localVersion);

        if (Local == null)
        {
            StatusText = "未安装";
            StatusBrush = InstallBrush;
            StatusBackground = InstallBackground;
            PrimaryText = "安装";
            PrimaryAction = ToolAction.Install;
            UpdateText = "更新";
        }
        else if (HasUpdate)
        {
            StatusText = "可更新";
            StatusBrush = UpdateBrush;
            StatusBackground = UpdateBackground;
            PrimaryText = "启动";
            PrimaryAction = ToolAction.Launch;
            UpdateText = $"更新到 {latestVersion}";
        }
        else
        {
            StatusText = "已就绪";
            StatusBrush = ReadyBrush;
            StatusBackground = ReadyBackground;
            PrimaryText = "启动";
            PrimaryAction = ToolAction.Launch;
            UpdateText = "更新";
        }

        if (Release != null)
        {
            VersionText = string.IsNullOrEmpty(localVersion)
                ? $"最新 {latestVersion} · 本地版本未知"
                : $"最新 {latestVersion} · 本地 {localVersion}";
        }
        else if (Local != null)
        {
            VersionText = string.IsNullOrEmpty(localVersion)
                ? $"本地版本未知 · {Local.Source}"
                : $"本地 {localVersion} · {Local.Source}";
        }
        else
        {
            VersionText = "尚未检测到最新版本";
        }

        RaiseAll();
    }

    public void SetBusy(bool busy, string text = "")
    {
        IsBusy = busy;
        BusyText = text;
        if (!busy) Progress = 0;
        RaiseAll();
    }

    public void SetProgress(double percent, string text)
    {
        Progress = Math.Clamp(percent, 0, 100);
        BusyText = text;
        RaiseAll();
    }

    private void RaiseAll()
    {
        var names = new[]
        {
            nameof(StatusText), nameof(StatusBrush), nameof(StatusBackground), nameof(VersionText),
            nameof(PrimaryText), nameof(PrimaryAction), nameof(HasUpdate),
            nameof(UpdateText), nameof(UpdateVisibility),
            nameof(IsBusy), nameof(BusyText), nameof(Progress), nameof(BusyVisibility),
            nameof(UninstallVisibility), nameof(MigrateVisibility)
        };
        foreach (var name in names)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private static Brush CreateIconBrush(string hex)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        catch
        {
            return new SolidColorBrush(Color.FromRgb(0x2E, 0x7C, 0xF6));
        }
    }

    private static Brush Brush(byte r, byte g, byte b) => new SolidColorBrush(Color.FromRgb(r, g, b));

    private static Brush Brush(byte a, byte r, byte g, byte b) => new SolidColorBrush(Color.FromArgb(a, r, g, b));
}
