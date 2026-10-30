using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using LXLGameToolbox.Core;
using LXLGameToolbox.UI;

namespace LXLGameToolbox.UI.Pages;

public partial class SettingsPage : UserControl
{
    private static readonly Dictionary<string, string> AccentNames = new()
    {
        ["#2E7CF6"] = "天蓝",
        ["#EC5F9C"] = "樱花粉",
        ["#20B8AE"] = "初音青",
        ["#A06CFF"] = "霓虹紫",
        ["#F2764B"] = "暖橙",
        ["#2FB98A"] = "森绿",
        ["#E5484D"] = "朱红",
        ["#E19A2B"] = "琥珀"
    };

    private bool _loading;

    public SettingsPage()
    {
        InitializeComponent();

        var config = ConfigStore.Current;
        AutoCheckToggle.IsChecked = config.CheckUpdateOnStart;
        SourceCombo.SelectedIndex = config.DownloadMode switch
        {
            "github" => 1,
            "proxy" => 2,
            _ => 0
        };

        DataPathText.Text = AppState.DataDir;
        ToolsPathText.Text = AppState.ToolsDir;

        RefreshAppearance();
    }

    // ---------- 外观 ----------

    private void RefreshAppearance()
    {
        _loading = true;
        var config = ConfigStore.Current;
        var dark = string.Equals(config.ThemeMode, "dark", StringComparison.OrdinalIgnoreCase);
        LightModeChip.IsChecked = !dark;
        DarkModeChip.IsChecked = dark;

        BuildPresets(config);
        BuildAccents(config);
        BuildBackgrounds(config);
        _loading = false;
    }

