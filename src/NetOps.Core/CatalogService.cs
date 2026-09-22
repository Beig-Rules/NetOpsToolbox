using System.Text.Json;
using NetOps.Core.Models;

namespace NetOps.Core;

public static class CatalogService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static BrandCatalog LoadFromJson(string json)
    {
        var catalog = JsonSerializer.Deserialize<BrandCatalog>(json, Options)
            ?? throw new InvalidOperationException("Invalid brand catalog JSON.");
        if (catalog.Version < 1)
            throw new InvalidOperationException("Unsupported catalog version.");
        return catalog;
    }

    public static BrandCatalog LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return LoadFromJson(json);
    }

    public static IEnumerable<ModelEntry> ModelsForBrand(BrandCatalog catalog, string brandId)
        => catalog.Models.Where(m => string.Equals(m.Brand, brandId, StringComparison.OrdinalIgnoreCase));
}
