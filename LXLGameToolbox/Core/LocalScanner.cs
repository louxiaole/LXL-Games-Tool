using System.Diagnostics;
using System.IO;

namespace LXLGameToolbox.Core;

/// <summary>本地找到的工具实例。</summary>
public sealed class LocalToolInfo
{
    public string ExePath { get; init; } = "";
    public string Version { get; init; } = "";
    public string Source { get; init; } = "";

    /// <summary>程序所在目录。</summary>
    public string InstallDir { get; init; } = "";

    /// <summary>软件自带的卸载程序（找不到为空）。</summary>
    public string UninstallerPath { get; init; } = "";

    /// <summary>是否安装在工具箱自己的 tools / apps 目录内（可由工具箱直接删除）。</summary>
    public bool IsManaged { get; init; }
}

/// <summary>
/// 查找本机已有的工具：
/// 自制工具 → 手动指定 → 工具箱安装目录 → 工作区候选路径；
/// 第三方软件 → 手动指定 → 常见安装目录扫描。
/// </summary>
public static class LocalScanner
{
    private static bool _workspaceResolved;
    private static string? _workspaceRoot;

    private static readonly Dictionary<string, (DateTime Time, LocalToolInfo? Info)> Cache = new();
    private static readonly TimeSpan CacheAge = TimeSpan.FromSeconds(30);

    /// <summary>工作区根目录（"游戏项目开发"），找不到则为 null。</summary>
    public static string? WorkspaceRoot
    {
        get
        {
            if (_workspaceResolved) return _workspaceRoot;
            _workspaceResolved = true;
            var directory = new DirectoryInfo(AppState.BaseDir);
            while (directory != null)
            {
                if (directory.Name == "游戏项目开发")
                {
                    _workspaceRoot = directory.FullName;
                    break;
                }
                directory = directory.Parent;
            }
            return _workspaceRoot;
        }
    }

    public static LocalToolInfo? Resolve(ToolDefinition tool)
    {
        if (ConfigStore.Current.ManualPaths.TryGetValue(tool.Id, out var manual) &&
            !string.IsNullOrWhiteSpace(manual) && File.Exists(manual))
        {
            return Create(manual, ReadVersion(manual), "手动指定");
        }

        lock (Cache)
        {
            if (Cache.TryGetValue(tool.Id, out var cached) && DateTime.Now - cached.Time < CacheAge)
                return cached.Info;
        }

        var info = ResolveCore(tool);

        lock (Cache)
        {
            Cache[tool.Id] = (DateTime.Now, info);
        }

        return info;
    }

    /// <summary>清空检测缓存（安装、卸载、迁移或手动重新扫描后调用）。</summary>
    public static void Invalidate()
    {
        lock (Cache) Cache.Clear();
    }

    private static LocalToolInfo? ResolveCore(ToolDefinition tool)
    {
        if (!tool.IsSelf) return ScanInstalledFolders(tool);

        var record = Installer.ReadRecord(tool);
        if (record != null && !string.IsNullOrEmpty(record.ExePath) && File.Exists(record.ExePath))
        {
            return Create(record.ExePath, record.Version, "工具箱安装");
        }

        var root = WorkspaceRoot;
        if (root != null)
        {
            foreach (var relative in tool.CandidatePaths)
            {
                var full = Path.Combine(root, relative);
                if (!File.Exists(full)) continue;
                return Create(full, ReadVersion(full), "本地已有");
            }
        }

        // 安装到用户目录 / Program Files 的情况（含 Electron 默认的 %LOCALAPPDATA%\Programs）
        return ScanInstalledFolders(tool);
    }

    private static LocalToolInfo Create(string exePath, string version, string source)
    {
        var directory = Path.GetDirectoryName(exePath) ?? "";
        return new LocalToolInfo
        {
            ExePath = exePath,
            Version = version,
            Source = source,
            InstallDir = directory,
            IsManaged = AppState.IsManagedPath(exePath),
            UninstallerPath = FindUninstaller(directory)
        };
    }

