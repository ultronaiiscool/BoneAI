using BoneAI.Game;

var entries = new[]
{
    new SpawnCatalogEntry("Ford", "npc.ford", "BONELAB", "NPCs"),
    new SpawnCatalogEntry("Ford Clone", "mod.ford.clone", "Example mod", "NPCs"),
    new SpawnCatalogEntry("Ford Variant Red", "mod.ford.variant.red", "Example mod", "NPCs"),
    new SpawnCatalogEntry("Ford Variant Blue", "mod.ford.variant.blue", "Example mod", "NPCs"),
    new SpawnCatalogEntry("Nimbus Gun", "gun.nimbus", "BONELAB", "Guns")
};

if (SpawnCatalogMatcher.Resolve(entries, "npc.ford").Barcode != "npc.ford")
    throw new Exception("Exact barcode lookup failed.");
if (SpawnCatalogMatcher.Resolve(entries, "Ford").Barcode != "npc.ford")
    throw new Exception("Exact title must outrank partial titles.");
if (SpawnCatalogMatcher.Resolve(entries, "nimbus").Barcode != "gun.nimbus")
    throw new Exception("Unique partial lookup failed.");
try { SpawnCatalogMatcher.Resolve(entries, "Ford Variant"); throw new Exception("Ambiguous lookup selected an item."); }
catch (InvalidOperationException ex) when (ex.Message.Contains("Ambiguous", StringComparison.Ordinal)) { }
try { SpawnCatalogMatcher.Resolve(entries, "missing"); throw new Exception("Missing item selected an item."); }
catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.Ordinal)) { }
Console.WriteLine("Spawn catalog resolution tests passed.");
