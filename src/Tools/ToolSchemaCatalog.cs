using Newtonsoft.Json.Linq;

namespace BoneAI.Tools;

/// <summary>Specific, provider-neutral argument schemas for frequently used game tools.</summary>
public static class ToolSchemaCatalog
{
    private readonly record struct Field(string Name, JToken Type, bool Required = false);

    public static JObject For(string name)
    {
        if (name.StartsWith("spawn.preset_", StringComparison.OrdinalIgnoreCase))
            return Object(F("query", Text()), F("barcode", Text()));
        if (name.StartsWith("world.find_component_", StringComparison.OrdinalIgnoreCase))
            return Object(F("query", Text()), F("radius", Number()), F("limit", Integer()));
        if (name.StartsWith("world.scan_radius_", StringComparison.OrdinalIgnoreCase))
            return Object(F("limit", Integer()));
        if (name.StartsWith("interaction.invoke_", StringComparison.OrdinalIgnoreCase))
            return Object(F("objectId", Text(), true));
        if (name.StartsWith("physics.force_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("physics.impulse_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("physics.velocity_", StringComparison.OrdinalIgnoreCase))
            return Object(F("objectId", Text(), true), F("magnitude", Number()));
        if (name.StartsWith("combat.damage_", StringComparison.OrdinalIgnoreCase))
            return Object(F("objectId", Text(), true), F("amount", Number()));
        return name switch
        {
            "tools.search" => Object(F("query", Text(), true), F("limit", Integer())),
            "world.find_object" or "world.find_npc" or "world.find_interactable" or "world.find_nearest" =>
                Object(F("query", Text()), F("radius", Number()), F("limit", Integer()), F("kind", Text())),
            "world.get_object_info" or "spawn.despawn" or "interaction.activate" or "interaction.use" or
            "interaction.press_button" or "interaction.pull_lever" or "interaction.open" or "interaction.close" =>
                Object(F("objectId", Text(), true)),
            "world.list_nearby" => Object(F("radius", Number()), F("limit", Integer())),
            "world.look_at_target" => Object(F("distance", Number())),
            "avatar.list" or "avatar.find" or "player.list_avatars" or "spawn.list" =>
                Object(F("query", Text()), F("limit", Integer())),
            "spawn.status" => Object(F("actionId", Text(), true)),
            "avatar.set" or "player.set_avatar" => Object(F("query", Text()), F("barcode", Text())),
            "spawn.spawn" or "spawn.spawn_item" or "spawn.spawn_npc" or "spawn.spawn_prop" or
            "spawn.spawn_vehicle" or "spawn.find_and_spawn" => Object(F("query", Text()), F("barcode", Text()), F("position", Vector()), F("rotation", Vector())),
            "player.teleport" => Object(F("position", Vector(), true), F("rotation", Vector())),
            "player.set_health" or "player.set_strength" or "player.set_speed" or "player.set_jump" =>
                Object(F("value", Number(), true)),
            "player.damage" => Object(F("amount", Number(), true)),
            "interaction.grab" or "interaction.pull_to_hand" =>
                Object(F("objectId", Text(), true), F("hand", Hand())),
            "interaction.grab_nearest" => Object(F("query", Text()), F("hand", Hand()), F("radius", Number())),
            "interaction.release" => Object(F("hand", Hand())),
            "interaction.bring_to_me" => Object(F("objectId", Text(), true), F("distance", Number())),
            "interaction.push" or "interaction.throw" or "physics.apply_force" or "physics.apply_impulse" =>
                Object(F("objectId", Text(), true), F("force", Vector(), true)),
            "physics.set_velocity" => Object(F("objectId", Text(), true), F("velocity", Vector(), true)),
            "physics.move_object" => Object(F("objectId", Text(), true), F("position", Vector(), true)),
            "physics.rotate_object" => Object(F("objectId", Text(), true), F("rotation", Vector(), true)),
            "combat.shoot" => Object(F("hand", Hand()), F("shots", Integer())),
            "combat.reload" => Object(F("hand", Hand())),
            "combat.attack_target" => Object(F("objectId", Text(), true), F("amount", Number()), F("shots", Integer()), F("hand", Hand())),
            "combat.attack_fusion_player" => Object(F("query", Text()), F("smallId", Integer()), F("amount", Number())),
            "movement.move_to" or "movement.navigate_to" => Object(F("position", Vector(), true), F("speed", Number())),
            "movement.go_to_object" or "movement.follow" => Object(F("objectId", Text(), true), F("speed", Number())),
            "movement.go_to_player" or "fusion.follow_player" => Object(F("query", Text()), F("smallId", Integer()), F("speed", Number())),
            "movement.turn" => Object(F("degrees", Number(), true)),
            "movement.jump" => Object(F("force", Number())),
            "fusion.find_player" => Object(F("query", Text(), true)),
            "ui.notify" => Object(F("message", Text(), true)),
            _ => new JObject { ["type"] = "object", ["additionalProperties"] = true }
        };
    }

    private static Field F(string name, JToken type, bool required = false) => new(name, type, required);
    private static JObject Text() => new() { ["type"] = "string" };
    private static JObject Number() => new() { ["type"] = "number" };
    private static JObject Integer() => new() { ["type"] = "integer" };
    private static JObject Hand() => new() { ["type"] = "string", ["enum"] = new JArray("left", "right") };
    private static JObject Vector() => Object(F("x", Number(), true), F("y", Number(), true), F("z", Number(), true));

    private static JObject Object(params Field[] fields)
    {
        var properties = new JObject();
        var required = new JArray();
        foreach (var field in fields)
        {
            properties[field.Name] = field.Type;
            if (field.Required) required.Add(field.Name);
        }
        return new JObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };
    }
}