    /// <summary>在安装目录（含一层子目录）查找软件自带的卸载程序。</summary>
    private static string FindUninstaller(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return "";

        var patterns = new[] { "unins*.exe", "uninstall*.exe", "uninst*.exe" };
        try
        {
            foreach (var pattern in patterns)
            {
                var files = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly);
                if (files.Length > 0) return files.OrderBy(path => path.Length).First();
            }

            foreach (var sub in Directory.EnumerateDirectories(directory))
            {
                foreach (var pattern in patterns)
                {
                    var files = Directory.GetFiles(sub, pattern, SearchOption.TopDirectoryOnly);
                    if (files.Length > 0) return files.OrderBy(path => path.Length).First();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Info($"查找 {directory} 的卸载程序失败：{ex.Message}");
        }

        return "";
    }

    /// <summary>在用户目录、Program Files 以及其它磁盘的常见软件目录里按关键词查找第三方软件。</summary>
    private static LocalToolInfo? ScanInstalledFolders(ToolDefinition tool)
    {
        if (tool.DetectNames.Length == 0 || tool.InstallFolderTokens.Length == 0) return null;

        // 系统盘之外的软件常见于 D:\APP、E:\Games 这类二级目录，所以磁盘根目录向下找两层
        foreach (var root in SearchRoots())
        {
            var hit = ScanRoot(root, tool, 1);
            if (hit != null) return hit;
        }

        foreach (var drive in OtherDriveRoots())
        {
            var hit = ScanRoot(drive, tool, 2);
            if (hit != null) return hit;
        }

        return null;
    }

    private static bool MatchesFolder(string directory, ToolDefinition tool)
    {
        var name = Path.GetFileName(directory).ToLowerInvariant();
        return tool.InstallFolderTokens.Any(token => name.Contains(token, StringComparison.Ordinal));
    }

    /// <summary>从 root 往下逐层查找目录名匹配的安装目录，最多下探 maxLevel 层。</summary>
    private static LocalToolInfo? ScanRoot(string root, ToolDefinition tool, int maxLevel)
    {
        if (!Directory.Exists(root)) return null;

        var visited = 0;
        var current = new List<string> { root };

        for (var depth = 0; depth <= maxLevel && current.Count > 0; depth++)
        {
            var next = new List<string>();

            foreach (var directory in current)
            {
                try
                {
                    if (depth > 0 && MatchesFolder(directory, tool))
                    {
                        var exe = FindExe(directory, tool.DetectNames);
                        if (exe != null) return Create(exe, ReadVersion(exe), "本地已安装");
                    }

                    if (depth < maxLevel)
                    {
                        foreach (var child in Directory.EnumerateDirectories(directory))
                        {
                            if (IsNoiseFolder(child)) continue;
                            next.Add(child);
                            if (++visited > MaxScannedFolders) break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info($"扫描目录 {directory} 失败：{ex.Message}");
                }

                if (visited > MaxScannedFolders) break;
            }

            if (visited > MaxScannedFolders) break;
            current = next;
        }

        return null;
    }

    private const int MaxScannedFolders = 30000;

    private static bool IsNoiseFolder(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        return name is "windows" or "winsxs" or "$recycle.bin" or "system volume information"
            or "node_modules" or "packages" or "windowsapps";
    }

    private static IEnumerable<string> SearchRoots()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (Directory.Exists(localAppData)) yield return localAppData;

        // Electron / Squirrel 安装器默认会把程序装到 %LOCALAPPDATA%\Programs
        var userPrograms = Path.Combine(localAppData, "Programs");
        if (Directory.Exists(userPrograms)) yield return userPrograms;

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (Directory.Exists(programFiles)) yield return programFiles;

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (Directory.Exists(programFilesX86)) yield return programFilesX86;
    }

    /// <summary>除系统盘外的固定磁盘根目录。</summary>
    private static IEnumerable<string> OtherDriveRoots()
    {
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (Exception ex)
        {
            Logger.Info($"枚举磁盘失败：{ex.Message}");
            return Array.Empty<string>();
        }

        var systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        return drives
            .Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady)
            .Select(drive => drive.RootDirectory.FullName)
            .Where(root => !string.Equals(root, systemRoot, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>在安装目录内（含一层子目录）查找主程序。</summary>
    private static string? FindExe(string directory, IEnumerable<string> names)
    {
        foreach (var exeName in names)
        {
            var direct = Path.Combine(directory, exeName);
            if (File.Exists(direct)) return direct;
        }

        try
        {
            foreach (var exeName in names)
            {
                var files = Directory.GetFiles(directory, exeName, SearchOption.AllDirectories);
                if (files.Length > 0) return files.OrderBy(path => path.Length).First();
            }
        }
        catch (Exception ex)
        {
            Logger.Info($"查找 {directory} 中的程序失败：{ex.Message}");
        }

        return null;
    }

    /// <summary>读取 exe 的版本号（失败返回空字符串）。</summary>
    public static string ReadVersion(string exePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            var product = (info.ProductVersion ?? "").Split('+')[0].Trim();
            var file = (info.FileVersion ?? "").Trim();
            var text = string.IsNullOrWhiteSpace(product) ? file : product;
            return string.IsNullOrWhiteSpace(text) ? "" : VersionUtil.Normalize(text);
        }
        catch
        {
            return "";
        }
    }
}
