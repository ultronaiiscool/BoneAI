using System.Reflection;
using BoneLib;
using BoneLib.Notifications;
using BoneAI.Catalogs;
using BoneAI.Fusion;
using BoneAI.Infrastructure;
using BoneAI.Tools;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Combat;
using Il2CppSLZ.Marrow.Data;
using Il2CppSLZ.Marrow.Interaction;
using Il2CppSLZ.Marrow.Warehouse;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BoneAI.Game;

public sealed class GameToolset
{
    private sealed record AvatarEntry(string title, string barcode, string pallet, string provider);
    private sealed record ComponentSnapshot(GameObject Object, Component[] Components, string[] TypeNames, bool IsNpc, bool IsInteractable, float ExpiresAt);
    private sealed record SemanticSnapshot(GameObject Object, bool Value, float ExpiresAt);

    private static readonly HashSet<string> NpcTypes = new(new[] { "PuppetMaster", "BehaviourBaseNav", "AIBrain", "Enemy_Health" }, StringComparer.Ordinal);
    private static readonly HashSet<string> InteractableTypes = new(new[] { "Grip", "Gun", "Seat", "ButtonNode", "LeverNode", "MarrowEntity" }, StringComparer.Ordinal);
    private static readonly HashSet<string> CompactTypes = new(new[] { "Rigidbody", "Grip", "Gun", "PuppetMaster", "BehaviourBaseNav", "AIBrain", "Seat", "ButtonNode", "LeverNode", "MarrowEntity", "Poolee" }, StringComparer.Ordinal);
    private static readonly HashSet<string> SemanticTypes = new(NpcTypes.Concat(InteractableTypes).Concat(new[] { "Poolee" }), StringComparer.Ordinal);

    private readonly AgentConfig _config;
    private readonly FusionBridge _fusion;
    private readonly ObjectRegistry _objects = new();
    private readonly SpawnController _spawns;
    private readonly AvatarCatalogProvider _avatarCatalog;
    private readonly Dictionary<string, float> _avatarDefaults = new();
    private Vector3? _moveDestination;
    private string? _followObject;
    private float _moveSpeed = 2.5f;
    private bool _avatarCatalogLogged;
    private IReadOnlyList<AvatarEntry> _cachedAvatars = Array.Empty<AvatarEntry>();
    private float _avatarCacheExpiresAt;
    private DateTime _avatarProviderRefreshUtc;
    private Task<IReadOnlyList<AvatarCatalogItem>>? _avatarRefreshTask;
    private readonly Dictionary<int, ComponentSnapshot> _componentCache = new();
    private readonly Dictionary<int, ComponentSnapshot> _deepComponentCache = new();
    private readonly Dictionary<int, SemanticSnapshot> _semanticCache = new();
    private readonly Collider[] _overlapBuffer = new Collider[1024];
    private List<GameObject> _nearbyCache = new();
    private Vector3 _nearbyCenter;
    private float _nearbyRadius;
    private float _nearbyExpiresAt;
    private int _cachedScene = int.MinValue;
    private long _worldScans;
    private long _worldCacheHits;
    private double _worldScanMilliseconds;
    private float _nextObjectPrune;
    private float _nextComponentPrune;
    private float _nextSpawnSweep;
    private float _nextMovementStep;
    private long _truncatedWorldScans;

