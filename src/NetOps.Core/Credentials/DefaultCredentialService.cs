using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetOps.Core.Credentials;

public sealed class DefaultCredEntry
{
    [JsonPropertyName("brand")] public string Brand { get; set; } = "";
    [JsonPropertyName("models")] public List<string> Models { get; set; } = new();
    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("password")] public string Password { get; set; } = "";
    [JsonPropertyName("notes")] public string Notes { get; set; } = "";
}

public sealed class DefaultCredFile
{
    [JsonPropertyName("version")] public int Version { get; set; }
    [JsonPropertyName("warning")] public string Warning { get; set; } = "";
    [JsonPropertyName("entries")] public List<DefaultCredEntry> Entries { get; set; } = new();
}

public sealed class DefaultCredentialService
{
    private DefaultCredFile _file = new();

    public string Warning => _file.Warning;

    public void Load(string path)
    {
        _file = JsonSerializer.Deserialize<DefaultCredFile>(File.ReadAllText(path))
                ?? new DefaultCredFile();
    }

    public IReadOnlyList<DefaultCredEntry> All => _file.Entries;

    public IEnumerable<DefaultCredEntry> ForBrand(string brand)
    {
        if (string.IsNullOrWhiteSpace(brand)) return _file.Entries;
        return _file.Entries.Where(e =>
            e.Brand.Contains(brand, StringComparison.OrdinalIgnoreCase) ||
            brand.Contains(e.Brand, StringComparison.OrdinalIgnoreCase));
    }

    public string Format(IEnumerable<DefaultCredEntry> list)
    {
        var sb = new StringBuilder();
        sb.AppendLine(_file.Warning);
        sb.AppendLine(new string('-', 48));
        foreach (var e in list)
        {
            var pwd = string.IsNullOrEmpty(e.Password) ? "(blank)" : e.Password;
            sb.AppendLine($"{e.Brand,-12} user={e.Username,-14} pass={pwd}");
            if (e.Models.Count > 0) sb.AppendLine($"             models: {string.Join(", ", e.Models)}");
            if (!string.IsNullOrWhiteSpace(e.Notes)) sb.AppendLine($"             notes: {e.Notes}");
        }
        return sb.ToString();
    }
}
