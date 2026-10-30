using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using LXLGameToolbox.Core;

namespace LXLGameToolbox.UI;

public sealed class WallpaperOption
{
    public string Id { get; init; } = "none";
    public string Name { get; init; } = "";
}

public sealed class ThemePreset
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string AccentHex { get; init; } = "#2E7CF6";
    public bool Dark { get; init; }
    public string BackgroundId { get; init; } = "none";
}

/// <summary>
/// 运行时换肤：直接替换 Theme.xaml 中的画刷资源（页面里都是 DynamicResource，会立即重绘）。
/// 支持浅色 / 深色、主题色、内置动漫壁纸与自定义背景。
/// </summary>
public static class ThemeManager
{
    private static readonly Dictionary<string, Color> LightPalette = new()
    {
        ["WindowBackgroundBrush"] = Color.FromRgb(0xF4, 0xF6, 0xF9),
        ["SurfaceBrush"] = Colors.White,
        ["SurfaceGlassBrush"] = Colors.White,
        ["MaskBrush"] = Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF),
        ["SurfaceAltBrush"] = Color.FromRgb(0xF9, 0xFA, 0xFC),
        ["SidebarBrush"] = Color.FromRgb(0xFB, 0xFB, 0xFC),
        ["InputBackgroundBrush"] = Color.FromRgb(0xF6, 0xF7, 0xF9),
        ["InputBorderBrush"] = Color.FromRgb(0xE1, 0xE5, 0xEB),
        ["BorderBrush"] = Color.FromRgb(0xE7, 0xEA, 0xF0),
        ["BorderStrongBrush"] = Color.FromRgb(0xD8, 0xDD, 0xE5),
        ["HoverBrush"] = Color.FromRgb(0xF0, 0xF2, 0xF6),
        ["PressedBrush"] = Color.FromRgb(0xE5, 0xE9, 0xEF),
        ["TextPrimaryBrush"] = Color.FromRgb(0x1C, 0x21, 0x2B),
        ["TextSecondaryBrush"] = Color.FromRgb(0x6B, 0x72, 0x80),
        ["TextTertiaryBrush"] = Color.FromRgb(0x9A, 0xA1, 0xAC),
        ["SuccessBrush"] = Color.FromRgb(0x23, 0xA5, 0x5A),
        ["SuccessLightBrush"] = Color.FromArgb(0x1A, 0x23, 0xA5, 0x5A),
        ["WarningBrush"] = Color.FromRgb(0xE1, 0x9A, 0x2B),
        ["WarningLightBrush"] = Color.FromArgb(0x1A, 0xE1, 0x9A, 0x2B),
        ["DangerBrush"] = Color.FromRgb(0xE5, 0x48, 0x4D),
        ["DangerLightBrush"] = Color.FromArgb(0x1A, 0xE5, 0x48, 0x4D),
        ["ScrollThumbBrush"] = Color.FromRgb(0xD5, 0xDA, 0xE1),
        ["ScrollThumbHoverBrush"] = Color.FromRgb(0xB8, 0xBF, 0xC9),
        ["ScrollThumbPressedBrush"] = Color.FromRgb(0xA5, 0xAD, 0xB9)
    };

    private static readonly Dictionary<string, Color> DarkPalette = new()
    {
        ["WindowBackgroundBrush"] = Color.FromRgb(0x12, 0x14, 0x1A),
        ["SurfaceBrush"] = Color.FromRgb(0x1B, 0x1E, 0x25),
        ["SurfaceGlassBrush"] = Color.FromRgb(0x1E, 0x21, 0x29),
        ["MaskBrush"] = Color.FromArgb(0x66, 0x12, 0x14, 0x1A),
        ["SurfaceAltBrush"] = Color.FromRgb(0x20, 0x23, 0x2B),
        ["SidebarBrush"] = Color.FromRgb(0x17, 0x1A, 0x20),
        ["InputBackgroundBrush"] = Color.FromRgb(0x23, 0x26, 0x2E),
        ["InputBorderBrush"] = Color.FromRgb(0x33, 0x37, 0x3F),
        ["BorderBrush"] = Color.FromRgb(0x2A, 0x2E, 0x36),
        ["BorderStrongBrush"] = Color.FromRgb(0x3A, 0x3F, 0x49),
        ["HoverBrush"] = Color.FromRgb(0x28, 0x2C, 0x34),
        ["PressedBrush"] = Color.FromRgb(0x32, 0x37, 0x42),
        ["TextPrimaryBrush"] = Color.FromRgb(0xEC, 0xEF, 0xF4),
        ["TextSecondaryBrush"] = Color.FromRgb(0xA6, 0xAD, 0xBB),
        ["TextTertiaryBrush"] = Color.FromRgb(0x72, 0x7A, 0x88),
        ["SuccessBrush"] = Color.FromRgb(0x34, 0xC7, 0x7B),
        ["SuccessLightBrush"] = Color.FromArgb(0x1F, 0x34, 0xC7, 0x7B),
        ["WarningBrush"] = Color.FromRgb(0xF0, 0xB4, 0x4C),
        ["WarningLightBrush"] = Color.FromArgb(0x1F, 0xF0, 0xB4, 0x4C),
        ["DangerBrush"] = Color.FromRgb(0xFF, 0x6B, 0x70),
        ["DangerLightBrush"] = Color.FromArgb(0x1F, 0xFF, 0x6B, 0x70),
        ["ScrollThumbBrush"] = Color.FromRgb(0x3A, 0x3F, 0x49),
        ["ScrollThumbHoverBrush"] = Color.FromRgb(0x4A, 0x50, 0x5C),
        ["ScrollThumbPressedBrush"] = Color.FromRgb(0x5A, 0x61, 0x70)
    };

    public static readonly IReadOnlyList<WallpaperOption> Wallpapers = new List<WallpaperOption>
    {
        new() { Id = "bg-sakura", Name = "樱花校园" },
        new() { Id = "bg-miku", Name = "初音夜景" },
        new() { Id = "bg-night", Name = "雨夜霓虹" },
        new() { Id = "bg-galaxy", Name = "星河山丘" },
        new() { Id = "bg-sunset", Name = "黄昏海边" },
        new() { Id = "bg-forest", Name = "森野晨光" },
        new() { Id = "bg-snow", Name = "雪国村落" },
        new() { Id = "bg-ocean", Name = "深海蓝光" }
    };

    public static readonly IReadOnlyList<ThemePreset> Presets = new List<ThemePreset>
    {
        new() { Id = "default", Name = "清透蓝", Description = "浅色 · 无背景", AccentHex = "#2E7CF6" },
        new() { Id = "sakura", Name = "樱花少女", Description = "浅色 · 樱花", AccentHex = "#EC5F9C", BackgroundId = "bg-sakura" },
        new() { Id = "miku", Name = "初音未来", Description = "浅色 · 初音", AccentHex = "#20B8AE", BackgroundId = "bg-miku" },
        new() { Id = "galaxy", Name = "星河幻想", Description = "深色 · 星河", AccentHex = "#A06CFF", Dark = true, BackgroundId = "bg-galaxy" },
        new() { Id = "night", Name = "雨夜霓虹", Description = "深色 · 雨夜", AccentHex = "#6C8CFF", Dark = true, BackgroundId = "bg-night" },
        new() { Id = "sunset", Name = "黄昏物语", Description = "浅色 · 黄昏", AccentHex = "#F2764B", BackgroundId = "bg-sunset" },
        new() { Id = "forest", Name = "森野精灵", Description = "浅色 · 森林", AccentHex = "#2FB98A", BackgroundId = "bg-forest" },
        new() { Id = "snow", Name = "雪国冬日", Description = "浅色 · 雪国", AccentHex = "#5E97DA", BackgroundId = "bg-snow" },
        new() { Id = "ocean", Name = "深海蓝光", Description = "深色 · 深海", AccentHex = "#2E9FD8", Dark = true, BackgroundId = "bg-ocean" },
        new() { Id = "midnight", Name = "极夜黑", Description = "深色 · 无背景", AccentHex = "#3B82F6", Dark = true }
    };

    public static readonly string[] AccentOptions =
    {
        "#2E7CF6", "#EC5F9C", "#20B8AE", "#A06CFF", "#F2764B", "#2FB98A", "#E5484D", "#E19A2B"
    };

    /// <summary>换肤 / 换背景后触发，主窗口据此重新加载背景图层。</summary>
    public static event Action? Changed;

    public static bool IsDark => string.Equals(ConfigStore.Current.ThemeMode, "dark", StringComparison.OrdinalIgnoreCase);

    public static bool HasBackground(string? id) =>
        !string.IsNullOrWhiteSpace(id) && !string.Equals(id, "none", StringComparison.OrdinalIgnoreCase);

    public static ThemePreset? FindPreset(string id) => Presets.FirstOrDefault(preset => preset.Id == id);

    /// <summary>应用当前配置里的主题与配色。</summary>
    public static void ApplyAll()
    {
        var config = ConfigStore.Current;
        var dark = IsDark;
        var palette = dark ? DarkPalette : LightPalette;
        var wallpaper = HasBackground(config.BackgroundId);

        foreach (var (key, color) in palette)
            SetBrush(key, color);

        // 独立窗口（工具详情）没有背景图层，需要保持不透明底色
        SetBrush("OpaqueBackgroundBrush", palette["WindowBackgroundBrush"]);

        if (wallpaper)
        {
            // 有壁纸时让窗口与表面半透明，壁纸才能透出来
            SetBrush("WindowBackgroundBrush", Colors.Transparent);
            var glassAlpha = dark ? (byte)0xE0 : (byte)0xE4;
            SetBrush("SurfaceGlassBrush", WithAlpha(palette["SurfaceGlassBrush"], glassAlpha));
            SetBrush("SurfaceBrush", WithAlpha(palette["SurfaceBrush"], dark ? (byte)0xF2 : (byte)0xF7));
            SetBrush("SurfaceAltBrush", WithAlpha(palette["SurfaceAltBrush"], dark ? (byte)0xE8 : (byte)0xEE));
            SetBrush("SidebarBrush", WithAlpha(palette["SidebarBrush"], glassAlpha));
        }

        ApplyAccent(config.AccentHex, dark);
        Changed?.Invoke();
    }

    /// <summary>参考当前主题的遮罩色，用于压暗/提亮壁纸，保证文字可读。</summary>
    public static Brush CreateScrim()
    {
        return IsDark
            ? new SolidColorBrush(Color.FromArgb(0x66, 0x08, 0x0A, 0x0E))
            : new SolidColorBrush(Color.FromArgb(0x3D, 0xFF, 0xFF, 0xFF));
    }

    public static BitmapSource? LoadBackgroundImage(string id, string customPath)
    {
        if (!HasBackground(id)) return null;
        return string.Equals(id, "custom", StringComparison.OrdinalIgnoreCase)
            ? LoadLocalImage(customPath)
            : LoadPackImage(id!);
    }

    /// <summary>设置页缩略图（限制解码宽度，避免一次性解码全部大图）。</summary>
    public static BitmapSource? LoadThumbnail(string id, string customPath, int decodeWidth = 240)
    {
        if (!HasBackground(id)) return null;
        return string.Equals(id, "custom", StringComparison.OrdinalIgnoreCase)
            ? LoadLocalImage(customPath, decodeWidth)
            : LoadPackImage(id!, decodeWidth);
    }

    public static BitmapSource? LoadCustomImage(string path) => LoadLocalImage(path);

    private static void ApplyAccent(string hex, bool dark)
    {
        var accent = ParseColor(hex, Color.FromRgb(0x2E, 0x7C, 0xF6));
        var text = dark ? Lighten(accent, 0.22) : Darken(accent, 0.08);

        SetBrush("AccentBrush", accent);
        SetBrush("AccentDarkBrush", Darken(accent, 0.12));
        SetBrush("AccentDeepBrush", Darken(accent, 0.26));
        SetBrush("AccentHoverBrush", Lighten(accent, 0.16));
        SetBrush("AccentLightBrush", WithAlpha(accent, 0x1C));
        SetBrush("AccentLighterBrush", WithAlpha(accent, 0x0F));
        SetBrush("AccentTextBrush", text);

        var gradient = new LinearGradientBrush(Lighten(accent, 0.18), Darken(accent, 0.08), 45);
        gradient.Freeze();
        Application.Current.Resources["AccentGradientBrush"] = gradient;

        var shadow = new DropShadowEffect
        {
            BlurRadius = 20,
            ShadowDepth = 2,
            Direction = 270,
            Opacity = 0.32,
            Color = accent
        };
        shadow.Freeze();
        Application.Current.Resources["AccentShadow"] = shadow;
    }

    private static void SetBrush(string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Application.Current.Resources[key] = brush;
    }

    private static BitmapSource? LoadPackImage(string id, int decodeWidth = 0)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            if (decodeWidth > 0) image.DecodePixelWidth = decodeWidth;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri($"pack://application:,,,/Assets/backgrounds/{id}.jpg", UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            Logger.Info($"加载内置背景 {id} 失败：{ex.Message}");
            return null;
        }
    }

    private static BitmapSource? LoadLocalImage(string path, int decodeWidth = 0)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            if (decodeWidth > 0) image.DecodePixelWidth = decodeWidth;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            Logger.Info($"加载自定义背景失败：{ex.Message}");
            return null;
        }
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return fallback;
        }
    }

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    private static Color Darken(Color color, double amount)
    {
        var factor = 1 - amount;
        return Color.FromArgb(color.A,
            (byte)Math.Clamp(color.R * factor, 0, 255),
            (byte)Math.Clamp(color.G * factor, 0, 255),
            (byte)Math.Clamp(color.B * factor, 0, 255));
    }

    private static Color Lighten(Color color, double amount)
    {
        return Color.FromArgb(color.A,
            (byte)Math.Clamp(color.R + (255 - color.R) * amount, 0, 255),
            (byte)Math.Clamp(color.G + (255 - color.G) * amount, 0, 255),
            (byte)Math.Clamp(color.B + (255 - color.B) * amount, 0, 255));
    }
}
