using System.Diagnostics;
using System.IO;
using System.Windows;
using LXLGameToolbox.Core;

namespace LXLGameToolbox.UI;

/// <summary>卸载 / 迁移的交互流程（工具卡片与详情窗口共用）。</summary>
public static class ToolOperations
{
    public static bool Uninstall(Window? owner, ToolItem item)
    {
        var local = item.Local;
        if (local == null) return false;

        if (ToolMaintenance.IsRunning(local))
        {
            MessageBox.Show(owner, $"请先关闭正在运行的「{item.Name}」，然后再卸载。", "无法卸载",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return ToolMaintenance.GetKind(local) switch
        {
            UninstallKind.Managed => UninstallManaged(owner, item, local),
            UninstallKind.External => UninstallExternal(owner, item, local),
            _ => UninstallManual(owner, item, local)
        };
    }

    private static bool UninstallManaged(Window? owner, ToolItem item, LocalToolInfo local)
    {
        var result = MessageBox.Show(owner,
            $"将从工具箱安装目录中删除「{item.Name}」：\n{local.InstallDir}\n\n删除后如需使用要重新安装。确定卸载？",
            "卸载 " + item.Name, MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return false;

        if (!ToolMaintenance.RemoveManaged(item.Tool, out var error))
        {
            MessageBox.Show(owner, error, "卸载失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        item.Refresh();
        AppState.SetStatus($"{item.Name} 已卸载");
        MessageBox.Show(owner, $"「{item.Name}」已卸载，安装文件已删除。", "卸载完成",
            MessageBoxButton.OK, MessageBoxImage.Information);
        return true;
    }

    private static bool UninstallExternal(Window? owner, ToolItem item, LocalToolInfo local)
    {
        var result = MessageBox.Show(owner,
            $"「{item.Name}」安装在：\n{local.InstallDir}\n\n将启动软件自带的卸载程序，请在向导里完成卸载；" +
            "完成后回到工具箱点击「扫描本地」刷新状态。\n\n继续？",
            "卸载 " + item.Name, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return false;

        if (!ToolMaintenance.RunUninstaller(item.Tool, local, out var error))
        {
            MessageBox.Show(owner, "启动卸载程序失败：" + error, "卸载失败",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        AppState.SetStatus($"{item.Name} 卸载程序已启动");
        return false;
    }

    private static bool UninstallManual(Window? owner, ToolItem item, LocalToolInfo local)
    {
        var result = MessageBox.Show(owner,
            $"「{item.Name}」安装在本机其他位置：\n{local.InstallDir}\n\n" +
            "没有找到软件自带的卸载程序，工具箱不会删除这里的文件。\n\n" +
            "是否打开安装目录手动处理？（选择「否」则仅从工具箱移除记录）",
            "卸载 " + item.Name, MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (result == MessageBoxResult.Yes) OpenFolder(local.InstallDir);

        ToolMaintenance.Forget(item.Tool);
        item.Refresh();
        AppState.SetStatus($"{item.Name} 已从工具箱移除，文件未删除");
        return false;
    }

    public static bool Migrate(Window? owner, ToolItem item)
    {
        var local = item.Local;
        if (local == null) return false;

        if (ToolMaintenance.IsRunning(local))
        {
            MessageBox.Show(owner, $"请先关闭正在运行的「{item.Name}」，然后再迁移。", "无法迁移",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        Directory.CreateDirectory(AppState.AppsDir);
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = $"选择「{item.Name}」的迁移目标文件夹",
            InitialDirectory = AppState.AppsDir
        };
        if (dialog.ShowDialog(owner) != true) return false;

        var targetRoot = dialog.FolderName;
        if (string.IsNullOrWhiteSpace(targetRoot)) return false;

        var destination = Path.Combine(targetRoot, item.Tool.Id);
        var result = MessageBox.Show(owner,
            $"将把「{item.Name}」的全部文件从：\n{local.InstallDir}\n\n移动到：\n{destination}\n\n" +
            "移动后工具箱会记录新位置并从这里启动。部分软件迁移后可能需要重新登录。\n\n继续？",
            "迁移 " + item.Name, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return false;

        if (!ToolMaintenance.MoveInstall(item.Tool, local, targetRoot, out var newExe, out var error,
                out var sourceKept))
        {
            MessageBox.Show(owner, error, "迁移失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        item.Refresh();
        AppState.SetStatus($"{item.Name} 已迁移到新位置");
        var message = $"「{item.Name}」迁移完成。\n\n新位置：\n{newExe}";
        if (sourceKept)
            message += $"\n\n部分原文件被占用未能删除，可稍后手动清理：\n{local.InstallDir}";
        MessageBox.Show(owner, message, "迁移完成", MessageBoxButton.OK, MessageBoxImage.Information);
        return true;
    }

    private static void OpenFolder(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error("打开目录失败：" + ex.Message);
        }
    }
}
