using System.Diagnostics;
using System.IO;

namespace LXLGameToolbox.Core;

public enum UninstallKind
{
    /// <summary>安装在工具箱 tools / apps 目录内，可直接删除。</summary>
    Managed,

    /// <summary>外部安装，但找到了软件自带的卸载程序。</summary>
    External,

    /// <summary>只能在资源管理器里手动处理。</summary>
    Manual
}

/// <summary>工具的卸载与目录迁移。</summary>
public static class ToolMaintenance
{
    public static UninstallKind GetKind(LocalToolInfo local)
    {
        if (local.IsManaged) return UninstallKind.Managed;
        if (!string.IsNullOrEmpty(local.UninstallerPath) && File.Exists(local.UninstallerPath))
            return UninstallKind.External;
        return UninstallKind.Manual;
    }

    /// <summary>程序的主进程是否正在运行。</summary>
    public static bool IsRunning(LocalToolInfo local)
    {
        try
        {
            var name = Path.GetFileNameWithoutExtension(local.ExePath);
            if (string.IsNullOrEmpty(name)) return false;
            return Process.GetProcessesByName(name).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>删除工具箱管理目录中的工具，返回是否全部删除成功。</summary>
    public static bool RemoveManaged(ToolDefinition tool, out string error)
    {
        error = "";
        var candidates = new[]
        {
            Path.Combine(AppState.ToolsDir, tool.Id),
            Path.Combine(AppState.AppsDir, tool.Id)
        };

        var failed = new List<string>();
        foreach (var dir in candidates)
        {
            if (!Directory.Exists(dir)) continue;
            try
            {
                Directory.Delete(dir, true);
                Logger.Info($"已删除 {tool.Name} 安装目录：{dir}");
            }
            catch (Exception ex)
            {
                Logger.Error($"删除 {dir} 失败：{ex.Message}");
                failed.Add(dir);
            }
        }

        ConfigStore.SetManualPath(tool.Id, "");
        LocalScanner.Invalidate();
        if (failed.Count > 0)
        {
            error = "以下目录正被占用，请先关闭程序后重试：\n" + string.Join("\n", failed);
            return false;
        }

        return true;
    }

    /// <summary>启动软件自带的卸载程序。</summary>
    public static bool RunUninstaller(ToolDefinition tool, LocalToolInfo local, out string error)
    {
        error = "";
        try
        {
            Process.Start(new ProcessStartInfo(local.UninstallerPath) { UseShellExecute = true });
            ConfigStore.SetManualPath(tool.Id, "");
            LocalScanner.Invalidate();
            Logger.Info($"启动 {tool.Name} 卸载程序：{local.UninstallerPath}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"启动 {tool.Name} 卸载程序失败：{ex.Message}");
            error = ex.Message;
            return false;
        }
    }

    /// <summary>仅从工具箱移除记录（不删除任何文件）。</summary>
    public static void Forget(ToolDefinition tool)
    {
        ConfigStore.SetManualPath(tool.Id, "");
        LocalScanner.Invalidate();
    }

    /// <summary>
    /// 把已安装目录整体迁移到目标根目录下的 {folderName} 子目录，并写回手动路径。
    /// </summary>
    public static bool MoveInstall(ToolDefinition tool, LocalToolInfo local, string targetRoot,
        out string newExePath, out string error, out bool sourceKept)
    {
        newExePath = "";
        error = "";
        sourceKept = false;

        var sourceDir = local.InstallDir;
        if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
        {
            error = "找不到原安装目录：" + sourceDir;
            return false;
        }

        var destDir = Path.Combine(targetRoot, tool.Id);
        try
        {
            if (string.Equals(Path.GetFullPath(sourceDir).TrimEnd('\\'),
                    Path.GetFullPath(destDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                error = "新目录与原目录相同，无需迁移。";
                return false;
            }
        }
        catch
        {
            // 路径非法时交给后续步骤报错
        }

        if (Directory.Exists(destDir))
        {
            error = "目标目录已存在：" + destDir + "\n请先清空或换一个位置。";
            return false;
        }

        try
        {
            Directory.CreateDirectory(targetRoot);
            Directory.Move(sourceDir, destDir);
            Logger.Info($"{tool.Name} 安装目录已迁移：{sourceDir} → {destDir}");
        }
        catch (Exception ex)
        {
            Logger.Error($"{tool.Name} 目录移动失败，改为复制：{ex.Message}");
            if (!CopyDirectory(sourceDir, destDir, out var copyError))
            {
                error = "迁移失败：" + copyError;
                return false;
            }

            try
            {
                Directory.Delete(sourceDir, true);
            }
            catch (Exception deleteEx)
            {
                Logger.Error($"复制完成但原目录删除失败：{deleteEx.Message}");
                sourceKept = true;
            }
        }

        var fileName = Path.GetFileName(local.ExePath);
        var targetExe = Path.Combine(destDir, fileName);
        if (!File.Exists(targetExe))
        {
            // 可执行文件在子目录时，按相对路径还原
            var relative = local.ExePath.StartsWith(sourceDir, StringComparison.OrdinalIgnoreCase)
                ? local.ExePath[sourceDir.Length..].TrimStart('\\', '/')
                : fileName;
            targetExe = Path.Combine(destDir, relative);
        }

        if (!File.Exists(targetExe))
        {
            error = "迁移后未找到主程序：" + targetExe;
            return false;
        }

        ConfigStore.SetManualPath(tool.Id, targetExe);
        newExePath = targetExe;
        LocalScanner.Invalidate();
        Logger.Info($"{tool.Name} 迁移完成，新程序位置：{targetExe}");
        return true;
    }

    private static bool CopyDirectory(string source, string target, out string error)
    {
        error = "";
        try
        {
            Directory.CreateDirectory(target);
            foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, dir);
                Directory.CreateDirectory(Path.Combine(target, relative));
            }

            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file);
                File.Copy(file, Path.Combine(target, relative), true);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
