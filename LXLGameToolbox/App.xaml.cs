using System.Windows;
using System.Windows.Threading;
using LXLGameToolbox.Core;

namespace LXLGameToolbox;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppState.EnsureFolders();
        Logger.Info($"{AppState.AppName} {AppState.VersionText} 启动，目录：{AppState.BaseDir}");

        // 启动即应用上次保存的主题与背景配置
        UI.ThemeManager.ApplyAll();

        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        Logger.Error("未处理异常：" + args.Exception);
        MessageBox.Show("程序遇到异常：" + args.Exception.Message, AppState.AppName,
            MessageBoxButton.OK, MessageBoxImage.Warning);
        args.Handled = true;

        // 启动阶段就失败时直接退出，避免留下无窗口的进程
        if (MainWindow == null) Shutdown();
    }
}
