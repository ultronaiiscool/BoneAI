using BoneAI.Tools;
using Newtonsoft.Json.Linq;

static void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var spawn = ToolSchemaCatalog.For("spawn.spawn_npc");
Require(spawn["properties"]?["query"]?["type"]?.Value<string>() == "string", "Spawn query schema missing.");
Require(spawn["properties"]?["position"]?["type"]?.Value<string>() == "object", "Spawn position schema missing.");
Require(spawn["additionalProperties"]?.Value<bool>() == false, "Spawn schema accepts unknown arguments.");
Require(ToolSchemaCatalog.For("spawn.status")["required"]?.Values<string>().Contains("actionId") == true, "Spawn status action ID must be required.");

var teleport = ToolSchemaCatalog.For("player.teleport");
Require(teleport["properties"]?["position"]?["properties"]?["x"]?["type"]?.Value<string>() == "number", "Position vector schema missing.");
Require(teleport["required"]?.Values<string>().Contains("position") == true, "Teleport position must be required.");

var force = ToolSchemaCatalog.For("physics.apply_force");
Require(force["properties"]?["force"]?["type"]?.Value<string>() == "object", "Force vector schema missing.");
var directional = ToolSchemaCatalog.For("physics.force_forward");
Require(directional["properties"]?["magnitude"]?["type"]?.Value<string>() == "number", "Directional magnitude schema missing.");

var unknown = ToolSchemaCatalog.For("unrecognized.future_tool");
Require(unknown["additionalProperties"]?.Value<bool>() == true, "Unknown tools should retain forward-compatible arguments.");
Console.WriteLine("Tool schema tests passed.");
