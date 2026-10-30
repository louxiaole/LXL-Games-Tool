using System.Windows;
using System.Windows.Input;
using LXLGameToolbox.Core;

namespace LXLGameToolbox.UI.Windows;

/// <summary>版本更新后首次启动时，展示当前版本的更新内容。</summary>
public partial class ChangelogWindow : Window
{
    /// <summary>用户是否选择「查看完整更新日志」。</summary>
    public bool OpenLogRequested { get; private set; }

    public ChangelogWindow(string version, string body)
    {
        InitializeComponent();
        Title = $"{AppState.AppName} {version} 更新内容";
        VersionText.Text = "v" + VersionUtil.Normalize(version);
        BodyText.Text = string.IsNullOrWhiteSpace(body) ? "本次版本没有填写更新说明。" : body.Trim();
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        OpenLogRequested = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
