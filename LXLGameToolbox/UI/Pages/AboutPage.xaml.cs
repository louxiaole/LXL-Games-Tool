using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using LXLGameToolbox.Core;

namespace LXLGameToolbox.UI.Pages;

public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();

        var tools = new ObservableCollection<ToolDefinition>(ToolRegistry.Tools);
        var view = CollectionViewSource.GetDefaultView(tools);
        view.GroupDescriptions.Add(new PropertyGroupDescription("Zone.Name"));
        view.SortDescriptions.Add(new SortDescription("ZoneOrder", ListSortDirection.Ascending));
        ToolListControl.ItemsSource = view;

        StorageText.Text = AppState.IsOnSystemDrive
            ? $"工具箱目录：{AppState.BaseDir}\n" +
              "配置、日志、下载缓存都跟随程序目录存放。"
            : $"配置、日志、下载缓存与第三方软件建议安装目录都在工具箱所在盘：\n{AppState.BaseDir}\n" +
              $"第三方软件建议安装到：{AppState.AppsDir}";
    }

    private void OpenLxlpcl_Click(object sender, RoutedEventArgs e) =>
        OpenUrl("https://github.com/louxiaole/LXLPCL");

    private void OpenRust_Click(object sender, RoutedEventArgs e) =>
        OpenUrl("https://github.com/louxiaole/RustServerManager");

    private void OpenGameBuff_Click(object sender, RoutedEventArgs e) =>
        OpenUrl("https://gamebuff.cn");

    private void OpenData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppState.DataDir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppState.DataDir}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error("打开数据目录失败：" + ex.Message);
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error("打开链接失败：" + ex.Message);
        }
    }
}
