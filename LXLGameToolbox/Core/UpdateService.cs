using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace LXLGameToolbox.Core;

/// <summary>
/// 工具箱自身的在线更新：读取 LXL-Games-Tool 的 Release，下载 zip 包，
/// 退出后由脚本替换程序文件并重新启动。
/// </summary>
public static class UpdateService
{
    public const string Owner = "louxiaole";
    public const string Repo = "LXL-Games-Tool";

    /// <summary>用于查询 Release 的虚拟工具定义（不属于工具列表）。</summary>
    public static readonly ToolDefinition Toolbox = new()
    {
        Id = "toolbox-self",
        Name = AppState.AppName,
        Description = "工具箱自身",
        Category = "工具箱",
        ZoneId = ZoneRegistry.Self,
        Owner = Owner,
        Repo = Repo,
        ExeName = "LXLGameToolbox.exe",
        Vendor = "小乐自制"
    };

    /// <summary>当前版本的内置更新说明：获取不到线上日志时作为兜底。</summary>
    public static string BuiltInNotes =>
        $"{AppState.VersionText} 更新内容\n" +
        "· 新增「更新日志」页面，可查看历史版本说明并在线更新工具箱\n" +
        "· 版本变化后首次启动会弹出本次更新内容\n" +
        "· 支持扫描其它磁盘（如 D:\\APP）中已安装的软件，旧版本会标注并提示更新\n" +
        "· 新增磁盘工具专区：分区助手、DiskGenius\n" +
        "· 优化安装提示与关于页说明";

    public static async Task<ReleaseInfo?> CheckLatestAsync(bool forceRefresh = false,
        CancellationToken token = default) =>
        await GitHubClient.GetLatestAsync(Toolbox, forceRefresh, token);

    public static async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(int count = 20,
        bool forceRefresh = false, CancellationToken token = default) =>
        await GitHubClient.GetReleasesAsync(Toolbox, count, forceRefresh, token);

    /// <summary>线上版本是否比当前版本新。</summary>
    public static bool IsNewer(ReleaseInfo? release) =>
        release != null && VersionUtil.IsNewer(release.DisplayVersion, AppState.VersionText);

    /// <summary>找到与指定版本对应的 Release（用于版本更新后的说明弹窗）。</summary>
    public static async Task<ReleaseInfo?> FindReleaseAsync(string version, CancellationToken token = default)
    {
        var releases = await GetReleasesAsync(20, false, token);
        return releases.FirstOrDefault(release =>
            string.Equals(release.DisplayVersion, VersionUtil.Normalize(version), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 下载并安装新版本：下载 zip → 解压到 Data\update → 启动替换脚本 → 关闭工具箱。
    /// </summary>
    public static async Task ApplyAsync(ReleaseInfo release, IProgress<DownloadInfo>? progress = null,
        CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(release.AssetUrl))
            throw new InvalidOperationException("该版本没有提供 zip 更新包，请前往 Releases 页面手动下载。");

        var version = release.DisplayVersion;
        var zipPath = Path.Combine(AppState.DownloadDir, $"toolbox-{version}.zip");
        var extractDir = Path.Combine(AppState.DataDir, "update", version);

        AppState.SetStatus($"正在下载工具箱 {version} ...");
        await Downloader.DownloadAsync(release.AssetUrl, zipPath, progress, token);

        if (Directory.Exists(extractDir))
        {
            try { Directory.Delete(extractDir, true); }
            catch (Exception ex) { Logger.Info($"清理旧更新目录失败：{ex.Message}"); }
        }

        Directory.CreateDirectory(extractDir);
        ZipFile.ExtractToDirectory(zipPath, extractDir, true);
        AppState.SetStatus($"工具箱 {version} 下载完成，正在准备更新 ...");
        Logger.Info($"工具箱更新包已解压：{extractDir}");

        LaunchReplacer(extractDir);
    }

    /// <summary>写一个等待本进程退出后替换文件并重新启动的脚本。</summary>
    private static void LaunchReplacer(string extractDir)
    {
        var scriptPath = Path.Combine(extractDir, "apply-update.ps1");
        var script = new StringBuilder()
            .AppendLine("$ErrorActionPreference = 'SilentlyContinue'")
            .AppendLine($"$target = '{AppState.BaseDir.TrimEnd('\\')}'")
            .AppendLine($"$source = '{extractDir}'")
            .AppendLine($"$exe = '{Path.Combine(AppState.BaseDir, "LXLGameToolbox.exe")}'")
            .AppendLine($"while (Get-Process -Id {Environment.ProcessId}) {{ Start-Sleep -Seconds 1 }}")
            .AppendLine("Start-Sleep -Seconds 1")
            .AppendLine("Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force")
            .AppendLine("Start-Sleep -Seconds 1")
            .AppendLine("Start-Process -FilePath $exe")
            .AppendLine("Remove-Item -LiteralPath $source -Recurse -Force")
            .AppendLine("Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force")
            .ToString();

        File.WriteAllText(scriptPath, script, new UTF8Encoding(false));

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Logger.Info("更新脚本已启动，工具箱即将退出并自动替换文件");
        AppState.SetStatus("正在更新工具箱，程序将自动重启 ...");
    }
}
