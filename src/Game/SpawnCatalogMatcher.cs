namespace BoneAI.Game;

public sealed record SpawnCatalogEntry(string Title, string Barcode, string Source, string Category);

public static class SpawnCatalogMatcher
{
    public static SpawnCatalogEntry Resolve(IReadOnlyList<SpawnCatalogEntry> entries, string query)
    {
        query = query.Trim();
        if (query.Length == 0) throw new ArgumentException("Provide a spawnable name or barcode.");

        var barcodes = entries.Where(x => x.Barcode.Equals(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (barcodes.Length == 1) return barcodes[0];
        var titles = entries.Where(x => x.Title.Equals(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (titles.Length == 1) return titles[0];

        var matches = titles.Length > 1 ? titles : entries.Where(x =>
            x.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            x.Barcode.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(7).ToArray();
        if (matches.Length == 1) return matches[0];
        if (matches.Length > 1) throw new InvalidOperationException("Ambiguous spawnable; use an exact barcode: " +
            string.Join(", ", matches.Take(6).Select(x => x.Title + " (" + x.Barcode + ")")));
        throw new InvalidOperationException("Spawnable not found in the loaded Marrow warehouse. Search spawn.list first.");
    }
}
