using System.Text;

namespace NetOps.Core.Tools;

/// <summary>Read-only view of Windows hosts file.</summary>
public sealed class HostsFileService
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "drivers", "etc", "hosts");

    public string Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== HOSTS FILE (read-only) ===");
        sb.AppendLine("Path: " + DefaultPath);
        sb.AppendLine();
        try
        {
            if (!File.Exists(DefaultPath))
            {
                sb.AppendLine("File not found.");
                return sb.ToString();
            }
            var lines = File.ReadAllLines(DefaultPath);
            var entries = 0;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    // keep comments lightly
                    if (line.StartsWith('#'))
                        sb.AppendLine(raw);
                    continue;
                }
                sb.AppendLine(raw);
                entries++;
            }
            sb.AppendLine();
            sb.AppendLine($"Active entries: {entries}");
            sb.AppendLine("OK (read-only — edit outside app if needed)");
        }
        catch (Exception ex)
        {
            sb.AppendLine("FAIL: " + ex.Message);
        }
        return sb.ToString();
    }
}
