using System.IO;

namespace LXLGameToolbox.Core;

/// <summary>专区：小乐自制工具、第三方游戏修改器 等。</summary>
public sealed class ToolZone
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Glyph { get; init; }
    public string ColorHex { get; init; } = "#2E7CF6";
}

/// <summary>专区注册表，新增专区只需在此追加一条。</summary>
public static class ZoneRegistry
{
    public const string Self = "self";
    public const string Mods = "mods";
    public const string Lan = "lan";
    public const string Disk = "disk";
    public const string Assist = "assist";

    public static IReadOnlyList<ToolZone> All { get; } = new List<ToolZone>
    {
        new()
        {
            Id = Self,
            Name = "小乐自制工具",
            Description = "我（小乐）自己开发的工具，支持一键下载、安装、自动更新与启动。",
            Glyph = "LX",
            ColorHex = "#2E7CF6"
        },
        new()
        {
            Id = Mods,
            Name = "游戏修改器专区",
            Description = "收录 GameBuff、风灵月影等第三方游戏修改器：可直接在工具箱内下载安装，并自动接管本机已安装的版本。",
            Glyph = "GB",
            ColorHex = "#7C5CFF"
        },
        new()
        {
            Id = Lan,
            Name = "联机工具专区",
            Description = "我的世界等游戏开服与联机用的第三方工具：提供官网下载入口，并自动检测、启动本机已安装的版本。",
            Glyph = "LAN",
            ColorHex = "#E1498A"
        },
        new()
        {
            Id = Disk,
            Name = "磁盘工具专区",
            Description = "不重装系统就能调整分区的第三方工具：合并 / 拆分分区、把 C 盘空间挪给其它盘，扩容前请先备份重要数据。",
            Glyph = "DK",
            ColorHex = "#14A39A"
        },
        new()
        {
            Id = Assist,
            Name = "游戏辅助专区",
            Description = "游戏性能监测与优化工具：实时显示帧率、硬件温度与占用，帮助找到掉帧原因。",
            Glyph = "GA",
            ColorHex = "#F2994A"
        }
    };

    public static ToolZone Get(string id) =>
        All.FirstOrDefault(zone => zone.Id == id) ?? All[0];
}

