using System.Net.Http;
using System.Text.Json;

namespace LXLGameToolbox.Core;

/// <summary>GitHub Release 信息。</summary>
public sealed class ReleaseInfo
{
    public string Tag { get; init; } = "";
    public string ReleaseName { get; init; } = "";
    public string Body { get; init; } = "";
    public string PublishedAt { get; init; } = "";
    public string AssetName { get; init; } = "";
    public string AssetUrl { get; init; } = "";
    public long AssetSize { get; init; }
    public string PageUrl { get; init; } = "";

    public string DisplayVersion => VersionUtil.Normalize(Tag);
    public bool HasAsset => !string.IsNullOrEmpty(AssetUrl);
}

/// <summary>读取 GitHub Releases 最新版本，结果缓存 5 分钟。</summary>
public static class GitHubClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly Dictionary<string, (DateTime Time, ReleaseInfo? Info)> Cache = new();
    private static readonly Dictionary<string, (DateTime Time, IReadOnlyList<ReleaseInfo> List)> ListCache = new();
    private static readonly string[] ApiMirrors = { "https://ghfast.top/", "https://gh-proxy.com/" };

    public static TimeSpan CacheDuration { get; } = TimeSpan.FromMinutes(5);

    public static async Task<ReleaseInfo?> GetLatestAsync(ToolDefinition tool, bool forceRefresh = false,
        CancellationToken token = default)
    {
        lock (Cache)
        {
            if (!forceRefresh && Cache.TryGetValue(tool.Id, out var cached) &&
                DateTime.Now - cached.Time < CacheDuration)
            {
                return cached.Info;
            }
        }

        var api = $"https://api.github.com/repos/{tool.Owner}/{tool.Repo}/releases/latest";
        ReleaseInfo? info = null;

        foreach (var url in BuildApiSources(api))
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("User-Agent", "LXLGameToolbox");
                request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
                using var response = await Http.SendAsync(request, token);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Info($"GitHub API {url} 返回 {(int)response.StatusCode}");
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(token);
                info = Parse(json, tool);
                if (info != null) break;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Info($"读取 {tool.Name} 版本信息失败（{url}）：{ex.Message}");
            }
        }

        lock (Cache)
        {
            Cache[tool.Id] = (DateTime.Now, info);
        }

        Logger.Info(info == null
            ? $"{tool.Name} 版本信息获取失败"
            : $"{tool.Name} 最新版本 {info.Tag}（{info.AssetName}）");
        return info;
    }

    public static void ClearCache()
    {
        lock (Cache) Cache.Clear();
        lock (ListCache) ListCache.Clear();
    }

    /// <summary>读取一个仓库的多个 Release（用于更新日志列表）。</summary>
    public static async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(ToolDefinition tool, int count = 10,
        bool forceRefresh = false, CancellationToken token = default)
    {
        lock (ListCache)
        {
            if (!forceRefresh && ListCache.TryGetValue(tool.Id, out var cached) &&
                DateTime.Now - cached.Time < CacheDuration)
            {
                return cached.List;
            }
        }

        var api = $"https://api.github.com/repos/{tool.Owner}/{tool.Repo}/releases?per_page={count}";
        IReadOnlyList<ReleaseInfo> list = Array.Empty<ReleaseInfo>();

        foreach (var url in BuildApiSources(api))
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("User-Agent", "LXLGameToolbox");
                request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
                using var response = await Http.SendAsync(request, token);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Info($"GitHub 更新日志 {url} 返回 {(int)response.StatusCode}");
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(token);
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Array) continue;

                list = document.RootElement.EnumerateArray()
                    .Select(element => ParseRelease(element, tool))
                    .Where(release => release != null)
                    .Select(release => release!)
                    .ToList();
                break;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Info($"读取 {tool.Name} 更新日志失败（{url}）：{ex.Message}");
            }
        }

        lock (ListCache)
        {
            ListCache[tool.Id] = (DateTime.Now, list);
        }

        return list;
    }

    /// <summary>只读取版本号（用于第三方软件的线上版本比对，不关心安装包）。</summary>
    public static async Task<string> GetLatestTagAsync(string owner, string repo, string name,
        CancellationToken token = default)
    {
        var api = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

        foreach (var url in BuildApiSources(api))
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("User-Agent", "LXLGameToolbox");
                request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
                using var response = await Http.SendAsync(request, token);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Info($"{name} 线上版本 {url} 返回 {(int)response.StatusCode}");
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(token);
                using var document = JsonDocument.Parse(json);
                var tag = ReadString(document.RootElement, "tag_name");
                if (string.IsNullOrEmpty(tag)) continue;

                var version = VersionUtil.Normalize(tag);
                Logger.Info($"{name} 线上版本：{version}");
                return version;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Info($"读取 {name} 线上版本失败（{url}）：{ex.Message}");
            }
        }

        return "";
    }

    private static IEnumerable<string> BuildApiSources(string api)
    {
        var mode = ConfigStore.Current.DownloadMode;
        if (mode == "proxy") return ApiMirrors.Select(mirror => mirror + api);
        if (mode == "github") return new[] { api };
        return new[] { api }.Concat(ApiMirrors.Select(mirror => mirror + api));
    }

    private static ReleaseInfo? Parse(string json, ToolDefinition tool)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return ParseRelease(document.RootElement, tool);
        }
        catch (Exception ex)
        {
            Logger.Info($"{tool.Name} Release 解析失败：{ex.Message}");
            return null;
        }
    }

    private static ReleaseInfo? ParseRelease(JsonElement root, ToolDefinition tool)
    {
        try
        {
            var tag = ReadString(root, "tag_name");
            var name = ReadString(root, "name");
            var body = ReadString(root, "body");
            var published = ReadString(root, "published_at");
            var page = ReadString(root, "html_url");

            var assetName = "";
            var assetUrl = "";
            long assetSize = 0;

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                JsonElement? zip = null;
                foreach (var asset in assets.EnumerateArray())
                {
                    var fileName = ReadString(asset, "name");
                    if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                    zip = asset;
                    if (fileName.Contains("win", StringComparison.OrdinalIgnoreCase)) break;
                }

                if (zip != null)
                {
                    assetName = ReadString(zip.Value, "name");
                    assetUrl = ReadString(zip.Value, "browser_download_url");
                    if (zip.Value.TryGetProperty("size", out var size) && size.TryGetInt64(out var value))
                        assetSize = value;
                }
            }

            return new ReleaseInfo
            {
                Tag = tag,
                ReleaseName = name,
                Body = body,
                PublishedAt = FormatDate(published),
                AssetName = assetName,
                AssetUrl = assetUrl,
                AssetSize = assetSize,
                PageUrl = string.IsNullOrEmpty(page) ? tool.ReleasesUrl : page
            };
        }
        catch (Exception ex)
        {
            Logger.Info($"{tool.Name} Release 解析失败：{ex.Message}");
            return null;
        }
    }

    private static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static string FormatDate(string iso)
    {
        return DateTime.TryParse(iso, out var time)
            ? time.ToLocalTime().ToString("yyyy-MM-dd")
            : iso;
    }
}
