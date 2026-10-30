using System.IO;

namespace LXLGameToolbox.Core;

/// <summary>按天写入 Data\logs 的简易日志。</summary>
public static class Logger
{
    private static readonly object Sync = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(AppState.LogDir);
                var path = Path.Combine(AppState.LogDir, $"log-{DateTime.Now:yyyyMMdd}.txt");
                File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // 日志失败不影响主流程
        }
    }
}
