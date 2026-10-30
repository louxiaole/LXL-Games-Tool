using System.IO;

namespace LXLGameToolbox.Core;

/// <summary>全局路径与运行状态。</summary>
public static class AppState
{
    public const string AppName = "LXL 游戏工具箱";
    public const string VersionText = "1.1.0";

    /// <summary>程序所在目录（发布后的根目录）。</summary>
    public static string BaseDir { get; } = AppContext.BaseDirectory;

    /// <summary>配置与日志目录，始终放在程序目录下，不写 C 盘。</summary>
    public static string DataDir => Path.Combine(BaseDir, "Data");
    public static string ToolsDir => Path.Combine(BaseDir, "tools");
    public static string DownloadDir => Path.Combine(DataDir, "downloads");
    public static string LogDir => Path.Combine(DataDir, "logs");
    public static string ConfigPath => Path.Combine(DataDir, "config.json");

    /// <summary>第三方软件建议的安装目录（与工具箱同盘，避免占用 C 盘）。</summary>
    public static string AppsDir => Path.Combine(BaseDir, "apps");

    /// <summary>工具箱是否位于系统盘（C 盘）。</summary>
    public static bool IsOnSystemDrive
    {
        get
        {
            try
            {
                return string.Equals(Path.GetPathRoot(BaseDir)?.TrimEnd('\\'),
                    Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))?.TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>路径是否位于工具箱管理的 tools / apps 目录内。</summary>
    public static bool IsManagedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        return IsUnderRoot(path, ToolsDir) || IsUnderRoot(path, AppsDir);
    }

    /// <summary>判断子路径是否在指定根目录下（避免 "C:\apps" 误匹配 "C:\apps2"）。</summary>
    public static bool IsUnderRoot(string path, string root)
    {
        try
        {
            var full = Path.GetFullPath(path).TrimEnd('\\', '/');
            var rootFull = Path.GetFullPath(root).TrimEnd('\\', '/');
            return full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>状态栏文本变化通知。</summary>
    public static event Action<string>? StatusChanged;

    public static void SetStatus(string text) => StatusChanged?.Invoke(text);

    public static void EnsureFolders()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(ToolsDir);
        Directory.CreateDirectory(DownloadDir);
        Directory.CreateDirectory(LogDir);
        Directory.CreateDirectory(AppsDir);
    }
}
