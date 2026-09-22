using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetOps.Core.Models;

namespace NetOps.Core.Firmware;

public sealed class FirmwareEntry
{
    [JsonPropertyName("brand")] public string Brand { get; set; } = "";
    [JsonPropertyName("model")] public string Model { get; set; } = "";
    [JsonPropertyName("recommended")] public string Recommended { get; set; } = "";
    [JsonPropertyName("channel")] public string Channel { get; set; } = "";
    [JsonPropertyName("notes")] public string Notes { get; set; } = "";
    [JsonPropertyName("flash")] public string Flash { get; set; } = "";
    [JsonPropertyName("downloadHint")] public string DownloadHint { get; set; } = "";
}

public sealed class FirmwareCatalogFile
{
    [JsonPropertyName("version")] public int Version { get; set; }
    [JsonPropertyName("entries")] public List<FirmwareEntry> Entries { get; set; } = new();
}

public sealed class FirmwareService
{
    private FirmwareCatalogFile _fw = new();

    public void Load(string path)
    {
        var json = File.ReadAllText(path);
        _fw = JsonSerializer.Deserialize<FirmwareCatalogFile>(json)
              ?? new FirmwareCatalogFile();
    }

    public string Diagnose(string brandId, string model, string? currentVersion)
    {
        var entry = _fw.Entries.FirstOrDefault(e =>
            string.Equals(e.Brand, brandId, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(model) || e.Model.Contains(model, StringComparison.OrdinalIgnoreCase)
             || model.Contains(e.Model, StringComparison.OrdinalIgnoreCase)));

        entry ??= _fw.Entries.FirstOrDefault(e =>
            string.Equals(e.Brand, brandId, StringComparison.OrdinalIgnoreCase));

        var sb = new StringBuilder();
        sb.AppendLine("=== FIRMWARE DIAGNOSIS ===");
        sb.AppendLine($"Brand/Model: {brandId} / {model}");
        sb.AppendLine($"Reported current: {currentVersion ?? "(unknown — enter manually or read from device)"}");

        if (entry is null)
        {
            sb.AppendLine("No firmware catalog entry. Use vendor site; flash=manual_only.");
            sb.AppendLine("Safety: never flash an image that does not match exact hardware revision.");
            return sb.ToString();
        }

        sb.AppendLine($"Recommended: {entry.Recommended} ({entry.Channel})");
        sb.AppendLine($"Flash method: {entry.Flash}");
        sb.AppendLine($"Download: {entry.DownloadHint}");
        sb.AppendLine($"Notes: {entry.Notes}");
        sb.AppendLine();
        sb.AppendLine("Offline path:");
        sb.AppendLine("  1) Download on admin PC (device WAN not required)");
        sb.AppendLine("  2) Verify checksum if vendor publishes one");
        sb.AppendLine("  3) Backup config before flash");
        sb.AppendLine("  4) Upload via supported method (web/ssh)");
        sb.AppendLine("  5) Do not power-cycle during write");
        sb.AppendLine();
        sb.AppendLine("Auto-flash is NOT performed in this build (safety). Diagnosis + guidance only.");
        return sb.ToString();
    }
}
