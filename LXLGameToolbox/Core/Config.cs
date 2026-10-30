using System.IO;
using System.Text.Json;

namespace LXLGameToolbox.Core;

public sealed class AppConfig
{
    /// <summary>启动时自动检查工具版本。</summary>
    public bool CheckUpdateOnStart { get; set; } = true;

    /// <summary>下载源：auto（直连 + 镜像）、github（仅直连）、proxy（仅镜像）。</summary>
    public string DownloadMode { get; set; } = "auto";

    /// <summary>手动指定的本地程序路径（工具 Id → exe 路径）。</summary>
    public Dictionary<string, string> ManualPaths { get; set; } = new();

    /// <summary>上次启动时记录的版本号，用于版本变化后弹出更新说明。</summary>
    public string LastSeenVersion { get; set; } = "";

    // ---------- 外观 ----------

    /// <summary>当前主题预设 Id（手动调整后为 custom）。</summary>
    public string ThemePreset { get; set; } = "default";

    /// <summary>外观模式：light / dark。</summary>
    public string ThemeMode { get; set; } = "light";

    /// <summary>主题色（十六进制）。</summary>
    public string AccentHex { get; set; } = "#2E7CF6";

    /// <summary>背景 Id：none、内置壁纸 Id，或 custom。</summary>
    public string BackgroundId { get; set; } = "none";

    /// <summary>自定义背景图片的本地路径。</summary>
    public string BackgroundPath { get; set; } = "";
}

public static class ConfigStore
{
    private static AppConfig? _current;

    public static AppConfig Current => _current ??= Load();

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(AppState.ConfigPath))
            {
                var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(AppState.ConfigPath));
                if (config != null)
                {
                    config.ManualPaths ??= new Dictionary<string, string>();
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Info("读取配置失败：" + ex.Message);
        }
        return new AppConfig();
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(AppState.DataDir);
            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AppState.ConfigPath, json);
        }
        catch (Exception ex)
        {
            Logger.Info("保存配置失败：" + ex.Message);
        }
    }

    public static void SetManualPath(string toolId, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) Current.ManualPaths.Remove(toolId);
        else Current.ManualPaths[toolId] = path;
        Save();
    }
}