    public GameToolset(AgentConfig config, FusionBridge fusion)
    {
        _config = config;
        _fusion = fusion;
        _spawns = new SpawnController(fusion);
        _avatarCatalog = new AvatarCatalogProvider(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData"));
        _ = RefreshAvatarCatalogAsync();
    }

    public void RegisterTools(ToolRegistry r)
    {
        Register(r, "player.get_state", "Get local player position, rotation, avatar, health, held objects and scene.", GetPlayerState);
        Register(r, "player.get_position", "Get local player position.", GetPlayerState);
        Register(r, "player.get_rotation", "Get local player rotation.", GetPlayerState);
        Register(r, "player.get_avatar", "Get local player avatar.", GetPlayerState);
        Register(r, "player.get_inventory", "Get local player's currently held objects.", GetPlayerState);
        Register(r, "player.teleport", "Teleport local player. arguments: position {x,y,z}, optional rotation {x,y,z}.", Teleport, action:true);
        Register(r, "player.set_health", "Set local current health. arguments: value.", SetHealth, action:true, player:true);
        Register(r, "player.heal", "Restore local player to full health.", Heal, action:true, player:true);
        Register(r, "player.damage", "Damage local player. arguments: amount.", DamagePlayer, action:true, player:true);
        Register(r, "avatar.list", "Search the complete installed Marrow avatar catalog, including SDK mod avatars. arguments: optional query and limit.", ListAvatars);
        Register(r, "avatar.find", "Find installed avatars by display name or barcode. arguments: query, optional limit.", ListAvatars);
        Register(r, "avatar.catalog_status", "Report avatar catalog count, providers, and last refresh time.", AvatarCatalogStatus);
        Register(r, "avatar.refresh", "Rebuild the avatar catalog from WristHub's verified index, installed pallet manifests, and the live Marrow warehouse.", RefreshAvatars, action:true);
        Register(r, "avatar.set", "Change the local avatar by fuzzy display name or exact barcode. arguments: query or barcode.", SetAvatar, action:true, player:true);
        Register(r, "player.list_avatars", "Search the complete installed avatar catalog. arguments: optional query and limit.", ListAvatars);
        Register(r, "player.set_avatar", "Change the local avatar by fuzzy display name or exact barcode. arguments: query or barcode.", SetAvatar, action:true, player:true);
        Register(r, "player.set_physics", "Set runtime avatar strength/speed/agility/vitality. arguments may include upperStrength, lowerStrength, gripStrength, speed, agility, vitality.", SetPhysics, action:true, player:true);
        Register(r, "player.set_strength", "Set upper, lower, and grip strength together. arguments: value.", SetStrength, action:true, player:true);
        Register(r, "player.set_speed", "Set local avatar movement speed. arguments: value.", SetSpeed, action:true, player:true);
        Register(r, "player.set_jump", "Set local avatar agility/jump capability. arguments: value.", SetJump, action:true, player:true);
        Register(r, "player.restore_physics", "Restore runtime avatar physics values captured before the first override.", RestorePhysics, action:true, player:true);
        Register(r, "world.list_nearby", "List compact descriptions and stable IDs for nearby objects. optional arguments: radius, limit.", ListNearby);
        Register(r, "world.find_object", "Fuzzy find nearby objects. arguments: query, optional radius.", FindObject);
        Register(r, "world.find_npc", "Find nearby NPCs by name, returning stable object IDs. arguments: optional query and radius.", FindNpc);
        Register(r, "world.find_interactable", "Find nearby grips, buttons, levers, doors, guns and seats. arguments: optional query and radius.", FindInteractable);
        Register(r, "world.get_object_info", "Inspect a registered object. arguments: objectId.", ObjectInfo);
        Register(r, "world.look_at_target", "Raycast from the player's view and return the object currently being looked at. optional arguments: distance.", LookAtTarget);
        Register(r, "world.find_nearest", "Find the nearest matching world object and return one stable object ID. arguments: optional query, kind (npc/interactable/object), radius.", FindNearest);
        Register(r, "world.get_scene_info", "Get active scene and object counts.", SceneInfo);
        Register(r, "spawn.list", "Search the loaded Marrow warehouse's base-game and installed-mod spawnables. arguments: query, optional limit.", SearchSpawnables);
        Register(r, "spawn.refresh", "Refresh BoneAI's live spawnable catalog from the Marrow warehouse.", RefreshSpawns, action:true, spawn:true);
        Register(r, "spawn.status", "Check the actual local callback result of a prior spawn request. arguments: actionId.", SpawnStatus);
        Register(r, "spawn.spawn", "Spawn a loaded item by exact barcode or unique name. Optional position {x,y,z} and rotation {x,y,z}. Online requests use Fusion's network spawner.", Spawn, action:true, spawn:true);
        Register(r, "spawn.spawn_item", "Spawn any item from the runtime catalog. arguments: barcode or query, optional position.", Spawn, action:true, spawn:true);
        Register(r, "spawn.spawn_npc", "Spawn an NPC from the runtime catalog. arguments: barcode or query, optional position.", Spawn, action:true, spawn:true);
        Register(r, "spawn.spawn_prop", "Spawn a prop from the runtime catalog. arguments: barcode or query, optional position.", Spawn, action:true, spawn:true);
        Register(r, "spawn.spawn_vehicle", "Spawn a vehicle from the runtime catalog. arguments: barcode or query, optional position.", Spawn, action:true, spawn:true);
        Register(r, "spawn.find_and_spawn", "Search BoneAI's live catalog and spawn only a unique match. arguments: query.", Spawn, action:true, spawn:true);
        Register(r, "spawn.despawn", "Despawn/destroy a registered spawned or scene object. arguments: objectId.", Despawn, action:true);
        Register(r, "interaction.grab", "Attach a nearby object's grip to a local hand. arguments: objectId, hand ('left' or 'right').", Grab, action:true);
        Register(r, "interaction.release", "Release the object in a local hand. arguments: hand.", Release, action:true);
        Register(r, "interaction.pull_to_hand", "Move an object to a hand and grab it. arguments: objectId, hand.", PullToHand, action:true);
        Register(r, "interaction.grab_nearest", "Find the nearest matching grippable and grab it. arguments: optional query, hand, radius.", GrabNearest, action:true);
        Register(r, "interaction.bring_to_me", "Move a physics object safely in front of the player. arguments: objectId, optional distance.", BringToMe, action:true);
        Register(r, "interaction.activate", "Activate/use an object through its public parameterless interaction method when exposed. arguments: objectId.", Activate, action:true);
        Register(r, "interaction.use", "Use an object through its public interaction method. arguments: objectId.", Activate, action:true);
        Register(r, "interaction.press_button", "Press a button through its public interaction method. arguments: objectId.", Activate, action:true);
        Register(r, "interaction.pull_lever", "Pull a lever through its public interaction method. arguments: objectId.", Activate, action:true);
        Register(r, "interaction.open", "Open an object through its public interaction method. arguments: objectId.", Activate, action:true);
        Register(r, "interaction.close", "Close an object through its public interaction method. arguments: objectId.", Activate, action:true);
        Register(r, "interaction.push", "Push an object. arguments: objectId, force {x,y,z}.", ApplyImpulse, action:true);
        Register(r, "interaction.throw", "Throw an object. arguments: objectId, force {x,y,z}.", ApplyImpulse, action:true);
        Register(r, "physics.apply_force", "Apply continuous force to a rigidbody. arguments: objectId, force {x,y,z}.", ApplyForce, action:true);
        Register(r, "physics.apply_impulse", "Apply an impulse to a rigidbody. arguments: objectId, force {x,y,z}.", ApplyImpulse, action:true);
        Register(r, "physics.set_velocity", "Set rigidbody velocity. arguments: objectId, velocity {x,y,z}.", SetVelocity, action:true);
        Register(r, "physics.move_object", "Move a rigidbody through its physics API. arguments: objectId, position {x,y,z}.", MoveObject, action:true);
        Register(r, "physics.rotate_object", "Rotate a rigidbody through its physics API. arguments: objectId, rotation Euler {x,y,z}.", RotateObject, action:true);
        Register(r, "combat.aim", "Aim a held gun at an object. arguments: objectId, hand.", Aim, action:true, combat:true);
        Register(r, "combat.shoot", "Fire a held gun. arguments: hand, optional shots.", Shoot, action:true, combat:true);
        Register(r, "combat.reload", "Request instant load on a held gun when supported. arguments: hand.", Reload, action:true, combat:true);
        Register(r, "combat.damage", "Apply a real Marrow Attack to a target's ReceiveAttack path, with legacy health fallback. arguments: objectId, amount.", DamageTarget, action:true, combat:true);
        Register(r, "combat.attack_target", "Perform a real attack on an object ID: aim/fire a held gun, send Fusion player damage, or damage/impulse an NPC. arguments: objectId, optional amount, hand, shots.", AttackTarget, action:true, combat:true);
        Register(r, "combat.attack_player", "Attack a Fusion player through Fusion's PlayerSender damage path. arguments: smallId or query, optional amount.", AttackFusionPlayer, action:true, combat:true);
        Register(r, "combat.attack_nearest", "Find and attack the nearest matching NPC or Fusion player. arguments: optional query, amount, hand, shots, radius.", AttackNearest, action:true, combat:true);
        Register(r, "combat.throw_at", "Throw one registered rigidbody toward another. arguments: objectId, targetObjectId, optional force.", ThrowAt, action:true, combat:true);
        Register(r, "combat.hit", "Hit a target with a physical impulse. arguments: objectId, force {x,y,z}.", ApplyImpulse, action:true, combat:true);
        Register(r, "combat.punch", "Punch a target with a physical impulse. arguments: objectId, force {x,y,z}.", ApplyImpulse, action:true, combat:true);
        Register(r, "combat.kick", "Kick a target with a physical impulse. arguments: objectId, force {x,y,z}.", ApplyImpulse, action:true, combat:true);
        Register(r, "movement.move_to", "Move toward a world position using short player teleports over updates. arguments: position, optional speed.", MoveTo, action:true);
        Register(r, "movement.navigate_to", "Navigate toward a world position using collision-unaware incremental movement. arguments: position, optional speed.", MoveTo, action:true);
        Register(r, "movement.go_to_object", "Move toward a registered object. arguments: objectId, optional speed.", GoToObject, action:true);
        Register(r, "movement.go_to_player", "Move toward a Fusion player selected by query or smallId. optional arguments: query or smallId, speed.", GoToPlayer, action:true);
        Register(r, "movement.follow", "Continuously follow a registered object. arguments: objectId, optional speed.", Follow, action:true);
        Register(r, "movement.stop", "Stop active move/follow behavior.", StopMovement, action:true);
        Register(r, "movement.turn", "Turn local player by yaw degrees. arguments: degrees.", Turn, action:true);
        Register(r, "movement.jump", "Apply an upward impulse to the local physics rig. optional arguments: force.", Jump, action:true);
        Register(r, "vehicle.enter", "Enter a vehicle/seat represented by a registered object. arguments: objectId.", EnterVehicle, action:true);
        Register(r, "vehicle.exit", "Exit the local player's current seat.", ExitVehicle, action:true);
        Register(r, "fusion.get_session", "Get detected Fusion state and authority mode.", c => ToolResult.Success(c, _fusion.GetSession()));
        Register(r, "fusion.get_players", "Get Fusion players from the installed Fusion API.", c => ToolResult.Success(c, _fusion.GetPlayers()));
        Register(r, "fusion.find_player", "Find Fusion players by username or numeric small ID and return stable rig object IDs. arguments: query.", FindFusionPlayer);
        Register(r, "fusion.follow_player", "Continuously follow a Fusion player selected by query or smallId. optional arguments: query or smallId, speed.", FollowFusionPlayer, action:true);
        Register(r, "fusion.get_sync_report", "Explain which action categories use ordinary Fusion replication and which remain local.", c => ToolResult.Success(c, _fusion.GetSynchronizationReport()));
        Register(r, "mods.list_loaded", "List loaded MelonLoader mod assemblies and versions.", LoadedMods);
        Register(r, "mods.get_capabilities", "List useful capabilities discovered in the user's installed mod DLLs and whether the AI integrates them.", ModCapabilities);
        Register(r, "logs.get_recent_errors", "Read recent error/exception lines from the current MelonLoader log. optional arguments: limit.", RecentErrors);
        Register(r, "diagnostics.performance", "Get BoneAI world-query cache hits, physical scans, cache size, and average scan time.", PerformanceDiagnostics);
        Register(r, "ui.notify", "Show an in-headset BoneLib notification. arguments: message.", Notify);
        RegisterExpandedTools(r);
    }

    private void RegisterExpandedTools(ToolRegistry r)
    {
        bool Room() => r.Count < 351;
        foreach (var component in new[]
        {
            "Rigidbody","Grip","Gun","Magazine","AmmoReceiver","Chamber","FirearmCartridge","PuppetMaster","BehaviourBaseNav","AIBrain","Enemy_Health",
            "Seat","ButtonNode","LeverNode","MarrowEntity","Poolee","InventorySlotReceiver","Plug","Socket","KeyReceiver","Door","HingeJoint","ConfigurableJoint",
            "FixedJoint","SpringJoint","ArticulationBody","Collider","Trigger","Zone","Checkpoint","Elevator","Vehicle","Kart","Motor","WheelCollider","HandPose",
            "InteractableHost","GripEvents","ObjectDestructible","Destructible","Health","ImpactSfx","AudioSource","Light","ParticleSystem","Animator","NavMeshAgent",
            "SpawnGun","NimbusGun","Constrainer","PowerPuncher","DevTool","Collectable","CrateSpawner","BoardGenerator","ZipJoint","Balloon","GachaCapsule","Keycard",
            "ControlPanel","ToggleButton","Powerable","Battery","Generator","Turret","Drone","Projectile","Blade","MeleeWeapon","Throwable","Climbable","Ladder","Zipline",
            "Portal","Teleporter","SecurityCamera","Display","Screen","NPC","Avatar","RigManager","Hand","Head","BodyVitals","Receiver","Transmitter","Controller"
        })
        {
            if (!Room()) break;
            var captured = component;
            Register(r, "world.find_component_" + Slug(captured), $"Find nearby objects containing the real Unity/Marrow component type {captured}. optional arguments: query, radius, limit.", c => FindByComponent(c, captured));
        }

        foreach (var preset in new[]
        {
            "pistol","rifle","shotgun","smg","revolver","machine gun","sniper","launcher","sword","knife","axe","hammer","bat","crowbar","spear","shield",
            "ammo","magazine","grenade","gadget","spawn gun","nimbus gun","constrainer","power puncher","dev tool","Ford","Nullbody","Crablet","Omni Projector",
            "security guard","zombie","skeleton","NPC","vehicle","go kart","car","motorcycle","prop","crate","box","barrel","table","chair","locker","door","button",
            "lever","key","keycard","battery","flashlight","radio","balloon","collectible","capsule","basketball","bowling ball","clipboard","traffic cone","trash can",
            "shopping cart","sledgehammer","katana","combat knife","M1911","Eder22","P350","AKM","M16","MP5","Uzi","M870","FAB","vector","crowbar electric"
        })
        {
            if (!Room()) break;
            var captured = preset;
            Register(r, "spawn.preset_" + Slug(captured), $"Search the live Marrow catalog for '{captured}' and spawn a unique match. Optional arguments: query or barcode.", c => SpawnPreset(c, captured), action:true, spawn:true);
        }

        foreach (var verb in new[]
        {
            "OnPress","Press","Activate","Deactivate","Use","Interact","Open","Close","Toggle","Unlock","Lock","Pull","Push","Trigger","Fire","Start","Stop","Enable",
            "Disable","TurnOn","TurnOff","PowerOn","PowerOff","Engage","Disengage","Deploy","Retract","Extend","Collapse","Eject","Insert","Submit","Click","Switch","Ring",
            "Play","Pause","Reset","Release","Grab","Attach","Detach","Mount","Dismount","Ingress","Egress"
        })
        {
            if (!Room()) break;
            var captured = verb;
            Register(r, "interaction.invoke_" + Slug(captured), $"Invoke the exact public parameterless '{captured}' interaction method on a registered object's component. arguments: objectId.", c => InvokeInteraction(c, captured), action:true);
        }

        foreach (var mode in new[] { "force", "impulse", "velocity" })
        foreach (var direction in new[] { "up", "down", "left", "right", "forward", "backward", "toward_player", "away_from_player" })
        {
            if (!Room()) break;
            var capturedMode = mode; var capturedDirection = direction;
            Register(r, $"physics.{capturedMode}_{capturedDirection}", $"Apply {capturedMode} in the {capturedDirection.Replace('_',' ')} direction. arguments: objectId, optional magnitude.", c => DirectionalPhysics(c, capturedMode, capturedDirection), action:true);
        }

        foreach (var (name, amount) in new[] { ("tap",2f),("light",8f),("medium",20f),("heavy",45f),("very_heavy",80f),("critical",150f),("knockout",300f) })
        {
            if (!Room()) break;
            var capturedName = name; var capturedAmount = amount;
            Register(r, "combat.damage_" + capturedName, $"Apply a real Marrow {capturedName.Replace('_',' ')} attack ({capturedAmount} base damage). arguments: objectId, optional amount override.", c => DamagePreset(c, capturedAmount), action:true, combat:true);
        }

        foreach (var (name, value, field) in new[]
        {
            ("strength_normal",1f,"strength"),("strength_strong",2f,"strength"),("strength_super",5f,"strength"),("strength_extreme",10f,"strength"),
            ("speed_normal",1f,"speed"),("speed_fast",2f,"speed"),("speed_super",4f,"speed"),("speed_extreme",8f,"speed"),
            ("jump_normal",1f,"jump"),("jump_high",2f,"jump"),("jump_super",4f,"jump"),("jump_extreme",8f,"jump")
        })
        {
            if (!Room()) break;
            var capturedValue = value; var capturedField = field;
            Register(r, "player.preset_" + name, $"Apply the {name.Replace('_',' ')} runtime avatar preset without modifying avatar files.", c => PlayerPreset(c, capturedField, capturedValue), action:true, player:true);
        }

        foreach (var degrees in new[] { -180f,-135f,-90f,-45f,-30f,-15f,15f,30f,45f,90f,135f,180f })
        {
            if (!Room()) break;
            var captured = degrees;
            Register(r, "movement.turn_" + (captured < 0 ? "left_" : "right_") + Math.Abs(captured), $"Turn the player by {captured} degrees.", c => TurnPreset(c, captured), action:true);
        }

        foreach (var radius in new[] { 1f,2f,3f,5f,8f,10f,15f,20f,30f,40f,50f })
        {
            if (!Room()) break;
            var captured = radius;
            Register(r, "world.scan_radius_" + captured, $"List compact world objects within exactly {captured} meters. optional argument: limit.", c => ScanPreset(c, captured));
        }

        AgentLog.Info($"Registered {r.Count} structured BONELAB tools for BoneAI v2.7.");
    }

    private ToolResult FindByComponent(ToolCall c, string component)
    {
        var radius = c.Arguments["radius"]?.Value<float>() ?? _config.WorldQueryRadius.Value;
        var limit = Math.Clamp(c.Arguments["limit"]?.Value<int>() ?? 30, 1, 100);
        var query = c.Arguments["query"]?.Value<string>() ?? string.Empty;
        var matches = NearbyObjects(radius, 500).Where(go => Components(go).TypeNames.Any(x => x.Equals(component, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(go => Score(go.name, query)).ThenBy(go => Vector3.Distance(Player.Head?.transform.position ?? Vector3.zero, go.transform.position)).Take(limit).Select(go => Describe(go, true)).ToArray();
        return ToolResult.Success(c, new { component, count = matches.Length, matches });
    }

    private ToolResult SpawnPreset(ToolCall c, string preset) { if (c.Arguments["query"] == null && c.Arguments["barcode"] == null) c.Arguments["query"] = preset; return Spawn(c); }
    private ToolResult InvokeInteraction(ToolCall c, string verb)
    {
        var go = NeedObject(c);
        foreach (var component in go.GetComponentsInChildren<Component>())
        {
            var method = component.GetType().GetMethod(verb, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (method == null) continue;
            method.Invoke(component, null);
            return ToolResult.Success(c, new { component = component.GetType().Name, method = verb });
        }
        return ToolResult.Failure(c, $"No component exposed public {verb}().");
    }
    private ToolResult DirectionalPhysics(ToolCall c, string mode, string direction)
    {
        var body = NeedObject(c).GetComponentInChildren<Rigidbody>() ?? throw new InvalidOperationException("Object has no Rigidbody.");
        var magnitude = Mathf.Clamp(c.Arguments["magnitude"]?.Value<float>() ?? 10f, 0f, 100f);
        var head = Player.Head?.transform;
        var vector = direction switch { "up" => Vector3.up, "down" => Vector3.down, "left" => Vector3.left, "right" => Vector3.right, "forward" => head?.forward ?? Vector3.forward, "backward" => -(head?.forward ?? Vector3.forward), "toward_player" => ((head?.position ?? Vector3.zero) - body.position).normalized, _ => (body.position - (head?.position ?? Vector3.zero)).normalized } * magnitude;
        if (mode == "velocity") body.velocity = vector; else body.AddForce(vector, mode == "impulse" ? ForceMode.Impulse : ForceMode.Force);
        return ToolResult.Success(c, new { mode, direction, magnitude });
    }
    private ToolResult DamagePreset(ToolCall c, float amount) { c.Arguments["amount"] ??= amount; return DamageTarget(c); }
    private ToolResult PlayerPreset(ToolCall c, string field, float value) { c.Arguments["value"] = value; return field == "strength" ? SetStrength(c) : field == "speed" ? SetSpeed(c) : SetJump(c); }
    private ToolResult TurnPreset(ToolCall c, float degrees) { c.Arguments["degrees"] = degrees; return Turn(c); }
    private ToolResult ScanPreset(ToolCall c, float radius) { c.Arguments["radius"] = radius; return ListNearby(c); }
    private static string Slug(string value) => new string(value.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');

    private void Register(ToolRegistry r, string name, string description, Func<ToolCall, ToolResult> handler, bool action=false, bool player=false, bool spawn=false, bool combat=false)
        => r.Register(name, description, c =>
        {
            if (!_config.Enabled.Value) return ToolResult.Failure(c, "Agent is disabled.");
            if (action && !_config.AllowActions.Value) return ToolResult.Failure(c, "Gameplay actions are disabled.");
            if (player && !_config.AllowPlayerModification.Value) return ToolResult.Failure(c, "Player modification is disabled.");
            if (spawn && !_config.AllowSpawning.Value) return ToolResult.Failure(c, "Spawning is disabled.");
            if (combat && !_config.AllowCombat.Value) return ToolResult.Failure(c, "Combat actions are disabled.");
            return handler(c);
        });

    private string RequireOwnedNetworkObject(GameObject go)
    {
        if (_fusion.IsSceneNetworked && !_config.FusionSynchronization.Value)
            throw new InvalidOperationException("Fusion synchronization is disabled; network-visible object mutation was blocked.");
        return _fusion.RequireLocalObjectAuthority(go);
    }

    public object GetCompactContext() => new
    {
        player = SafeState(),
        permissions = new { actions = _config.AllowActions.Value, playerModification = _config.AllowPlayerModification.Value, spawning = _config.AllowSpawning.Value, combat = _config.AllowCombat.Value, fusionSynchronization = _config.FusionSynchronization.Value },
        fusion = _fusion.GetSession(),
        fusionPlayers = FusionPlayerData(),
        nearby = CompactNearby(8f, 10),
        recentScene = SceneManager.GetActiveScene().name
    };
    private object SafeState() { try { return PlayerStateData(); } catch (Exception ex) { return new { available=false, error=ex.Message }; } }
    private ToolResult GetPlayerState(ToolCall c) => ToolResult.Success(c, PlayerStateData());
    private object PlayerStateData()
    {
        var rig = Player.RigManager;
        if (rig == null) return new { available = false };
        var avatar = Player.Avatar;
        return new { available=true, scene=SceneManager.GetActiveScene().name, position=V(rig.transform.position), rotation=V(rig.transform.eulerAngles), avatar=avatar?.name, health=ReadMember(rig.health,"curr_Health"), maxHealth=ReadMember(rig.health,"max_Health"), leftHeld=HeldName(Player.LeftHand), rightHeld=HeldName(Player.RightHand) };
    }
    private ToolResult Teleport(ToolCall c) { var rig=NeedRig(); var pos=Vec(c.Arguments["position"]); var e=Vec(c.Arguments["rotation"],rig.transform.eulerAngles); rig.Teleport(pos,e,true); return ToolResult.Success(c,new{position=V(pos)}); }
    private ToolResult SetHealth(ToolCall c) { var h=NeedRig().health; SetMember(h,"curr_Health",Num(c,"value")); return ToolResult.Success(c,new{health=ReadMember(h,"curr_Health")}); }
    private ToolResult Heal(ToolCall c) { NeedRig().health.SetFullHealth(); return ToolResult.Success(c,new{health=ReadMember(NeedRig().health,"curr_Health")}); }
    private ToolResult DamagePlayer(ToolCall c) { NeedRig().health.TAKEDAMAGE(Num(c,"amount")); return ToolResult.Success(c,new{health=ReadMember(NeedRig().health,"curr_Health")}); }
    private ToolResult ListAvatars(ToolCall c)
    {
        var query = c.Arguments["query"]?.Value<string>() ?? string.Empty;
        var limit = Math.Clamp(c.Arguments["limit"]?.Value<int>() ?? 25, 1, 100);
        return ToolResult.Success(c, AvatarCatalog().OrderBy(x => Score(x.title, query)).ThenBy(x => x.title).Take(limit).ToArray());
    }
    private ToolResult AvatarCatalogStatus(ToolCall c)
    {
        var entries = AvatarCatalog().ToArray();
        return ToolResult.Success(c, new { count = entries.Length, refreshing = _avatarRefreshTask is { IsCompleted: false }, lastRefreshUtc = _avatarCatalog.LastRefreshUtc, providers = entries.GroupBy(x => x.provider).ToDictionary(x => x.Key, x => x.Count()) });
    }
    private ToolResult RefreshAvatars(ToolCall c)
    {
        if (_avatarRefreshTask is { IsCompleted: false })
            return ToolResult.Pending(c, new { refreshing = true }, "Avatar catalog refresh is already running in the background.");
        _avatarRefreshTask = _avatarCatalog.RefreshAsync();
        _ = _avatarRefreshTask.ContinueWith(task =>
        {
            if (task.IsFaulted) AgentLog.Exception("avatar catalog refresh", task.Exception!.GetBaseException());
            else AgentLog.Info($"Avatar catalog refreshed in background: {task.Result.Count} avatars.");
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return ToolResult.Pending(c, new { refreshing = true }, "Avatar manifest scan started in the background. Call avatar.catalog_status after it completes.");
    }
    private ToolResult SetAvatar(ToolCall c)
    {
        var request = c.Arguments["barcode"]?.Value<string>() ?? c.Arguments["query"]?.Value<string>() ?? throw new ArgumentException("Missing avatar query or barcode.");
        var avatars = AvatarCatalog().ToArray();
        var match = avatars.FirstOrDefault(x => string.Equals(x.barcode, request, StringComparison.OrdinalIgnoreCase))
                    ?? avatars.OrderBy(x => Score(x.title, request)).ThenBy(x => Score(x.barcode, request)).FirstOrDefault();
        if (match == null) return ToolResult.Failure(c, $"No installed avatar matched '{request}'.");
        if (_fusion.IsOnline && _config.FusionSynchronization.Value)
        {
            _fusion.SwapLocalAvatar(match.barcode);
            return ToolResult.Success(c, new { match.title, match.barcode, pending = true, synchronization = "LabFusion LocalAvatar.SwapAvatarCrate" });
        }
        NeedRig().SwapAvatarCrate(new Barcode(match.barcode), true, null);
        return ToolResult.Success(c, new { match.title, match.barcode, pending = true, synchronization = "local" });
    }
    private ToolResult SetPhysics(ToolCall c)
    {
        var a=Player.Avatar ?? throw new InvalidOperationException("No active avatar.");
        var maps = new[]{("upperStrength","strengthUpper"),("lowerStrength","strengthLower"),("gripStrength","strengthGrip"),("speed","speed"),("agility","agility"),("vitality","vitality")};
        var changes = maps.Where(map => c.Arguments[map.Item1] != null)
            .Select(map => (map.Item1, map.Item2, Value: c.Arguments[map.Item1]!.Value<float>())).ToArray();
        foreach (var change in changes)
            if (!float.IsFinite(change.Value) || change.Value < 0.1f || change.Value > 20f)
                throw new ArgumentException(change.Item1 + " must be between 0.1 and 20.");
        foreach (var change in changes)
        {
            _avatarDefaults.TryAdd(change.Item2, Convert.ToSingle(ReadMember(a, change.Item2)));
            SetMember(a, change.Item2, change.Value);
        }
        return ToolResult.Success(c,new{modified=true});
    }
    private ToolResult SetStrength(ToolCall c)
    {
        var value = Num(c, "value");
        c.Arguments = new JObject { ["upperStrength"] = value, ["lowerStrength"] = value, ["gripStrength"] = value };
        return SetPhysics(c);
    }
    private ToolResult SetSpeed(ToolCall c) { c.Arguments = new JObject { ["speed"] = Num(c, "value") }; return SetPhysics(c); }
    private ToolResult SetJump(ToolCall c) { c.Arguments = new JObject { ["agility"] = Num(c, "value") }; return SetPhysics(c); }
    private ToolResult RestorePhysics(ToolCall c) { var a=Player.Avatar??throw new InvalidOperationException("No active avatar."); foreach(var x in _avatarDefaults) SetMember(a,x.Key,x.Value); _avatarDefaults.Clear(); return ToolResult.Success(c); }
    private ToolResult ListNearby(ToolCall c) => ToolResult.Success(c,CompactNearby(c.Arguments["radius"]?.Value<float>()??_config.WorldQueryRadius.Value,c.Arguments["limit"]?.Value<int>()??_config.WorldQueryLimit.Value));
    private ToolResult FindObject(ToolCall c) { var q=Str(c,"query"); var all=NearbyObjects(c.Arguments["radius"]?.Value<float>()??_config.WorldQueryRadius.Value,100).OrderBy(x=>Score(x.name,q)).Take(10).Select(x=>Describe(x)).ToArray(); return ToolResult.Success(c,all); }
    private ToolResult FindNpc(ToolCall c) => FindSemantic(c, IsNpc);
    private ToolResult FindInteractable(ToolCall c) => FindSemantic(c, IsInteractable);
    private ToolResult ObjectInfo(ToolCall c) { var go=NeedObject(c); return ToolResult.Success(c,Describe(go,true)); }
    private ToolResult SceneInfo(ToolCall c) => ToolResult.Success(c,new{name=SceneManager.GetActiveScene().name,buildIndex=SceneManager.GetActiveScene().buildIndex,nearbyCount=NearbyObjects(_config.WorldQueryRadius.Value,500).Count});
    private ToolResult LookAtTarget(ToolCall c)
    {
        var head = Player.Head ?? throw new InvalidOperationException("Player head is unavailable.");
        var distance = Mathf.Clamp(c.Arguments["distance"]?.Value<float>() ?? 30f, 0.5f, 100f);
        if (!Physics.Raycast(head.transform.position, head.transform.forward, out var hit, distance)) return ToolResult.Failure(c, "No object is under the player's view ray.");
        return ToolResult.Success(c, Describe(CanonicalObject(hit.collider), true));
    }
    private ToolResult FindNearest(ToolCall c)
    {
        var query = c.Arguments["query"]?.Value<string>() ?? string.Empty;
        var kind = c.Arguments["kind"]?.Value<string>()?.ToLowerInvariant() ?? "object";
        var radius = c.Arguments["radius"]?.Value<float>() ?? _config.WorldQueryRadius.Value;
        IEnumerable<GameObject> matches = NearbyObjects(radius, 500);
        if (kind == "npc") matches = matches.Where(IsNpc);
        else if (kind == "interactable") matches = matches.Where(IsInteractable);
        var match = matches.OrderBy(x => Score(x.name, query)).ThenBy(x => Vector3.Distance(Player.Head?.transform.position ?? Vector3.zero, x.transform.position)).FirstOrDefault();
        return match == null ? ToolResult.Failure(c, "No matching nearby object was found.") : ToolResult.Success(c, Describe(match, true));
    }
    private ToolResult FindSemantic(ToolCall c, Func<GameObject, bool> predicate)
    {
        var query = c.Arguments["query"]?.Value<string>() ?? string.Empty;
        var radius = c.Arguments["radius"]?.Value<float>() ?? _config.WorldQueryRadius.Value;
        var matches = NearbyObjects(radius, 300).Where(predicate).OrderBy(x => Score(x.name, query)).ThenBy(x => Vector3.Distance(Player.Head?.transform.position ?? Vector3.zero, x.transform.position)).Take(20).Select(x => Describe(x)).ToArray();
        return ToolResult.Success(c, matches);
    }

    private object[] FusionPlayerData()
    {
        try
        {
            return _fusion.GetPlayerSnapshots().Select(x => new
            {
                smallId = x.SmallId,
                username = x.Username,
                isHost = x.IsHost,
                isMe = x.IsMe,
                objectId = SafeRegister(x.RigObject),
                position = V(x.Position)
            }).Cast<object>().ToArray();
        }
        catch (Exception ex)
        {
            AgentLog.Warn("Fusion player context unavailable during rig transition: " + ex.GetBaseException().Message);
            return Array.Empty<object>();
        }
    }

    private ToolResult FindFusionPlayer(ToolCall c)
    {
        var query = Str(c, "query");
        var players = _fusion.GetPlayerSnapshots().OrderBy(x => Score(x.Username, query)).ThenBy(x => x.SmallId).Select(x => new
        {
            smallId = x.SmallId,
            username = x.Username,
            isHost = x.IsHost,
            isMe = x.IsMe,
            objectId = SafeRegister(x.RigObject),
            position = V(x.Position)
        }).Take(10).ToArray();
        return ToolResult.Success(c, players);
    }

    private FusionBridge.PlayerSnapshot ResolveFusionPlayer(ToolCall c)
    {
        var players = _fusion.GetPlayerSnapshots();
        if (c.Arguments["smallId"]?.Value<byte?>() is byte id)
            return players.FirstOrDefault(x => x.SmallId == id) ?? throw new InvalidOperationException($"Fusion player {id} was not found.");
        var query = Str(c, "query");
        return players.OrderBy(x => Score(x.Username, query)).FirstOrDefault() ?? throw new InvalidOperationException($"No Fusion player matched '{query}'.");
    }

    private IEnumerable<AvatarEntry> AvatarCatalog()
    {
        if (Time.unscaledTime < _avatarCacheExpiresAt && _avatarProviderRefreshUtc == _avatarCatalog.LastRefreshUtc) return _cachedAvatars;
        var found = new Dictionary<string, AvatarEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _avatarCatalog.Items)
            found[item.Barcode] = new AvatarEntry(item.Title, item.Barcode, item.Source, item.Provider);
        var warehouse = AssetWarehouse.Instance;
        if (warehouse != null)
        {
            foreach (var crate in warehouse.GetCrates())
            {
                if (crate is not AvatarCrate avatar) continue;
                var barcode = avatar.Barcode?.ID ?? string.Empty;
                if (string.IsNullOrWhiteSpace(barcode)) continue;
                found[barcode] = new AvatarEntry(avatar.Title ?? barcode, barcode, avatar.Pallet?.Title ?? string.Empty, "Marrow warehouse");
            }
        }
        _cachedAvatars = found.Values.ToArray();
        _avatarProviderRefreshUtc = _avatarCatalog.LastRefreshUtc;
        _avatarCacheExpiresAt = Time.unscaledTime + 20f;
        return _cachedAvatars;
    }

    private async Task RefreshAvatarCatalogAsync()
    {
        try
        {
            var entries = await _avatarCatalog.RefreshAsync().ConfigureAwait(false);
            AgentLog.Info($"Avatar catalog provider ready with {entries.Count} avatars.");
        }
        catch (Exception ex) { AgentLog.Exception("avatar catalog refresh", ex); }
    }

    private ToolResult SearchSpawnables(ToolCall c)
    {
        var query=c.Arguments["query"]?.Value<string>()??string.Empty; var limit=Math.Clamp(c.Arguments["limit"]?.Value<int>()??20,1,100);
        var catalog = SpawnCatalog();
        if (catalog.Count == 0 && AssetWarehouse.Instance?.InitialLoaded != true) return ToolResult.Failure(c, "Marrow warehouse is not loaded yet.");
        if (catalog.Count == 0 && _spawns.IsRefreshing) return ToolResult.Pending(c, new { catalogLoading = true }, "Spawn catalog is loading in small batches. Retry shortly.");
        if (catalog.Count == 0 && _spawns.EntryCount == 0) return ToolResult.Pending(c, new { catalogLoading = true }, "Spawn catalog will start loading on the next game update. Retry shortly.");
        return ToolResult.Success(c,catalog.OrderBy(x=>Score(x.Title,query)).Take(limit)
            .Select(x=>new{title=x.Title,barcode=x.Barcode,source=x.Source,category=x.Category,loaded=true}).ToArray());
    }
    private ToolResult RefreshSpawns(ToolCall c){if(AssetWarehouse.Instance?.InitialLoaded!=true)return ToolResult.Failure(c,"Marrow warehouse is not loaded; try again after the level finishes loading.");var previous=_spawns.Refresh();return ToolResult.Pending(c,new{provider="Marrow warehouse",previousCount=previous,refreshing=true},"Spawn catalog refresh queued for per-frame batches. Call spawn.list shortly.");}
    private ToolResult SpawnStatus(ToolCall c)
    {
        var actionId=Str(c,"actionId");
        var attempt=_spawns.GetAttempt(actionId);
        if(attempt==null)return ToolResult.Failure(c,"No recent spawn request has that action ID.");
        var spawned=attempt.Spawned;
        var data=new{requestedActionId=actionId,state=attempt.State,title=attempt.Entry.Title,barcode=attempt.Entry.Barcode,
            objectId=spawned==null?null:SafeRegister(spawned),networkRequested=attempt.NetworkRequested,
            networkEntityId=attempt.NetworkEntityId,peerConfirmed=false,error=attempt.Error};
        if(attempt.State=="failed")return ToolResult.Failure(c,attempt.Error??"Spawn failed.");
        if(attempt.State=="confirmed_local")return ToolResult.Success(c,data);
        return ToolResult.Pending(c,data,attempt.Error??"No local spawn callback has confirmed this request. Fusion peer visibility is not independently confirmed.");
    }
    private ToolResult Spawn(ToolCall c)
    {
        var request=c.Arguments["barcode"]?.Value<string>()??Str(c,"query");
        if(_fusion.IsOnline&&!_config.FusionSynchronization.Value)
            return ToolResult.Failure(c,"Fusion synchronization is disabled; refusing an online local-only spawn.");
        var head=Player.Head;
        if(head==null)return ToolResult.Failure(c,"Player head is not available for a safe spawn position.");
        var forward=head.forward; forward.y=0;
        if(forward.sqrMagnitude<0.001f)forward=Vector3.forward;
        forward.Normalize();
        var position=Vec(c.Arguments["position"],head.position+forward*1.25f+Vector3.down*0.12f);
        var rotation=Quaternion.Euler(Vec(c.Arguments["rotation"],Quaternion.LookRotation(forward,Vector3.up).eulerAngles));
        var attempt=_spawns.Spawn(c.Id,request,position,rotation);
        return ToolResult.Pending(c,new{provider="BoneAI",title=attempt.Entry.Title,barcode=attempt.Entry.Barcode,
            actionId=c.Id,networkRequested=attempt.NetworkRequested,position=V(position),confirmed=false},
            "Spawn requested. Call spawn.status with this actionId to check local completion; peer visibility still requires a second-client check.");
    }
    private ToolResult Despawn(ToolCall c){var go=NeedObject(c);var sync=RequireOwnedNetworkObject(go);var poolee=FindComponentByName(go,"Poolee");var method=poolee?.GetType().GetMethod("Despawn",Type.EmptyTypes);if(method!=null){method.Invoke(poolee,null);return ToolResult.Pending(c,new{path="Poolee.Despawn",synchronization=sync},"Despawn was requested; object removal and peer replication are not yet confirmed.");}return ToolResult.Failure(c,"Object is not a pooled spawnable and was not destroyed locally.");}

    private ToolResult Grab(ToolCall c){var go=NeedObject(c); var hand=Hand(c); var grip=go.GetComponentInChildren<Grip>(); if(grip==null)return ToolResult.Failure(c,"No Grip component was found."); RequireOwnedNetworkObject(go); grip.Snatch(hand,false); return HeldObjectMatches(go,HeldObject(hand)) ? ToolResult.Success(c,new{objectId=Str(c,"objectId"),hand=HandName(c),confirmed=true}) : ToolResult.Pending(c,new{objectId=Str(c,"objectId"),hand=HandName(c)},"Grip was invoked, but the hand attachment is not confirmed yet.");}
    private ToolResult Release(ToolCall c){var h=Hand(c); h.DetachObject(); return HeldObject(h)==null ? ToolResult.Success(c,new{hand=HandName(c),confirmed=true}) : ToolResult.Pending(c,new{hand=HandName(c)},"Detach was invoked, but the hand still reports an attached object.");}
    private ToolResult PullToHand(ToolCall c){var go=NeedObject(c); var h=Hand(c); var grip=go.GetComponentInChildren<Grip>(); if(grip==null)return ToolResult.Failure(c,"No Grip component was found."); RequireOwnedNetworkObject(go); go.transform.position=h.transform.position; grip.Snatch(h,false); return HeldObjectMatches(go,HeldObject(h)) ? ToolResult.Success(c,new{objectId=Str(c,"objectId"),confirmed=true}) : ToolResult.Pending(c,new{objectId=Str(c,"objectId")},"Pull and grip were invoked, but attachment is not confirmed yet.");}
    private ToolResult GrabNearest(ToolCall c)
    {
        var query = c.Arguments["query"]?.Value<string>() ?? string.Empty;
        var radius = c.Arguments["radius"]?.Value<float>() ?? _config.WorldQueryRadius.Value;
        var match = NearbyObjects(radius, 300).Where(x => x.GetComponentInChildren<Grip>() != null).OrderBy(x => Score(x.name, query)).ThenBy(x => Vector3.Distance(Player.Head?.transform.position ?? Vector3.zero, x.transform.position)).FirstOrDefault();
        if (match == null) return ToolResult.Failure(c, "No matching grippable object was found.");
        c.Arguments["objectId"] = _objects.Register(match);
        return PullToHand(c);
    }
    private ToolResult BringToMe(ToolCall c)
    {
        var go = NeedObject(c);
        var body = go.GetComponentInChildren<Rigidbody>() ?? throw new InvalidOperationException("Object has no Rigidbody.");
        var head = Player.Head ?? throw new InvalidOperationException("Player head is unavailable.");
        var distance = Mathf.Clamp(c.Arguments["distance"]?.Value<float>() ?? 1.2f, 0.4f, 4f);
        var destination = head.transform.position + head.transform.forward * distance - Vector3.up * 0.25f;
        var synchronization = RequireOwnedNetworkObject(go);
        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.MovePosition(destination);
        return ToolResult.Pending(c, new { objectId = Str(c, "objectId"), requestedPosition = V(destination), synchronization },
            "Rigidbody move was requested; inspect the object after the physics step before claiming its position or peer visibility.");
    }
    private ToolResult Activate(ToolCall c)
    {
        var go=NeedObject(c); var names=new[]{"OnPress","Press","Activate","Use","Interact","Open"};
        foreach(var component in go.GetComponentsInChildren<Component>()) foreach(var name in names){var m=component.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public,null,Type.EmptyTypes,null); if(m!=null){m.Invoke(component,null); return ToolResult.Pending(c,new{component=component.GetType().Name,method=name},"Interaction method was invoked; resulting object state is not confirmed.");}}
        return ToolResult.Failure(c,"No supported public interaction method was exposed by this object.");
    }
    private ToolResult ApplyForce(ToolCall c)=>RigidAction(c,rb=>rb.AddForce(BoundedVector(c.Arguments["force"],100f),ForceMode.Force));
    private ToolResult ApplyImpulse(ToolCall c)=>RigidAction(c,rb=>rb.AddForce(BoundedVector(c.Arguments["force"],100f),ForceMode.Impulse));
    private ToolResult SetVelocity(ToolCall c)=>RigidAction(c,rb=>rb.velocity=BoundedVector(c.Arguments["velocity"],50f));
    private ToolResult MoveObject(ToolCall c)=>RigidAction(c,rb=>rb.MovePosition(Vec(c.Arguments["position"])));
    private ToolResult RotateObject(ToolCall c)=>RigidAction(c,rb=>rb.MoveRotation(Quaternion.Euler(Vec(c.Arguments["rotation"]))));
    private ToolResult RigidAction(ToolCall c,Action<Rigidbody> action){var go=NeedObject(c);var rb=go.GetComponentInChildren<Rigidbody>()??throw new InvalidOperationException("Object has no Rigidbody.");var sync=RequireOwnedNetworkObject(go);action(rb);return ToolResult.Pending(c,new{objectId=Str(c,"objectId"),synchronization=sync},"Physics command was issued; its next-step transform and peer replication are not yet confirmed.");}

    private ToolResult Aim(ToolCall c){var target=NeedObject(c); var held=HeldObject(Hand(c))??throw new InvalidOperationException("Hand is empty."); held.transform.rotation=Quaternion.LookRotation(target.transform.position-held.transform.position,Vector3.up); return ToolResult.Success(c);}
    private ToolResult Shoot(ToolCall c){var held=HeldObject(Hand(c))??throw new InvalidOperationException("Hand is empty."); var gun=FindComponentByName(held,"Gun")??throw new InvalidOperationException("Held object has no Gun component."); var fire=gun.GetType().GetMethod("Fire",Type.EmptyTypes)??throw new MissingMethodException("Gun.Fire"); var requested=c.Arguments["shots"]?.Value<int>()??1;var attempts=Math.Clamp(requested,1,3); for(var i=0;i<attempts;i++)fire.Invoke(gun,null); return ToolResult.Pending(c,new{requestedShots=requested,attemptedShots=attempts,capped=requested>3},"Gun.Fire was invoked at most three times in this frame; ammunition, projectile, hit, and peer delivery were not confirmed.");}
    private ToolResult Reload(ToolCall c){var held=HeldObject(Hand(c))??throw new InvalidOperationException("Hand is empty."); var gun=FindComponentByName(held,"Gun")??throw new InvalidOperationException("Held object has no Gun component."); var m=gun.GetType().GetMethod("InstantLoadAsync",Type.EmptyTypes)??throw new MissingMethodException("Gun.InstantLoadAsync"); m.Invoke(gun,null); return ToolResult.Pending(c,new{requested=true},"Async reload was requested; completion and ammunition were not confirmed.");}
    private ToolResult DamageTarget(ToolCall c)
    {
        var go=NeedObject(c); var amount=Num(c,"amount");
        var origin = Player.Head?.transform.position ?? NeedRig().transform.position;
        var attack = CreateAttack(amount, origin, go.transform.position - origin);
        foreach (var comp in go.GetComponentsInChildren<Component>())
        {
            var receive = comp.GetType().GetMethod("ReceiveAttack", new[] { typeof(Attack) });
            if (receive != null)
            {
                var synchronization = RequireOwnedNetworkObject(go);
                receive.Invoke(comp, new object[] { attack });
                return ToolResult.Pending(c, new { component = comp.GetType().Name, method = "ReceiveAttack", requestedDamage=amount, synchronization }, "Attack receiver was invoked; health change and peer delivery were not confirmed.");
            }
            var legacy = comp.GetType().GetMethod("TAKEDAMAGE", new[] { typeof(float) }) ?? comp.GetType().GetMethod("TakeDamage", new[] { typeof(float) });
            if (legacy != null)
            {
                if (_fusion.IsOnline) return ToolResult.Failure(c,"Legacy local-only damage was blocked in Fusion to avoid desync.");
                legacy.Invoke(comp, new object[] { amount });
                return ToolResult.Pending(c, new { component = comp.GetType().Name, method = legacy.Name, requestedDamage=amount }, "Legacy damage method was invoked; health change was not confirmed.");
            }
        }
        return ToolResult.Failure(c,"Target exposes no supported damage receiver.");
    }
    private ToolResult AttackTarget(ToolCall c)
    {
        var target = NeedObject(c);
        var amount = c.Arguments["amount"]?.Value<float>() ?? 25f;
        var preferred = HandName(c);
        foreach (var handName in new[] { preferred, preferred == "left" ? "right" : "left" })
        {
            var hand = handName == "left" ? Player.LeftHand : Player.RightHand;
            var held = hand == null ? null : HeldObject(hand);
            var gun = held == null ? null : FindComponentByName(held, "Gun");
            if (held == null || gun == null) continue;
            held.transform.rotation = Quaternion.LookRotation(target.transform.position - held.transform.position, Vector3.up);
            var fire = gun.GetType().GetMethod("Fire", Type.EmptyTypes) ?? throw new MissingMethodException("Gun.Fire");
            var requestedShots = c.Arguments["shots"]?.Value<int>() ?? 1;
            var shots = Math.Clamp(requestedShots, 1, 3);
            for (var i = 0; i < shots; i++) fire.Invoke(gun, null);
            return ToolResult.Pending(c, new { mode = "held gun", hand = handName, requestedShots, attemptedShots=shots, capped=requestedShots>3 }, "Gun.Fire was invoked at most three times in this frame; hits and peer delivery were not confirmed.");
        }
        if (_fusion.TryGetPlayerId(target, out var smallId))
        {
            if (!_config.FusionSynchronization.Value) return ToolResult.Failure(c, "Fusion synchronization is disabled.");
            var origin = Player.Head?.transform.position ?? NeedRig().transform.position;
            _fusion.DamagePlayer(smallId, amount, origin, target.transform.position - origin);
            return ToolResult.Pending(c, new { mode = "Fusion player damage", smallId, requestedDamage=amount, networkPath = "PlayerSender.SendPlayerDamage" }, "Fusion damage message sent; remote health change was not confirmed.");
        }
        var result = DamageTarget(new ToolCall { Id = c.Id, Name = c.Name, Arguments = new JObject { ["objectId"] = Str(c, "objectId"), ["amount"] = amount } });
        if (result.Result != "success") return result;
        var body = target.GetComponentInChildren<Rigidbody>();
        if (body != null)
        {
            var origin = Player.Head?.transform.position ?? NeedRig().transform.position;
            body.AddForce((target.transform.position - origin).normalized * Mathf.Clamp(amount * 0.2f, 2f, 20f), ForceMode.Impulse);
        }
        return ToolResult.Success(c, new { mode = "Marrow damage receiver", amount, impulse = body != null });
    }
    private ToolResult AttackFusionPlayer(ToolCall c)
    {
        if (!_config.FusionSynchronization.Value) return ToolResult.Failure(c, "Fusion synchronization is disabled.");
        var player = ResolveFusionPlayer(c);
        var amount = c.Arguments["amount"]?.Value<float>() ?? 25f;
        var origin = Player.Head?.transform.position ?? NeedRig().transform.position;
        _fusion.DamagePlayer(player.SmallId, amount, origin, player.Position - origin);
        return ToolResult.Pending(c, new { player = player.Username, smallId = player.SmallId, requestedDamage=amount, networkPath = "PlayerSender.SendPlayerDamage" }, "Fusion damage message sent; remote health change was not confirmed.");
    }
    private ToolResult AttackNearest(ToolCall c)
    {
        var query = c.Arguments["query"]?.Value<string>() ?? string.Empty;
        var radius = c.Arguments["radius"]?.Value<float>() ?? _config.WorldQueryRadius.Value;
        var candidates = NearbyObjects(radius, 500).Where(x => IsNpc(x) || _fusion.TryGetPlayerId(x, out _)).OrderBy(x => Score(x.name, query)).ThenBy(x => Vector3.Distance(Player.Head?.transform.position ?? Vector3.zero, x.transform.position));
        var target = candidates.FirstOrDefault();
        if (target == null) return ToolResult.Failure(c, "No nearby NPC or Fusion player matched.");
        c.Arguments["objectId"] = _objects.Register(target);
        return AttackTarget(c);
    }
    private ToolResult ThrowAt(ToolCall c)
    {
        var source = NeedObject(c);
        var targetId = c.Arguments["targetObjectId"]?.Value<string>() ?? throw new ArgumentException("Missing string argument: targetObjectId");
        if (!_objects.TryGet(targetId, out var target)) return ToolResult.Failure(c, "Target object handle is missing or expired: " + targetId);
        var body = source.GetComponentInChildren<Rigidbody>() ?? throw new InvalidOperationException("Thrown object has no Rigidbody.");
        var force = Mathf.Clamp(c.Arguments["force"]?.Value<float>() ?? 15f, 1f, 60f);
        var synchronization = RequireOwnedNetworkObject(source);
        body.AddForce((target.transform.position - body.position).normalized * force, ForceMode.VelocityChange);
        return ToolResult.Pending(c, new { objectId = Str(c, "objectId"), targetObjectId = targetId, requestedForce = force, synchronization }, "Throw impulse was issued; travel and impact are not confirmed.");
    }
    private ToolResult MoveTo(ToolCall c){_moveDestination=CheckedDestination(Vec(c.Arguments["position"]));_followObject=null;_moveSpeed=CheckedMoveSpeed(c);return ToolResult.Pending(c,new{started=true,destination=V(_moveDestination.Value)},"Movement started; destination has not been reached yet.");}
    private ToolResult GoToObject(ToolCall c)
    {
        var go = NeedObject(c);
        _moveDestination = go.transform.position;
        _followObject = null;
        _moveSpeed = CheckedMoveSpeed(c);
        return ToolResult.Pending(c, new { started = true, objectId = Str(c, "objectId"), destination = V(_moveDestination.Value) }, "Movement started; destination has not been reached yet.");
    }
    private ToolResult GoToPlayer(ToolCall c)
    {
        var player = ResolveFusionPlayer(c);
        if (player.RigObject == null) return ToolResult.Failure(c, "Fusion player's rig is not currently available.");
        c.Arguments["objectId"] = _objects.Register(player.RigObject);
        return GoToObject(c);
    }
    private ToolResult Follow(ToolCall c){NeedObject(c);_followObject=Str(c,"objectId");_moveDestination=null;_moveSpeed=CheckedMoveSpeed(c);return ToolResult.Pending(c,new{started=true},"Follow behavior started and remains active until stopped.");}
    private ToolResult FollowFusionPlayer(ToolCall c)
    {
        var player = ResolveFusionPlayer(c);
        if (player.RigObject == null) return ToolResult.Failure(c, "Fusion player's rig is not currently available.");
        c.Arguments["objectId"] = _objects.Register(player.RigObject);
        return Follow(c);
    }
    private ToolResult StopMovement(ToolCall c){_moveDestination=null;_followObject=null;return ToolResult.Success(c);}
    private ToolResult Turn(ToolCall c){var rig=NeedRig();var e=rig.transform.eulerAngles;e.y+=Num(c,"degrees");rig.Teleport(rig.transform.position,e,true);return ToolResult.Success(c,new{yaw=e.y});}
    private ToolResult Jump(ToolCall c){var force=c.Arguments["force"]?.Value<float>()??4.5f;if(!float.IsFinite(force)||force<0.1f||force>8f)return ToolResult.Failure(c,"Jump force must be between 0.1 and 8.");var rig=NeedRig();if(rig.activeSeat!=null)return ToolResult.Failure(c,"Cannot jump while seated.");var body=rig.physicsRig?.rbFeet;if(body==null)return ToolResult.Failure(c,"Physics rig feet rigidbody is unavailable; jump was not applied.");body.AddForce(Vector3.up*force,ForceMode.VelocityChange);return ToolResult.Pending(c,new{requestedForce=force,rigidbody="PhysicsRig.rbFeet"},"One verified rig rigidbody received an impulse; actual motion is not confirmed.");}
    private ToolResult EnterVehicle(ToolCall c){var go=NeedObject(c);var seat=FindComponentByName(go,"Seat")??throw new InvalidOperationException("No Seat component found.");var m=seat.GetType().GetMethod("IngressRig")??throw new MissingMethodException("Seat.IngressRig");m.Invoke(seat,new object[]{NeedRig()});return NeedRig().activeSeat!=null ? ToolResult.Success(c,new{seated=true,peerReplicationConfirmed=false}) : ToolResult.Pending(c,new{requested=true},"Seat ingress was invoked; seated state is not confirmed yet.");}
    private ToolResult ExitVehicle(ToolCall c){var seat=NeedRig().activeSeat;if(seat==null)return ToolResult.Failure(c,"Player is not seated.");seat.EgressRig(false);return NeedRig().activeSeat==null ? ToolResult.Success(c,new{seated=false,peerReplicationConfirmed=false}) : ToolResult.Pending(c,new{requested=true},"Seat egress was invoked; exit is not confirmed yet.");}
    private ToolResult LoadedMods(ToolCall c)=>ToolResult.Success(c,AppDomain.CurrentDomain.GetAssemblies().Select(a=>new{name=a.GetName().Name,version=a.GetName().Version?.ToString()}).Where(x=>!string.IsNullOrWhiteSpace(x.name)).OrderBy(x=>x.name).ToArray());
    private ToolResult ModCapabilities(ToolCall c)
    {
        bool Has(string name)=>AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name==name);
        return ToolResult.Success(c,new object[]{
            new{name="BoneAI Spawner",loaded=true,usable=true,integration="live Marrow catalog, local spawn callbacks, and Fusion network requests without SpawnLab"},
            new{name="LabFusion",loaded=Has("LabFusion"),usable=true,integration="session/player queries and existing action replication"},
            new{name="Force Pull Anything",loaded=Has("Force Pull Anything"),usable=true,integration="its Grip patch automatically improves agent grip pulls"},
            new{name="StrengthMod",loaded=Has("StrengthMod"),usable=true,integration="coexists; agent runtime avatar overrides remain separate"},
            new{name="Stat Changer",loaded=Has("Stat_Changer__Simple_Edition_"),usable=true,integration="verified speed/jump fields informed agent modifiers"},
            new{name="QuickItem",loaded=Has("QuickItem"),usable=false,integration="private saved-item workflow; BoneAI uses its own spawner"},
            new{name="PowerTools",loaded=Has("PowerTools"),usable=false,integration="global cheats deliberately not changed implicitly"},
            new{name="Portals",loaded=Has("Portals"),usable=false,integration="portal spawning is local AssetSpawner and not claimed network-safe"},
            new{name="Echolocation",loaded=Has("Echolocation"),usable=false,integration="internal perception cache is non-public; Fusion players queried directly"},
            new{name="ModioModNetworker",loaded=Has("ModioModNetworker"),usable=true,integration="automatically observes Fusion spawn traffic; no direct calls required"},
            new{name="WristHub",loaded=Has("WristHub.Core"),usable=true,integration="verified avatar index is used as the primary installed-avatar catalog; unrelated UI/system controls remain isolated"},
            new{name="BoneAI.Catalogs",loaded=true,usable=true,integration="Built into BoneAI.dll; scans WristHub and installed pallet manifests"}
        });
    }
    private ToolResult RecentErrors(ToolCall c)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MelonLoader", "Latest.log");
        if (!File.Exists(path)) return ToolResult.Failure(c, "Latest.log was not found.");
        var limit = Math.Clamp(c.Arguments["limit"]?.Value<int>() ?? 40, 1, 200);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (stream.Length > 256 * 1024) stream.Seek(-256 * 1024, SeekOrigin.End);
        using var reader = new StreamReader(stream);
        var lines = reader.ReadToEnd().Split('\n')
            .Where(x => x.Contains("error", StringComparison.OrdinalIgnoreCase) || x.Contains("exception", StringComparison.OrdinalIgnoreCase))
            .TakeLast(limit).Select(Infrastructure.RuntimeSecrets.Redact).ToArray();
        return ToolResult.Success(c, lines);
    }
    private ToolResult Notify(ToolCall c){Notifier.Send(new Notification{Title="BoneAI",Message=Str(c,"message"),ShowTitleOnPopup=true,Type=NotificationType.Information,PopupLength=4});return ToolResult.Success(c);}
    private ToolResult PerformanceDiagnostics(ToolCall c) => ToolResult.Success(c, new
    {
        physicalWorldScans = _worldScans,
        cacheHits = _worldCacheHits,
        hitRatePercent = _worldScans + _worldCacheHits == 0 ? 0 : Math.Round(100d * _worldCacheHits / (_worldScans + _worldCacheHits), 1),
        averagePhysicalScanMs = _worldScans == 0 ? 0 : Math.Round(_worldScanMilliseconds / _worldScans, 3),
        cachedObjects = _componentCache.Count,
        truncatedWorldScans = _truncatedWorldScans
    });

    public void Update()
    {
        _spawns.TickCatalog();
        if (!_avatarCatalogLogged && AssetWarehouse.Instance?.InitialLoaded == true)
        {
            _avatarCatalogLogged = true;
            try { AgentLog.Info($"Combined avatar catalog ready with {AvatarCatalog().Count()} avatars."); }
            catch (Exception ex) { AgentLog.Warn("Avatar catalog validation failed: " + ex.GetBaseException().Message); }
        }
        if (Time.unscaledTime >= _nextObjectPrune) { _objects.Prune(); _nextObjectPrune = Time.unscaledTime + 2f; }
        if (Time.unscaledTime >= _nextComponentPrune)
        {
            PruneComponents(_componentCache);
            PruneComponents(_deepComponentCache);
            foreach (var id in _semanticCache.Where(x => x.Value.Object == null || x.Value.ExpiresAt < Time.unscaledTime).Select(x => x.Key).ToArray()) _semanticCache.Remove(id);
            _nextComponentPrune = Time.unscaledTime + 10f;
        }
        if (Time.unscaledTime >= _nextSpawnSweep) { _spawns.SweepExpiredRequests(); _nextSpawnSweep = Time.unscaledTime + 5f; }
        if (Time.unscaledTime < _nextMovementStep) return;
        var step = Mathf.Clamp(Time.unscaledTime - (_nextMovementStep - 0.1f), 0.02f, 0.15f);
        _nextMovementStep = Time.unscaledTime + 0.1f;
        try
        {
            if (_followObject != null)
            {
                if (_objects.TryGet(_followObject, out var target)) _moveDestination = target.transform.position - target.transform.forward * 1.5f;
                else { _followObject = null; _moveDestination = null; }
            }
            if (_moveDestination is not Vector3 destination || Player.RigManager == null) return;
            var rig = Player.RigManager;
            if (rig.activeSeat != null) return;
            var current = rig.transform.position;
            var flat = new Vector3(destination.x, current.y, destination.z);
            if (Vector3.Distance(current, flat) < 0.15f)
            {
                if (_followObject == null) _moveDestination = null;
                return;
            }
            var next = Vector3.MoveTowards(current, flat, _moveSpeed * step);
            rig.Teleport(next, rig.transform.eulerAngles, true);
        }
        catch (Exception ex)
        {
            _followObject = null;
            _moveDestination = null;
            AgentLog.Warn("Movement stopped after game exception: " + ex.GetBaseException().Message);
        }
    }

    private List<GameObject> NearbyObjects(float radius,int limit)
    {
        EnsureSceneCache();
        var center = Player.Head?.transform.position ?? Vector3.zero;
        radius = Mathf.Clamp(radius, 0.5f, 50f);
        limit = Math.Clamp(limit, 1, 500);
        if (Time.unscaledTime < _nearbyExpiresAt && radius <= _nearbyRadius && Vector3.SqrMagnitude(center - _nearbyCenter) < 0.25f)
        {
            _worldCacheHits++;
            return _nearbyCache.Where(x => x != null && Vector3.SqrMagnitude(x.transform.position - center) <= radius * radius).Take(limit).ToList();
        }
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var seen = new HashSet<int>();
        var result = new List<GameObject>();
        var hitCount = Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer);
        if (hitCount == _overlapBuffer.Length) _truncatedWorldScans++;
        for (var i = 0; i < hitCount; i++)
        {
            var hit = _overlapBuffer[i];
            if (hit == null) continue;
            var go = CanonicalObject(hit);
            if (seen.Add(go.GetInstanceID())) result.Add(go);
        }
        result.Sort((a, b) => Vector3.SqrMagnitude(a.transform.position - center).CompareTo(Vector3.SqrMagnitude(b.transform.position - center)));
        _nearbyCenter = center; _nearbyRadius = radius; _nearbyExpiresAt = Time.unscaledTime + 0.25f; _nearbyCache = result;
        _worldScans++; _worldScanMilliseconds += (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency;
        return result.Take(limit).ToList();
    }
    private object[] CompactNearby(float radius,int limit)=>NearbyObjects(radius,limit).Select(x=>Describe(x)).ToArray();
    private object Describe(GameObject go,bool detailed=false){var info=Components(go);var types=info.TypeNames.Where(x=>detailed||CompactTypes.Contains(x)).Take(detailed?30:10).ToArray();var head=Player.Head?.transform.position??Vector3.zero;return new{id=_objects.Register(go),name=go.name,position=V(go.transform.position),distance=Math.Round(Vector3.Distance(head,go.transform.position),2),kind=info.IsNpc?"npc":_fusion.TryGetPlayerId(go,out _)?"fusionPlayer":info.IsInteractable?"interactable":"object",types};}
    private GameObject CanonicalObject(Collider hit)
    {
        if (hit.attachedRigidbody != null) return hit.attachedRigidbody.gameObject;
        var current = hit.transform;
        GameObject best = hit.gameObject;
        for (var depth = 0; current != null && depth < 8; depth++, current = current.parent)
        {
            var candidate = current.gameObject;
            if (HasSemanticComponent(candidate)) return candidate;
            if (!candidate.name.Contains("collider", StringComparison.OrdinalIgnoreCase) && !candidate.name.StartsWith("prop_static", StringComparison.OrdinalIgnoreCase)) best = candidate;
        }
        return best;
    }
    private bool HasSemanticComponent(GameObject go)
    {
        var id = go.GetInstanceID();
        if (_semanticCache.TryGetValue(id, out var cached) && cached.Object == go && Time.unscaledTime < cached.ExpiresAt) return cached.Value;
        var value = go.GetComponents<Component>().Any(x => x != null && SemanticTypes.Contains(x.GetType().Name));
        _semanticCache[id] = new SemanticSnapshot(go, value, Time.unscaledTime + 1f);
        return value;
    }
    private ComponentSnapshot Components(GameObject go, bool includeChildren = false)
    {
        EnsureSceneCache();
        var id = go.GetInstanceID();
        var cache = includeChildren ? _deepComponentCache : _componentCache;
        if (cache.TryGetValue(id, out var cached) && cached.Object == go && Time.unscaledTime < cached.ExpiresAt) return cached;
        var components = includeChildren ? go.GetComponentsInChildren<Component>() : go.GetComponents<Component>();
        var names = components.Where(x => x != null).Select(x => x.GetType().Name).Distinct().ToArray();
        var snapshot = new ComponentSnapshot(go, components, names, names.Any(NpcTypes.Contains), names.Any(InteractableTypes.Contains), Time.unscaledTime + 1f);
        cache[id] = snapshot;
        return snapshot;
    }
    private bool IsNpc(GameObject go) => Components(go).IsNpc;
    private bool IsInteractable(GameObject go) => Components(go).IsInteractable;
    private static void PruneComponents(Dictionary<int, ComponentSnapshot> cache)
    {
        foreach (var id in cache.Where(x => x.Value.Object == null || x.Value.ExpiresAt < Time.unscaledTime).Select(x => x.Key).ToArray()) cache.Remove(id);
    }
    private void EnsureSceneCache()
    {
        var scene = SceneManager.GetActiveScene().buildIndex;
        if (scene == _cachedScene) return;
        _cachedScene = scene; _componentCache.Clear(); _deepComponentCache.Clear(); _semanticCache.Clear(); _nearbyCache.Clear(); _nearbyExpiresAt = 0;
    }
    private IReadOnlyList<SpawnCatalogEntry> SpawnCatalog()=>_spawns.GetEntries();
    private static int Score(string value,string query){if(string.IsNullOrWhiteSpace(query))return 0;value=value.ToLowerInvariant();query=query.ToLowerInvariant();if(value==query)return 0;if(value.Contains(query))return 1+value.IndexOf(query);return Levenshtein(value,query)+20;}
    private static int Levenshtein(string a,string b){var d=new int[b.Length+1];for(var j=0;j<=b.Length;j++)d[j]=j;for(var i=1;i<=a.Length;i++){var prev=d[0];d[0]=i;for(var j=1;j<=b.Length;j++){var old=d[j];d[j]=Math.Min(Math.Min(d[j]+1,d[j-1]+1),prev+(a[i-1]==b[j-1]?0:1));prev=old;}}return d[b.Length];}
    private GameObject NeedObject(ToolCall c){var id=Str(c,"objectId");if(!_objects.TryGet(id,out var go))throw new InvalidOperationException("Object handle is missing or expired: "+id);return go;}
    private string? SafeRegister(GameObject? go)
    {
        if (go == null) return null;
        try { return _objects.Register(go); }
        catch { return null; }
    }
    private static RigManager NeedRig()=>Player.RigManager??throw new InvalidOperationException("Local player rig is unavailable.");
    private static Hand Hand(ToolCall c)=>HandName(c)=="left"?(Player.LeftHand??throw new InvalidOperationException("Left hand unavailable.")):(Player.RightHand??throw new InvalidOperationException("Right hand unavailable."));
    private static string HandName(ToolCall c)=>(c.Arguments["hand"]?.Value<string>()??"right").ToLowerInvariant();
    private static GameObject? HeldObject(Hand hand)=>hand.m_CurrentAttachedGO;
    private static bool HeldObjectMatches(GameObject requested, GameObject? held) => held != null &&
        (held == requested || held.transform.IsChildOf(requested.transform) || requested.transform.IsChildOf(held.transform));
    private static string? HeldName(Hand? hand)=>hand==null?null:HeldObject(hand)?.name;
    private Component? FindComponentByName(GameObject go,string name)=>Components(go,true).Components.FirstOrDefault(x=>x!=null&&x.GetType().Name==name);
    private static Vector3 CheckedDestination(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z) ? value : throw new ArgumentException("Movement destination must be finite.");
    private static float CheckedMoveSpeed(ToolCall call){var value=call.Arguments["speed"]?.Value<float>()??2.5f;if(!float.IsFinite(value)||value<0.1f||value>5f)throw new ArgumentException("Movement speed must be between 0.1 and 5 meters per second.");return value;}
    private static string Str(ToolCall c,string key)=>c.Arguments[key]?.Value<string>()??throw new ArgumentException("Missing string argument: "+key);
    private static float Num(ToolCall c,string key){var value=c.Arguments[key]?.Value<float>()??throw new ArgumentException("Missing numeric argument: "+key);if(!float.IsFinite(value)||Math.Abs(value)>10000f)throw new ArgumentException(key+" must be a finite value with magnitude at most 10000.");return value;}
    private static Attack CreateAttack(float amount, Vector3 origin, Vector3 direction) => new()
    {
        damage = Mathf.Max(0f, amount),
        attackType = AttackType.Blunt,
        origin = origin,
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward,
        normal = direction.sqrMagnitude > 0.0001f ? -direction.normalized : Vector3.back
    };
    private static Vector3 Vec(JToken? token,Vector3? fallback=null){var value=token==null?(fallback??throw new ArgumentException("Missing vector.")):new Vector3(token["x"]?.Value<float>()??0,token["y"]?.Value<float>()??0,token["z"]?.Value<float>()??0);if(!float.IsFinite(value.x)||!float.IsFinite(value.y)||!float.IsFinite(value.z)||Math.Abs(value.x)>10000f||Math.Abs(value.y)>10000f||Math.Abs(value.z)>10000f)throw new ArgumentException("Vector coordinates must be finite and within 10000 units.");return value;}
    private static Vector3 BoundedVector(JToken? token,float maximum){var value=Vec(token);if(value.sqrMagnitude>maximum*maximum)throw new ArgumentException("Vector magnitude exceeds safe gameplay limit of "+maximum+".");return value;}
    private static object V(Vector3 v)=>new{x=Math.Round(v.x,3),y=Math.Round(v.y,3),z=Math.Round(v.z,3)};
    private static object? ReadMember(object? o,string name){if(o==null)return null;var t=o.GetType();return t.GetProperty(name)?.GetValue(o)??t.GetField(name)?.GetValue(o);}
    private static void SetMember(object o,string name,object value){var t=o.GetType();var p=t.GetProperty(name);if(p!=null){p.SetValue(o,Convert.ChangeType(value,p.PropertyType));return;}var f=t.GetField(name)??throw new MissingMemberException(t.FullName,name);f.SetValue(o,Convert.ChangeType(value,f.FieldType));}
}
