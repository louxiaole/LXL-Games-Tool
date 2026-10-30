using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LXLGameToolbox.Core;

/// <summary>创建 Windows 快捷方式（.lnk）。</summary>
public static class ShortcutHelper
{
    /// <summary>在桌面创建指向指定程序的快捷方式，返回 .lnk 路径。</summary>
    public static string CreateDesktopShortcut(string name, string targetPath, string description)
    {
        if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
            throw new FileNotFoundException("找不到要创建快捷方式的程序：" + targetPath);

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrEmpty(desktop))
            throw new InvalidOperationException("无法定位桌面目录。");

        var linkPath = Path.Combine(desktop, name + ".lnk");
        var directory = Path.GetDirectoryName(targetPath) ?? "";

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
            throw new InvalidOperationException("当前系统不支持创建快捷方式（缺少 WScript.Shell）。");

        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell,
                new object[] { linkPath });
            if (shortcut == null) throw new InvalidOperationException("创建快捷方式失败。");

            var shortcutType = shortcut.GetType();
            SetProperty(shortcutType, shortcut, "TargetPath", targetPath);
            SetProperty(shortcutType, shortcut, "WorkingDirectory", directory);
            SetProperty(shortcutType, shortcut, "IconLocation", targetPath);
            SetProperty(shortcutType, shortcut, "Description", description);
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            if (shortcut != null) Marshal.ReleaseComObject(shortcut);
            if (shell != null) Marshal.ReleaseComObject(shell);
        }

        Logger.Info($"已创建桌面快捷方式：{linkPath}");
        return linkPath;
    }

    private static void SetProperty(Type type, object instance, string name, string value) =>
        type.InvokeMember(name, BindingFlags.SetProperty, null, instance, new object[] { value });
}