    private void BuildPresets(AppConfig config)
    {
        PresetPanel.Children.Clear();
        foreach (var preset in ThemeManager.Presets)
        {
            var stack = new StackPanel { Width = 132 };
            stack.Children.Add(CreateSwatch(preset.BackgroundId, config.BackgroundPath, preset.AccentHex, 132, 72));
            stack.Children.Add(new TextBlock
            {
                Text = preset.Name,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 7, 0, 0)
            });
            stack.Children.Add(new TextBlock
            {
                Text = preset.Description,
                FontSize = 10,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                Margin = new Thickness(0, 3, 0, 0)
            });

            var radio = new RadioButton
            {
                Content = stack,
                Tag = preset.Id,
                GroupName = "ThemePreset",
                Style = (Style)FindResource("ThemeBackgroundRadio"),
                Margin = new Thickness(0, 0, 10, 10),
                IsChecked = preset.Id == config.ThemePreset,
                ToolTip = preset.Description
            };
            AutomationProperties.SetName(radio, preset.Name);
            radio.Checked += Preset_Checked;
            PresetPanel.Children.Add(radio);
        }
    }

    private void BuildAccents(AppConfig config)
    {
        AccentPanel.Children.Clear();
        foreach (var hex in ThemeManager.AccentOptions)
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(new Ellipse
            {
                Width = 15,
                Height = 15,
                Fill = new SolidColorBrush(ParseColor(hex)),
                VerticalAlignment = VerticalAlignment.Center
            });
            stack.Children.Add(new TextBlock
            {
                Text = AccentNames.TryGetValue(hex, out var name) ? name : hex,
                FontSize = 11,
                Margin = new Thickness(7, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            var radio = new RadioButton
            {
                Content = stack,
                Tag = hex,
                GroupName = "ThemeAccent",
                Style = (Style)FindResource("ThemeOptionRadio"),
                IsChecked = string.Equals(hex, config.AccentHex, StringComparison.OrdinalIgnoreCase)
            };
            AutomationProperties.SetName(radio, stack.Children.OfType<TextBlock>().First().Text);
            radio.Checked += Accent_Checked;
            AccentPanel.Children.Add(radio);
        }
    }

    private void BuildBackgrounds(AppConfig config)
    {
        BackgroundPanel.Children.Clear();
        AddBackgroundOption("none", "无背景", "", config);

        foreach (var wallpaper in ThemeManager.Wallpapers)
            AddBackgroundOption(wallpaper.Id, wallpaper.Name, "", config);

        if (!string.IsNullOrWhiteSpace(config.BackgroundPath) && File.Exists(config.BackgroundPath))
            AddBackgroundOption("custom", "自定义图片", config.BackgroundPath, config);
    }

    private void AddBackgroundOption(string id, string name, string customPath, AppConfig config)
    {
        var stack = new StackPanel { Width = 116 };
        stack.Children.Add(CreateSwatch(id, customPath, config.AccentHex, 116, 64));
        stack.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 11,
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var radio = new RadioButton
        {
            Content = stack,
            Tag = id,
            GroupName = "ThemeBackground",
            Style = (Style)FindResource("ThemeBackgroundRadio"),
            Margin = new Thickness(0, 0, 10, 10),
            IsChecked = string.Equals(id, config.BackgroundId, StringComparison.OrdinalIgnoreCase),
            ToolTip = name
        };
        AutomationProperties.SetName(radio, name);
        radio.Checked += Background_Checked;
        BackgroundPanel.Children.Add(radio);
    }

    private static Border CreateSwatch(string backgroundId, string customPath, string accentHex,
        double width, double height)
    {
        var border = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true
        };

        var image = ThemeManager.LoadThumbnail(backgroundId, customPath, (int)width * 2);
        if (image != null)
        {
            border.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
            return border;
        }

        var accent = ParseColor(accentHex);
        border.Background = new LinearGradientBrush(accent, Darken(accent, 0.35), 45);
        return border;
    }

    private void Preset_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not RadioButton radio) return;
        var preset = ThemeManager.FindPreset(radio.Tag as string ?? "");
        if (preset == null) return;

        var config = ConfigStore.Current;
        config.ThemePreset = preset.Id;
        config.ThemeMode = preset.Dark ? "dark" : "light";
        config.AccentHex = preset.AccentHex;
        config.BackgroundId = preset.BackgroundId;
        ApplyAndRefresh();
        AppState.SetStatus($"已切换到「{preset.Name}」主题");
    }

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not RadioButton radio) return;
        var mode = radio.Tag as string == "dark" ? "dark" : "light";
        var config = ConfigStore.Current;
        if (string.Equals(config.ThemeMode, mode, StringComparison.OrdinalIgnoreCase)) return;

        config.ThemeMode = mode;
        config.ThemePreset = "custom";
        ApplyAndRefresh();
        AppState.SetStatus(mode == "dark" ? "已切换到深色模式" : "已切换到浅色模式");
    }

    private void Accent_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not RadioButton radio) return;
        var hex = radio.Tag as string ?? "";
        var config = ConfigStore.Current;
        if (string.Equals(config.AccentHex, hex, StringComparison.OrdinalIgnoreCase)) return;

        config.AccentHex = hex;
        config.ThemePreset = "custom";
        ApplyAndRefresh();
        AppState.SetStatus("已更换主题色");
    }

    private void Background_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not RadioButton radio) return;
        var id = radio.Tag as string ?? "none";
        var config = ConfigStore.Current;
        if (string.Equals(config.BackgroundId, id, StringComparison.OrdinalIgnoreCase)) return;

        config.BackgroundId = id;
        config.ThemePreset = "custom";
        ApplyAndRefresh();
        AppState.SetStatus(id == "none" ? "已关闭背景图片" : "已更换背景图片");
    }

    private void PickBackground_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择背景图片",
            Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        var config = ConfigStore.Current;
        config.BackgroundPath = dialog.FileName;
        config.BackgroundId = "custom";
        config.ThemePreset = "custom";
        ApplyAndRefresh();
        AppState.SetStatus("已设置自定义背景图片");
    }

    private void ApplyAndRefresh()
    {
        ConfigStore.Save();
        ThemeManager.ApplyAll();
        Dispatcher.BeginInvoke(new Action(RefreshAppearance), DispatcherPriority.Background);
    }

    // ---------- 常规 ----------

    private void AutoCheckToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (ConfigStore.Current.CheckUpdateOnStart == (AutoCheckToggle.IsChecked == true)) return;
        ConfigStore.Current.CheckUpdateOnStart = AutoCheckToggle.IsChecked == true;
        ConfigStore.Save();
        AppState.SetStatus(AutoCheckToggle.IsChecked == true ? "已开启启动时检查更新" : "已关闭启动时检查更新");
    }

    private void SourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var mode = SourceCombo.SelectedIndex switch
        {
            1 => "github",
            2 => "proxy",
            _ => "auto"
        };
        if (ConfigStore.Current.DownloadMode == mode) return;
        ConfigStore.Current.DownloadMode = mode;
        ConfigStore.Save();
    }

    private void CreateShortcut_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                throw new InvalidOperationException("无法确定工具箱主程序位置。");

            var linkPath = ShortcutHelper.CreateDesktopShortcut(AppState.AppName, exePath,
                "LXL 游戏工具箱 · 快速启动");
            AppState.SetStatus("已在桌面创建快捷方式");
            MessageBox.Show(Window.GetWindow(this), $"桌面快捷方式已创建：\n{linkPath}", "创建成功",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Logger.Error("创建桌面快捷方式失败：" + ex.Message);
            MessageBox.Show(Window.GetWindow(this), "创建桌面快捷方式失败：" + ex.Message, "设置",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenData_Click(object sender, RoutedEventArgs e) => OpenFolder(AppState.DataDir);

    private void OpenTools_Click(object sender, RoutedEventArgs e) => OpenFolder(AppState.ToolsDir);

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppState.DownloadDir);
            foreach (var file in Directory.GetFiles(AppState.DownloadDir)) File.Delete(file);
            GitHubClient.ClearCache();
            Logger.Info("已清空下载缓存与版本缓存");
            AppState.SetStatus("已清空下载缓存与版本缓存");
        }
        catch (Exception ex)
        {
            Logger.Error("清空缓存失败：" + ex.Message);
            MessageBox.Show(Window.GetWindow(this), "清空缓存失败：" + ex.Message, "设置",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error("打开目录失败：" + ex.Message);
        }
    }

    // ---------- 颜色工具 ----------

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Color.FromRgb(0x2E, 0x7C, 0xF6);
        }
    }

    private static Color Darken(Color color, double amount)
    {
        var factor = 1 - amount;
        return Color.FromArgb(color.A,
            (byte)Math.Clamp(color.R * factor, 0, 255),
            (byte)Math.Clamp(color.G * factor, 0, 255),
            (byte)Math.Clamp(color.B * factor, 0, 255));
    }
}