/// <summary>一个可注册到工具箱内的工具。新增工具只需在 ToolRegistry 里加一条定义。</summary>
public sealed class ToolDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string ZoneId { get; init; }

    /// <summary>GitHub 仓库所有者（仅自制工具需要）。</summary>
    public string Owner { get; init; } = "";

    /// <summary>GitHub 仓库名（仅自制工具需要）。</summary>
    public string Repo { get; init; } = "";

    /// <summary>发布包里主程序的文件名。</summary>
    public string ExeName { get; init; } = "";

    /// <summary>提供方，例如「小乐自制」「GameBuff 官方」。</summary>
    public string Vendor { get; init; } = "";

    /// <summary>官网首页。</summary>
    public string HomepageUrl { get; init; } = "";

    /// <summary>官网下载页（未填时使用官网首页）。</summary>
    public string DownloadUrl { get; init; } = "";

    /// <summary>安装包直链（第三方软件提供时，工具箱内直接下载安装）。</summary>
    public string DirectInstallUrl { get; init; } = "";

    /// <summary>卡片图标资源文件名（Assets\icons 下），留空则使用文字字形。</summary>
    public string IconAsset { get; init; } = "";

    /// <summary>是否可一键下载安装。页面的下载页会被自动解析为安装包直链。</summary>
    public bool CanDirectInstall => !string.IsNullOrEmpty(DirectInstallUrl);

    /// <summary>卡片图标上的文字。</summary>
    public string Glyph { get; init; } = "LX";

    public string ColorHex { get; init; } = "#2E7CF6";

    /// <summary>本地已有程序的相对路径（相对于工作区根目录），按优先顺序查找。</summary>
    public string[] CandidatePaths { get; init; } = Array.Empty<string>();

    /// <summary>第三方软件：本机可能存在的程序文件名。</summary>
    public string[] DetectNames { get; init; } = Array.Empty<string>();

    /// <summary>第三方软件：安装目录名里可能包含的关键词（小写）。</summary>
    public string[] InstallFolderTokens { get; init; } = Array.Empty<string>();

    /// <summary>允许把已安装目录整体迁移到其他位置（用于安装时无法选路径的软件）。</summary>
    public bool SupportsMigration { get; init; }

    /// <summary>第三方软件在 GitHub 上的版本仓库（仅用于线上版本比对，不参与下载流程）。</summary>
    public string OnlineVersionOwner { get; init; } = "";
    public string OnlineVersionRepo { get; init; } = "";
    public bool HasOnlineVersion => !string.IsNullOrEmpty(OnlineVersionOwner) && !string.IsNullOrEmpty(OnlineVersionRepo);

    /// <summary>下载安装包时携带的 Referer（部分官网有防盗链，缺少时会返回 403）。</summary>
    public string DownloadReferer { get; init; } = "";

    public ToolZone Zone => ZoneRegistry.Get(ZoneId);

    /// <summary>专区排序序号（小乐自制工具在前）。</summary>
    public int ZoneOrder => ZoneRegistry.All.ToList().FindIndex(zone => zone.Id == ZoneId);

    public bool IsSelf => ZoneId == ZoneRegistry.Self;

    public bool HasGitHub => !string.IsNullOrEmpty(Owner) && !string.IsNullOrEmpty(Repo);

    public string RepoUrl => HasGitHub ? $"https://github.com/{Owner}/{Repo}" : HomepageUrl;

    public string ReleasesUrl => HasGitHub ? $"{RepoUrl}/releases" : HomepageUrl;

    public string SiteUrl => string.IsNullOrEmpty(DownloadUrl) ? HomepageUrl : DownloadUrl;
}

