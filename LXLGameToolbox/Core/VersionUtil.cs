using System.Text.RegularExpressions;

namespace LXLGameToolbox.Core;

/// <summary>版本号归一化与比较（支持 v1.0.0 / 3.0.4beta / 1.0.0.0 等写法）。</summary>
public static class VersionUtil
{
    /// <summary>取出文本中的数字版本部分，例如 "v1.0.0" → "1.0.0"，"3.0.4beta" → "3.0.4"。</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var match = Regex.Match(text.Trim(), "\\d+(?:\\.\\d+)*");
        return match.Success ? match.Value : text.Trim();
    }

    /// <summary>比较两个版本，返回值大于 0 表示 left 更新。</summary>
    public static int Compare(string? left, string? right)
    {
        var a = Parse(left);
        var b = Parse(right);
        var length = Math.Max(a.Length, b.Length);
        for (var i = 0; i < length; i++)
        {
            var x = i < a.Length ? a[i] : 0;
            var y = i < b.Length ? b[i] : 0;
            if (x != y) return x.CompareTo(y);
        }
        return 0;
    }

    /// <summary>latest 是否比 current 新。current 为空时视为需要更新。</summary>
    public static bool IsNewer(string? latest, string? current)
    {
        if (string.IsNullOrWhiteSpace(latest)) return false;
        if (string.IsNullOrWhiteSpace(current)) return true;
        return Compare(latest, current) > 0;
    }

    public static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "-";
        var units = new[] { "B", "KB", "MB", "GB" };
        var size = (double)bytes;
        var index = 0;
        while (size >= 1024 && index < units.Length - 1)
        {
            size /= 1024;
            index++;
        }
        return index == 0 ? $"{size:F0} {units[index]}" : $"{size:F1} {units[index]}";
    }

    private static int[] Parse(string? text)
    {
        var core = Normalize(text);
        if (string.IsNullOrEmpty(core)) return Array.Empty<int>();
        return core.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var value) ? value : 0)
            .ToArray();
    }
}
