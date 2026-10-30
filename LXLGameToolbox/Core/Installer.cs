using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace LXLGameToolbox.Core;

/// <summary>安装记录（保存在 tools\{id}\install.json）。</summary>
public sealed class InstallRecord
{
    public string Version { get; set; } = "";
    public string ExePath { get; set; } = "";
    public string InstalledAt { get; set; } = "";
    public string AssetName { get; set; } = "";
}

/// <summary>下载 Release zip 包并解压到 tools\{id}\ 目录。</summary>
public static class Installer
{
    public static string ToolDir(ToolDefinition tool) => Path.Combine(AppState.ToolsDir, tool.Id);

    public static string RecordPath(ToolDefinition tool) => Path.Combine(ToolDir(tool), "install.json");

    public static InstallRecord? ReadRecord(ToolDefinition tool)
    {
        try
        {
            var path = RecordPath(tool);
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<InstallRecord>(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Logger.Info($"读取 {tool.Name} 安装记录失败：{ex.Message}");
            return null;
        }
    }

    public static async Task<InstallRecord> InstallAsync(ToolDefinition tool, ReleaseInfo release,
        IProgress<DownloadInfo>? progress = null, CancellationToken token = default)
    {
        if (!release.HasAsset)
            throw new InvalidOperationException("该版本没有提供 zip 安装包，请前往 Release 页面手动下载。");

        var directory = ToolDir(tool);
        Directory.CreateDirectory(directory);

        var versionText = VersionUtil.Normalize(release.Tag);
        var zipPath = Path.Combine(AppState.DownloadDir, $"{tool.Id}-{versionText}.zip");

        AppState.SetStatus($"正在下载 {tool.Name} {release.Tag} ...");
        await DownloadAsync(tool, release, zipPath, progress, token);

        AppState.SetStatus($"正在解压 {tool.Name} ...");
        Logger.Info($"解压 {zipPath} → {directory}");
        ZipFile.ExtractToDirectory(zipPath, directory, true);

        var exePath = FindExe(directory, tool.ExeName)
                      ?? throw new FileNotFoundException($"安装包内未找到 {tool.ExeName}，请检查 Release 资源。");

        var record = new InstallRecord
        {
            Version = versionText,
            ExePath = exePath,
            InstalledAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            AssetName = release.AssetName
        };
        File.WriteAllText(RecordPath(tool),
            JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));

        Logger.Info($"{tool.Name} 安装完成：{exePath}");
        AppState.SetStatus($"{tool.Name} {versionText} 安装完成");
        LocalScanner.Invalidate();
        return record;
    }

    // ---------- 第三方软件：下载官方安装包并启动安装 ----------

    public static string ThirdPartyInstallerPath(ToolDefinition tool) =>
        Path.Combine(AppState.DownloadDir, $"{tool.Id}-setup.exe");

    /// <summary>缓存安装包的有效期，超过后重新下载，确保拿到的是官方最新版。</summary>
    public static readonly TimeSpan InstallerCacheAge = TimeSpan.FromDays(7);

    /// <summary>本地是否已有一份还算新鲜的官方安装包。</summary>
    public static bool HasFreshInstaller(ToolDefinition tool)
    {
        var path = ThirdPartyInstallerPath(tool);
        if (!Downloader.HasCachedInstaller(path)) return false;
        return DateTime.Now - File.GetLastWriteTime(path) <= InstallerCacheAge;
    }

    /// <summary>读取本地官方安装包内记录的版本号（读不到返回空字符串）。</summary>
    public static string ReadInstallerVersion(ToolDefinition tool) =>
        ReadInstallerVersion(ThirdPartyInstallerPath(tool));

    public static string ReadInstallerVersion(string path)
    {
        try
        {
            if (!File.Exists(path)) return "";
            var info = FileVersionInfo.GetVersionInfo(path);
            var raw = string.IsNullOrWhiteSpace(info.ProductVersion) ? info.FileVersion : info.ProductVersion;
            return VersionUtil.Normalize(raw);
        }
        catch (Exception ex)
        {
            Logger.Info($"读取安装包版本失败：{ex.Message}");
            return "";
        }
    }

    /// <summary>下载（或复用仍有效的缓存）第三方安装包，返回本地 exe 路径。</summary>
    public static async Task<string> EnsureInstallerAsync(ToolDefinition tool,
        IProgress<DownloadInfo>? progress = null, CancellationToken token = default)
    {
        if (!tool.CanDirectInstall)
            throw new InvalidOperationException("该软件未提供安装包直链，请前往官网下载。");

        var path = ThirdPartyInstallerPath(tool);
        if (HasFreshInstaller(tool))
        {
            Logger.Info($"{tool.Name} 安装包缓存有效，直接安装：{path}（版本 {ReadInstallerVersion(path)}）");
            progress?.Report(new DownloadInfo(new FileInfo(path).Length, new FileInfo(path).Length, 0, TimeSpan.Zero));
            return path;
        }

        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                Logger.Info($"{tool.Name} 缓存安装包已过期，重新下载最新版：{path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"删除过期安装包失败：{ex.Message}");
            }
        }

        AppState.SetStatus($"正在下载 {tool.Name} 安装包 ...");
        await Downloader.DownloadInstallerAsync(tool.DirectInstallUrl, path, progress, token,
            tool.DownloadReferer);

        var version = ReadInstallerVersion(path);
        Logger.Info($"{tool.Name} 安装包下载完成，版本：{(string.IsNullOrEmpty(version) ? "未知" : version)}");
        AppState.SetStatus($"{tool.Name} 安装包下载完成，正在启动安装程序");
        return path;
    }

    /// <summary>启动下载好的安装程序，返回进程（用户取消 UAC 时为 null）。</summary>
    public static Process? RunInstaller(string installerPath)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo(installerPath) { UseShellExecute = true });
            Logger.Info($"启动安装程序：{installerPath}");
            return process;
        }
        catch (Exception ex)
        {
            Logger.Error($"启动安装程序失败：{ex.Message}");
            throw;
        }
    }

    private static async Task DownloadAsync(ToolDefinition tool, ReleaseInfo release, string zipPath,
        IProgress<DownloadInfo>? progress, CancellationToken token)
    {
        if (File.Exists(zipPath) && Downloader.LooksLikeZip(zipPath) &&
            release.AssetSize > 0 && new FileInfo(zipPath).Length == release.AssetSize)
        {
            Logger.Info($"{tool.Name} 已有完整安装包缓存，跳过下载：{zipPath}");
            progress?.Report(new DownloadInfo(release.AssetSize, release.AssetSize, 0, TimeSpan.Zero));
            return;
        }

        await Downloader.DownloadAsync(release.AssetUrl, zipPath, progress, token);
    }

    private static string? FindExe(string root, string exeName)
    {
        var direct = Path.Combine(root, exeName);
        if (File.Exists(direct)) return direct;

        var files = Directory.GetFiles(root, exeName, SearchOption.AllDirectories);
        return files.OrderBy(path => path.Length).FirstOrDefault();
    }
}
