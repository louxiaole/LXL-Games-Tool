using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using LXLGameToolbox.Core;

namespace LXLGameToolbox.UI.Pages;

/// <summary>左侧导航「下载中心」：集中查看后台下载 / 更新任务的进度。</summary>
public partial class DownloadsPage : UserControl
{
    public DownloadsPage()
    {
        InitializeComponent();
        TaskList.ItemsSource = DownloadCenter.Tasks;
        DownloadCenter.Tasks.CollectionChanged += OnTasksCollectionChanged;
        foreach (var task in DownloadCenter.Tasks) task.PropertyChanged += OnTaskChanged;
        UpdateEmpty();
        Unloaded += (_, _) => DownloadCenter.Tasks.CollectionChanged -= OnTasksCollectionChanged;
    }

    private void OnTasksCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (DownloadTask task in e.NewItems)
                task.PropertyChanged += OnTaskChanged;
        }
        if (e.OldItems != null)
        {
            foreach (DownloadTask task in e.OldItems)
                task.PropertyChanged -= OnTaskChanged;
        }
        UpdateEmpty();
    }

    private void OnTaskChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DownloadTask.State)) return;
        if (sender is not DownloadTask task || task.IsRunning) return;

        AppState.SetStatus(task.State == DownloadState.Completed
            ? $"{task.Name} 下载完成"
            : $"{task.Name} {task.StateText}");
        UpdateEmpty();
    }

    private void UpdateEmpty()
    {
        EmptyPanel.Visibility = DownloadCenter.Tasks.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is DownloadTask task)
            DownloadCenter.Cancel(task);
    }

    private void OpenInstaller_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not DownloadTask task) return;
        if (string.IsNullOrEmpty(task.InstallerPath) || !File.Exists(task.InstallerPath)) return;

        try
        {
            Installer.RunInstaller(task.InstallerPath);
            AppState.SetStatus($"{task.Name} 安装程序已启动");
        }
        catch (Exception ex)
        {
            Logger.Error($"启动安装程序失败：{ex.Message}");
            MessageBox.Show(Window.GetWindow(this), $"启动安装程序失败：{ex.Message}", task.Name,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(AppState.DownloadDir);

    private void Clear_Click(object sender, RoutedEventArgs e) => DownloadCenter.RemoveFinished();

    private static void OpenFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error($"打开目录失败：{ex.Message}");
        }
    }
}
