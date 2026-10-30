using System.Windows.Media.Imaging;
using LXLGameToolbox.Core;

namespace LXLGameToolbox.UI;

/// <summary>加载工具图标：使用 Assets\icons 下随程序发布的官方图标，缺失时回退文字字形。</summary>
public static class ToolIcon
{
    private static readonly Dictionary<string, BitmapSource?> Cache = new();

    public static BitmapSource? Load(ToolDefinition tool)
    {
        if (string.IsNullOrEmpty(tool.IconAsset)) return null;
        if (Cache.TryGetValue(tool.IconAsset, out var cached)) return cached;

        BitmapSource? image = null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri($"pack://application:,,,/Assets/icons/{tool.IconAsset}");
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 96;
            bitmap.EndInit();
            bitmap.Freeze();
            image = bitmap;
        }
        catch (Exception ex)
        {
            Logger.Info($"加载 {tool.Name} 图标失败：{ex.Message}");
        }

        Cache[tool.IconAsset] = image;
        return image;
    }
}
