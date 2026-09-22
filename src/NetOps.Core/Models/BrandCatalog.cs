using System.Text.Json.Serialization;

namespace NetOps.Core.Models;

public sealed class BrandCatalog
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("brands")]
    public List<BrandEntry> Brands { get; set; } = new();

    [JsonPropertyName("models")]
    public List<ModelEntry> Models { get; set; } = new();
}

public sealed class BrandEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("classes")]
    public List<string> Classes { get; set; } = new();
}

public sealed class ModelEntry
{
    [JsonPropertyName("brand")]
    public string Brand { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("class")]
    public string Class { get; set; } = "";

    [JsonPropertyName("protocols")]
    public List<string> Protocols { get; set; } = new();

    [JsonPropertyName("flash")]
    public string Flash { get; set; } = "";
}
