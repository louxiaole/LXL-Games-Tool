using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace LXLGameToolbox.Core;

/// <summary>一次下载进度回调。</summary>
public sealed record DownloadInfo(long Received, long Total, double BytesPerSecond, TimeSpan Eta)
{
    public double Percent => Total > 0 ? Math.Min(100, Received * 100.0 / Total) : 0;

    public string SpeedText => BytesPerSecond > 0
        ? VersionUtil.FormatSize((long)BytesPerSecond) + "/s"
        : "计算中";

    public string EtaText
    {
        get
        {
            if (Total <= 0) return "未知";
            if (Eta <= TimeSpan.Zero) return "即将完成";
            return Eta.TotalHours >= 1
                ? $"{(int)Eta.TotalHours} 小时 {Eta.Minutes} 分"
                : $"{Eta.Minutes} 分 {Eta.Seconds:00} 秒";
        }
    }
}

/// <summary>带多镜像回退的下载器。</summary>
public static class Downloader
{
    private static readonly string[] Mirrors =
    {
        "https://ghfast.top/",
        "https://gh-proxy.com/",
        "https://ghproxy.net/"
    };

    private static readonly HttpClient Http = CreateHttp();

    private static HttpClient CreateHttp()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(8),
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(15) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        return client;
    }

    /// <summary>按当前配置生成下载源顺序：直连 GitHub → 各加速镜像。</summary>
    public static IReadOnlyList<string> BuildSources(string url)
    {
        var mode = ConfigStore.Current.DownloadMode;
        if (mode == "github") return new[] { url };
        if (mode == "proxy") return Mirrors.Select(mirror => mirror + url).ToList();
        return new[] { url }.Concat(Mirrors.Select(mirror => mirror + url)).ToList();
    }

    /// <summary>依次尝试各下载源，成功后返回本地文件路径；文件必须是有效 zip。</summary>
    public static async Task<string> DownloadAsync(string url, string targetPath,
        IProgress<DownloadInfo>? progress = null, CancellationToken token = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var errors = new List<string>();
        var sources = BuildSources(url);

        for (var i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            try
            {
                Logger.Info($"下载 {targetPath} ← {source}");
                var tempPath = targetPath + ".part";
                if (File.Exists(tempPath)) File.Delete(tempPath);

                using var response = await Http.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, token);
                if (!response.IsSuccessStatusCode)
                {
                    errors.Add($"{source} 返回 {(int)response.StatusCode}");
                    continue;
                }

                await StreamToFileAsync(response, tempPath, progress, token);

                if (!LooksLikeZip(tempPath))
                {
                    File.Delete(tempPath);
                    errors.Add($"{source} 返回的不是有效压缩包");
                    continue;
                }

                File.Move(tempPath, targetPath, true);
                if (File.Exists(targetPath + ".part")) File.Delete(targetPath + ".part");
                Logger.Info($"下载完成：{targetPath}（{VersionUtil.FormatSize(new FileInfo(targetPath).Length)}）");
                return targetPath;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"{source}：{ex.Message}");
                Logger.Info($"下载源失败（{source}）：{ex.Message}");
                if (i < sources.Count - 1) AppState.SetStatus("下载失败，正在切换镜像源重试 ...");
            }
        }

        throw new IOException("全部下载源均失败：" + string.Join(" | ", errors));
    }

    /// <summary>下载第三方安装包：校验 MZ 头；若地址返回的是下载页，则解析其中的 exe 直链。</summary>
    public static async Task<string> DownloadInstallerAsync(string url, string targetPath,
        IProgress<DownloadInfo>? progress = null, CancellationToken token = default, string referer = "")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var current = url;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var tempPath = targetPath + ".part";
            if (File.Exists(tempPath)) File.Delete(tempPath);

            Logger.Info($"下载安装包 {targetPath} ← {current}");
            using (var request = new HttpRequestMessage(HttpMethod.Get, current))
            {
                if (!string.IsNullOrEmpty(referer))
                    request.Headers.TryAddWithoutValidation("Referer", referer);

                using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                if (!response.IsSuccessStatusCode)
                    throw new IOException($"下载失败：{current} 返回 {(int)response.StatusCode}");

                await StreamToFileAsync(response, tempPath, progress, token);
            }

            if (LooksLikeExe(tempPath))
            {
                File.Move(tempPath, targetPath, true);
                Logger.Info($"安装包下载完成：{targetPath}（{VersionUtil.FormatSize(new FileInfo(targetPath).Length)}）");
                return targetPath;
            }

            // 返回的是下载页面：解析页面里的 exe 直链后重试一次
            var resolved = TryResolveExeLink(tempPath, current);
            File.Delete(tempPath);
            if (resolved == null)
                throw new IOException("该地址没有提供可用的安装包，请前往官网手动下载。");

            Logger.Info($"已解析安装包直链：{resolved}");
            AppState.SetStatus("正在解析官方安装包地址 ...");
            current = resolved;
        }

        throw new IOException("安装包地址解析失败，请前往官网手动下载。");
    }

    /// <summary>已存在且完整的安装包可直接复用。</summary>
    public static bool HasCachedInstaller(string path) =>
        File.Exists(path) && new FileInfo(path).Length > 1024 * 1024 && LooksLikeExe(path);

    /// <summary>校验文件头是否为 Windows 可执行文件（MZ）。</summary>
    public static bool LooksLikeExe(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length < 4) return false;
            var head = new byte[2];
            _ = stream.Read(head, 0, 2);
            return head[0] == (byte)'M' && head[1] == (byte)'Z';
        }
        catch
        {
            return false;
        }
    }

    /// <summary>从下载页面 HTML 中找出第一个 exe 直链。</summary>
    private static string? TryResolveExeLink(string htmlPath, string baseUrl)
    {
        try
        {
            if (new FileInfo(htmlPath).Length > 8 * 1024 * 1024) return null;
            var html = File.ReadAllText(htmlPath);
            var matches = Regex.Matches(html, @"href\s*=\s*[""']([^""']+\.exe)[""']", RegexOptions.IgnoreCase);
            if (matches.Count == 0) return null;

            // 优先取链接附近带 Download 字样的那个，避免抓到页面上的广告链接
            string? link = null;
            foreach (Match match in matches)
            {
                var tail = html.Substring(match.Index, Math.Min(300, html.Length - match.Index));
                link ??= match.Groups[1].Value;
                if (!tail.Contains("download", StringComparison.OrdinalIgnoreCase)) continue;
                link = match.Groups[1].Value;
                break;
            }

            return link != null && Uri.TryCreate(new Uri(baseUrl), link, out var uri) ? uri.ToString() : null;
        }
        catch (Exception ex)
        {
            Logger.Info($"解析下载页失败：{ex.Message}");
            return null;
        }
    }

    private static async Task StreamToFileAsync(HttpResponseMessage response, string tempPath,
        IProgress<DownloadInfo>? progress, CancellationToken token)
    {
        var total = response.Content.Headers.ContentLength ?? 0;
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = File.Create(tempPath);

        var buffer = new byte[81920];
        long received = 0;
        var watch = Stopwatch.StartNew();
        var lastReport = TimeSpan.Zero;
        int read;

        while ((read = await input.ReadAsync(buffer, token)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), token);
            received += read;

            if (watch.Elapsed - lastReport < TimeSpan.FromMilliseconds(120)) continue;

            lastReport = watch.Elapsed;
            var seconds = Math.Max(0.001, watch.Elapsed.TotalSeconds);
            var speed = received / seconds;
            var eta = total > 0 && speed > 1
                ? TimeSpan.FromSeconds(Math.Max(0, (total - received) / speed))
                : TimeSpan.Zero;
            progress?.Report(new DownloadInfo(received, total, speed, eta));
        }
    }

    /// <summary>校验文件头是否为 zip（PK）。</summary>
    public static bool LooksLikeZip(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length < 4) return false;
            var head = new byte[2];
            _ = stream.Read(head, 0, 2);
            return head[0] == 0x50 && head[1] == 0x4B;
        }
        catch
        {
            return false;
        }
    }
}