/// <summary>工具箱内所有工具的注册表。</summary>
public static class ToolRegistry
{
    public static IReadOnlyList<ToolDefinition> Tools { get; } = new List<ToolDefinition>
    {
        // ---------- 小乐自制工具 ----------
        new()
        {
            Id = "lxlpcl",
            Name = "LXLPCL",
            Description = "我制作的《我的世界》启动器，支持多版本管理、皮肤制作、联机与自动更新。",
            Category = "游戏启动器",
            ZoneId = ZoneRegistry.Self,
            Vendor = "小乐自制",
            Owner = "louxiaole",
            Repo = "LXLPCL",
            ExeName = "LXLPCL.exe",
            Glyph = "LX",
            IconAsset = "lxlpcl.png",
            ColorHex = "#2E7CF6",
            CandidatePaths = new[]
            {
                Path.Combine("我的世界", "LXLPCL", "LXLPCL", "bin", "Release", "net8.0-windows", "LXLPCL.exe"),
                Path.Combine("我的世界", "LXLPCL", "LXLPCL", "bin", "Debug", "net8.0-windows", "LXLPCL.exe")
            }
        },
        new()
        {
            Id = "rust-solo-tools",
            Name = "Rust Solo Tools",
            Description = "Rust 服务器管理工具，单机开服、插件配置与服务器状态监控一站式处理。",
            Category = "服务器工具",
            ZoneId = ZoneRegistry.Self,
            Vendor = "小乐自制",
            Owner = "louxiaole",
            Repo = "RustServerManager",
            ExeName = "RustServerManager.exe",
            Glyph = "RS",
            ColorHex = "#E19A2B",
            CandidatePaths = new[]
            {
                Path.Combine("Rust Solo Tools", "Rust Solo Tools", "RustServerManager.exe"),
                Path.Combine("Rust Solo Tools", "RustServerManager.exe")
            }
        },
        new()
        {
            Id = "store-wiki",
            Name = "小乐商店百科",
            Description = "我制作的商店 / 物品百科查询工具，内置元素、材料、技能等数据，支持快速检索与图标浏览。",
            Category = "游戏百科",
            ZoneId = ZoneRegistry.Self,
            Vendor = "小乐自制",
            Glyph = "BK",
            ColorHex = "#7C5CFF",
            CandidatePaths = new[]
            {
                Path.Combine("小乐商店百科", "release", "win-unpacked", "小乐商店百科.exe"),
                Path.Combine("小乐商店百科", "小乐商店百科.exe")
            },
            DetectNames = new[] { "小乐商店百科.exe" },
            InstallFolderTokens = new[] { "小乐商店百科" }
        },

        // ---------- 游戏修改器专区（第三方） ----------
        new()
        {
            Id = "gamebuff",
            Name = "GameBuff",
            Description = "支持 7000+ 款单机游戏修改器，客户端内挑选游戏后一键启用修改项。官网唯一域名为 gamebuff.cn，其他后缀均非官方。",
            Category = "游戏修改器",
            ZoneId = ZoneRegistry.Mods,
            Vendor = "GameBuff 官方",
            HomepageUrl = "https://gamebuff.cn",
            DownloadUrl = "https://gamebuff.cn",
            DirectInstallUrl = "https://dl.gamebuff.cn/download/GameBuff_Setup.exe",
            Glyph = "GB",
            IconAsset = "gamebuff.png",
            ColorHex = "#7C5CFF",
            DetectNames = new[] { "GameBuff.exe" },
            InstallFolderTokens = new[] { "gamebuff" }
        },
        new()
        {
            Id = "fling-trainer",
            Name = "风灵月影",
            Description = "FLiNG Trainer 免费修改器发布站，按游戏索引单个修改器，下载解压后与游戏同时运行即可。",
            Category = "游戏修改器",
            ZoneId = ZoneRegistry.Mods,
            Vendor = "FLiNG Trainer",
            HomepageUrl = "https://flingtrainer.com",
            DownloadUrl = "https://flingtrainer.com",
            Glyph = "FL",
            IconAsset = "flingtrainer.png",
            ColorHex = "#23A55A"
        },
        new()
        {
            Id = "wemod",
            Name = "WeMod",
            Description = "一站式修改器平台，自动识别本机游戏，社区维护的修改项一键开关，改动即时生效、可随时恢复。",
            Category = "游戏修改器",
            ZoneId = ZoneRegistry.Mods,
            Vendor = "WeMod 官方",
            HomepageUrl = "https://www.wemod.com",
            DownloadUrl = "https://www.wemod.com/download",
            DirectInstallUrl = "https://api.wemod.com/client/download",
            Glyph = "WM",
            IconAsset = "wemod.png",
            ColorHex = "#1C8EE0",
            DetectNames = new[] { "Wand.exe", "WeMod.exe" },
            InstallFolderTokens = new[] { "wemod", "wand" }
        },
        new()
        {
            Id = "cheat-engine",
            Name = "Cheat Engine",
            Description = "开源内存修改与调试工具，可自行搜索数值、编写脚本，适合进阶玩家做针对性修改。",
            Category = "内存修改工具",
            ZoneId = ZoneRegistry.Mods,
            Vendor = "Cheat Engine 官方",
            HomepageUrl = "https://cheatengine.org",
            DownloadUrl = "https://cheatengine.org/download.php",
            // 官网下载页地址会在下载时自动解析出当前的 exe 直链
            DirectInstallUrl = "https://www.cheatengine.org/download.php",
            Glyph = "CE",
            IconAsset = "cheatengine.png",
            ColorHex = "#8A63D2",
            DetectNames = new[] { "CheatEngine.exe", "Cheat Engine.exe", "cheatengine-x86_64.exe" },
            InstallFolderTokens = new[] { "cheat engine", "cheatengine" },
            OnlineVersionOwner = "cheat-engine",
            OnlineVersionRepo = "cheat-engine"
        },

        // ---------- 联机工具专区 ----------
        new()
        {
            Id = "sakurafrp",
            Name = "樱花内网穿透（SakuraFrp）",
            Description = "我的世界联机常用的内网穿透工具：把本地开放到公网，好友不用公网 IP 也能直接连进你的存档或服务器。下载需登录官网，工具箱负责检测与一键启动。",
            Category = "联机工具",
            ZoneId = ZoneRegistry.Lan,
            Vendor = "SakuraFrp 官方",
            HomepageUrl = "https://www.natfrp.com",
            DownloadUrl = "https://www.natfrp.com",
            Glyph = "SK",
            IconAsset = "sakurafrp.png",
            ColorHex = "#E1498A",
            DetectNames = new[] { "SakuraLauncher.exe", "SakuraFrpLauncher.exe", "SakuraFrp.exe" },
            InstallFolderTokens = new[] { "sakura", "natfrp" },
            SupportsMigration = true
        },

        // ---------- 磁盘工具专区（第三方） ----------
        new()
        {
            Id = "partition-assistant",
            Name = "分区助手",
            Description = "傲梅 Partition Assistant 标准版：不重装系统即可合并、拆分、扩容分区，把空余空间从别的盘划给 C 盘。操作前请先备份重要数据。",
            Category = "磁盘分区工具",
            ZoneId = ZoneRegistry.Disk,
            Vendor = "傲梅科技 官方",
            HomepageUrl = "https://www.disktool.cn",
            DownloadUrl = "https://www.disktool.cn/download.html",
            Glyph = "PA",
            ColorHex = "#2E9E7B",
            DetectNames = new[] { "PartitionAssistant.exe", "PartAssist.exe", "PAInstall.exe" },
            InstallFolderTokens = new[] { "partition assistant", "分区助手", "aomei", "partassist" }
        },
        new()
        {
            Id = "diskgenius",
            Name = "DiskGenius",
            Description = "国产磁盘管理与数据恢复工具：分区调整、无损扩容、坏道检测、数据恢复都能做，免费版即可满足日常扩容需求。",
            Category = "磁盘分区工具",
            ZoneId = ZoneRegistry.Disk,
            Vendor = "DiskGenius 官方",
            HomepageUrl = "https://www.diskgenius.cn",
            DownloadUrl = "https://www.diskgenius.cn/download.php",
            Glyph = "DG",
            ColorHex = "#3A7BD5",
            DetectNames = new[] { "DiskGenius.exe" },
            InstallFolderTokens = new[] { "diskgenius", "disk genius" }
        },

        // ---------- 游戏辅助专区（第三方） ----------
        new()
        {
            Id = "gamepp",
            Name = "游戏加加",
            Description = "国产游戏性能监测与优化工具：实时显示帧率、CPU / 显卡占用与温度，支持游戏内悬浮窗、性能评分与一键优化，帮助定位掉帧与卡顿。",
            Category = "游戏性能监测",
            ZoneId = ZoneRegistry.Assist,
            Vendor = "游戏加加 官方",
            HomepageUrl = "https://gamepp.com",
            DownloadUrl = "https://gamepp.com",
            DirectInstallUrl = "https://dllite.gamepp.com/download/GamePPLite_Setup.exe",
            DownloadReferer = "https://gamepp.com/",
            Glyph = "GP",
            IconAsset = "gamepp.png",
            ColorHex = "#F2994A",
            DetectNames = new[] { "GamePP.exe", "GamePPLauncher.exe" },
            InstallFolderTokens = new[] { "gamepp", "游戏加加" }
        }
    };

    public static IEnumerable<string> Categories => Tools.Select(tool => tool.Category).Distinct();

    public static IEnumerable<ToolDefinition> InZone(string zoneId) =>
        Tools.Where(tool => tool.ZoneId == zoneId);
}
