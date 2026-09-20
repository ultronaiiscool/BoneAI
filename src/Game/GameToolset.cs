using System.Reflection;
using BoneLib;
using BoneLib.Notifications;
using BonelabAIAgent.Fusion;
using BonelabAIAgent.Infrastructure;
using BonelabAIAgent.Tools;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BonelabAIAgent.Game;

public sealed class GameToolset
{
    private readonly AgentConfig _config;
    private readonly FusionBridge _fusion;
    private readonly ObjectRegistry _objects = new();
    private readonly SpawnLabBridge _spawnLab = new();
    private readonly Dictionary<string, float> _avatarDefaults = new();
    private Vector3? _moveDestination;
    private string? _followObject;
    private float _moveSpeed = 2.5f;

    public GameToolset(AgentConfig config, FusionBridge fusion) { _config = config; _fusion = fusion; }

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
        Register(r, "player.set_avatar", "Change avatar by exact barcode. arguments: barcode.", SetAvatar, action:true, player:true);
        Register(r, "player.set_physics", "Set runtime avatar strength/speed/agility/vitality. arguments may include upperStrength, lowerStrength, gripStrength, speed, agility, vitality.", SetPhysics, action:true, player:true);
        Register(r, "player.restore_physics", "Restore runtime avatar physics values captured before the first override.", RestorePhysics, action:true, player:true);
        Register(r, "world.list_nearby", "List compact descriptions and stable IDs for nearby objects. optional arguments: radius, limit.", ListNearby);
        Register(r, "world.find_object", "Fuzzy find nearby objects. arguments: query, optional radius.", FindObject);
        Register(r, "world.get_object_info", "Inspect a registered object. arguments: objectId.", ObjectInfo);
        Register(r, "world.get_scene_info", "Get active scene and object counts.", SceneInfo);
        Register(r, "spawn.list", "Search SpawnLab's complete base-game and downloaded SDK spawnable catalog. arguments: query, optional limit.", SearchSpawnables);
        Register(r, "spawn.refresh", "Tell SpawnLab to rescan pallet files and rebuild its catalog.", RefreshSpawnLab, action:true, spawn:true);
        Register(r, "spawn.spawn", "Spawn a catalog item using its exact barcode or fuzzy name. arguments: barcode or query, optional position.", Spawn, action:true, spawn:true);
        Register(r, "spawn.spawn_item", "Spawn any item from the runtime catalog. arguments: barcode or query, optional position.", Spawn, action:true, spawn:true);
        Register(r, "spawn.spawn_npc", "Spawn an NPC from the runtime catalog. arguments: barcode or query, optional position.", Spawn, action:true, spawn:true);
        Register(r, "spawn.spawn_prop", "Spawn a prop from the runtime catalog. arguments: barcode or query, optional position.", Spawn, action:true, spawn:true);
        Register(r, "spawn.spawn_vehicle", "Spawn a vehicle from the runtime catalog. arguments: barcode or query, optional position.", Spawn, action:true, spawn:true);
        Register(r, "spawn.despawn", "Despawn/destroy a registered spawned or scene object. arguments: objectId.", Despawn, action:true);
        Register(r, "interaction.grab", "Attach a nearby object's grip to a local hand. arguments: objectId, hand ('left' or 'right').", Grab, action:true);
        Register(r, "interaction.release", "Release the object in a local hand. arguments: hand.", Release, action:true);
        Register(r, "interaction.pull_to_hand", "Move an object to a hand and grab it. arguments: objectId, hand.", PullToHand, action:true);
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
        Register(r, "combat.damage", "Apply real Marrow damage to a target through its damage receiver. arguments: objectId, amount.", DamageTarget, action:true, combat:true);
        Register(r, "combat.hit", "Hit a target with a physical impulse. arguments: objectId, force {x,y,z}.", ApplyImpulse, action:true, combat:true);
        Register(r, "combat.punch", "Punch a target with a physical impulse. arguments: objectId, force {x,y,z}.", ApplyImpulse, action:true, combat:true);
        Register(r, "combat.kick", "Kick a target with a physical impulse. arguments: objectId, force {x,y,z}.", ApplyImpulse, action:true, combat:true);
        Register(r, "movement.move_to", "Move toward a world position using short player teleports over updates. arguments: position, optional speed.", MoveTo, action:true);
        Register(r, "movement.navigate_to", "Navigate toward a world position using collision-unaware incremental movement. arguments: position, optional speed.", MoveTo, action:true);
        Register(r, "movement.follow", "Continuously follow a registered object. arguments: objectId, optional speed.", Follow, action:true);
        Register(r, "movement.stop", "Stop active move/follow behavior.", StopMovement, action:true);
        Register(r, "movement.turn", "Turn local player by yaw degrees. arguments: degrees.", Turn, action:true);
        Register(r, "movement.jump", "Apply an upward impulse to the local physics rig. optional arguments: force.", Jump, action:true);
        Register(r, "vehicle.enter", "Enter a vehicle/seat represented by a registered object. arguments: objectId.", EnterVehicle, action:true);
        Register(r, "vehicle.exit", "Exit the local player's current seat.", ExitVehicle, action:true);
        Register(r, "fusion.get_session", "Get detected Fusion state and authority mode.", c => ToolResult.Success(c, _fusion.GetSession()));
        Register(r, "fusion.get_players", "Get Fusion players from the installed Fusion API.", c => ToolResult.Success(c, _fusion.GetPlayers()));
        Register(r, "fusion.get_sync_report", "Explain which action categories use ordinary Fusion replication and which remain local.", c => ToolResult.Success(c, _fusion.GetSynchronizationReport()));
        Register(r, "mods.list_loaded", "List loaded MelonLoader mod assemblies and versions.", LoadedMods);
        Register(r, "mods.get_capabilities", "List useful capabilities discovered in the user's installed mod DLLs and whether the AI integrates them.", ModCapabilities);
        Register(r, "logs.get_recent_errors", "Read recent error/exception lines from the current MelonLoader log. optional arguments: limit.", RecentErrors);
        Register(r, "ui.notify", "Show an in-headset BoneLib notification. arguments: message.", Notify);
    }

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

    public object GetCompactContext() => new { player = SafeState(), fusion = _fusion.GetSession(), nearby = CompactNearby(8f, 12), recentScene = SceneManager.GetActiveScene().name };
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
    private ToolResult SetAvatar(ToolCall c) { var barcode=Str(c,"barcode"); NeedRig().SwapAvatarCrate(new Il2CppSLZ.Marrow.Warehouse.Barcode(barcode),true,null); return ToolResult.Success(c,new{barcode,pending=true}); }
    private ToolResult SetPhysics(ToolCall c)
    {
        var a=Player.Avatar ?? throw new InvalidOperationException("No active avatar.");
        foreach(var map in new[]{("upperStrength","strengthUpper"),("lowerStrength","strengthLower"),("gripStrength","strengthGrip"),("speed","speed"),("agility","agility"),("vitality","vitality")})
            if(c.Arguments[map.Item1]!=null){ _avatarDefaults.TryAdd(map.Item2,Convert.ToSingle(ReadMember(a,map.Item2))); SetMember(a,map.Item2,c.Arguments[map.Item1]!.Value<float>()); }
        return ToolResult.Success(c,new{modified=true});
    }
    private ToolResult RestorePhysics(ToolCall c) { var a=Player.Avatar??throw new InvalidOperationException("No active avatar."); foreach(var x in _avatarDefaults) SetMember(a,x.Key,x.Value); _avatarDefaults.Clear(); return ToolResult.Success(c); }
    private ToolResult ListNearby(ToolCall c) => ToolResult.Success(c,CompactNearby(c.Arguments["radius"]?.Value<float>()??_config.WorldQueryRadius.Value,c.Arguments["limit"]?.Value<int>()??_config.WorldQueryLimit.Value));
    private ToolResult FindObject(ToolCall c) { var q=Str(c,"query"); var all=NearbyObjects(c.Arguments["radius"]?.Value<float>()??_config.WorldQueryRadius.Value,100).OrderBy(x=>Score(x.name,q)).Take(10).Select(x=>Describe(x)).ToArray(); return ToolResult.Success(c,all); }
    private ToolResult ObjectInfo(ToolCall c) { var go=NeedObject(c); return ToolResult.Success(c,Describe(go,true)); }
    private ToolResult SceneInfo(ToolCall c) => ToolResult.Success(c,new{name=SceneManager.GetActiveScene().name,buildIndex=SceneManager.GetActiveScene().buildIndex,nearbyCount=NearbyObjects(_config.WorldQueryRadius.Value,500).Count});

    private ToolResult SearchSpawnables(ToolCall c)
    {
        var query=c.Arguments["query"]?.Value<string>()??string.Empty; var limit=c.Arguments["limit"]?.Value<int>()??20;
        return ToolResult.Success(c,SpawnCatalog().OrderBy(x=>Score(x.title,query)).Take(limit).ToArray());
    }
    private ToolResult RefreshSpawnLab(ToolCall c){_spawnLab.Refresh();return ToolResult.Success(c,new{provider="SpawnLab",count=_spawnLab.GetEntries().Count});}
    private ToolResult Spawn(ToolCall c)
    {
        var request=c.Arguments["barcode"]?.Value<string>()??Str(c,"query");
        if(!_spawnLab.Available)return ToolResult.Failure(c,"SpawnLab 1.0.0 is not loaded; spawning was not attempted.");
        if(c.Arguments["position"]!=null)return ToolResult.Failure(c,"SpawnLab 1.0.0 chooses its verified front-of-player transform and does not expose an arbitrary-position API.");
        var entry=_spawnLab.Spawn(request);
        return ToolResult.Success(c,new{provider="SpawnLab",title=entry.Title,barcode=entry.Barcode,source=entry.Source,pending=true,synchronization=_fusion.IsAvailable?"SpawnLab NetworkAssetSpawner when the Fusion level is networked":"SpawnLab LocalAssetSpawner"});
    }
    private ToolResult Despawn(ToolCall c){var go=NeedObject(c);var poolee=FindComponentByName(go,"Poolee");var method=poolee?.GetType().GetMethod("Despawn",Type.EmptyTypes);if(method!=null){method.Invoke(poolee,null);return ToolResult.Success(c,new{path="Poolee.Despawn",synchronization=_fusion.IsAvailable?"Fusion Poolee despawn patch when network-owned":"offline"});}return ToolResult.Failure(c,"Object is not a pooled spawnable and was not destroyed locally.");}

    private ToolResult Grab(ToolCall c){var go=NeedObject(c); var hand=Hand(c); var grip=go.GetComponentInChildren<Grip>(); if(grip==null)return ToolResult.Failure(c,"No Grip component was found."); grip.Snatch(hand,false); return ToolResult.Success(c,new{objectId=Str(c,"objectId"),hand=HandName(c)});}
    private ToolResult Release(ToolCall c){var h=Hand(c); h.DetachObject(); return ToolResult.Success(c,new{hand=HandName(c)});}
    private ToolResult PullToHand(ToolCall c){var go=NeedObject(c); var h=Hand(c); var grip=go.GetComponentInChildren<Grip>(); if(grip==null)return ToolResult.Failure(c,"No Grip component was found."); go.transform.position=h.transform.position; grip.Snatch(h,false); return ToolResult.Success(c,new{objectId=Str(c,"objectId")});}
    private ToolResult Activate(ToolCall c)
    {
        var go=NeedObject(c); var names=new[]{"OnPress","Press","Activate","Use","Interact","Open"};
        foreach(var component in go.GetComponentsInChildren<Component>()) foreach(var name in names){var m=component.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public,null,Type.EmptyTypes,null); if(m!=null){m.Invoke(component,null); return ToolResult.Success(c,new{component=component.GetType().Name,method=name});}}
        return ToolResult.Failure(c,"No supported public interaction method was exposed by this object.");
    }
    private ToolResult ApplyForce(ToolCall c)=>RigidAction(c,rb=>rb.AddForce(Vec(c.Arguments["force"]),ForceMode.Force));
    private ToolResult ApplyImpulse(ToolCall c)=>RigidAction(c,rb=>rb.AddForce(Vec(c.Arguments["force"]),ForceMode.Impulse));
    private ToolResult SetVelocity(ToolCall c)=>RigidAction(c,rb=>rb.velocity=Vec(c.Arguments["velocity"]));
    private ToolResult MoveObject(ToolCall c)=>RigidAction(c,rb=>rb.MovePosition(Vec(c.Arguments["position"])));
    private ToolResult RotateObject(ToolCall c)=>RigidAction(c,rb=>rb.MoveRotation(Quaternion.Euler(Vec(c.Arguments["rotation"]))));
    private ToolResult RigidAction(ToolCall c,Action<Rigidbody> action){var rb=NeedObject(c).GetComponentInChildren<Rigidbody>()??throw new InvalidOperationException("Object has no Rigidbody."); action(rb); return ToolResult.Success(c,new{objectId=Str(c,"objectId")});}

    private ToolResult Aim(ToolCall c){var target=NeedObject(c); var held=HeldObject(Hand(c))??throw new InvalidOperationException("Hand is empty."); held.transform.rotation=Quaternion.LookRotation(target.transform.position-held.transform.position,Vector3.up); return ToolResult.Success(c);}
    private ToolResult Shoot(ToolCall c){var held=HeldObject(Hand(c))??throw new InvalidOperationException("Hand is empty."); var gun=FindComponentByName(held,"Gun")??throw new InvalidOperationException("Held object has no Gun component."); var fire=gun.GetType().GetMethod("Fire",Type.EmptyTypes)??throw new MissingMethodException("Gun.Fire"); var shots=Math.Clamp(c.Arguments["shots"]?.Value<int>()??1,1,20); for(var i=0;i<shots;i++)fire.Invoke(gun,null); return ToolResult.Success(c,new{shots,synchronization=_fusion.IsAvailable?"ordinary Fusion gun-shot patch":"offline"});}
    private ToolResult Reload(ToolCall c){var held=HeldObject(Hand(c))??throw new InvalidOperationException("Hand is empty."); var gun=FindComponentByName(held,"Gun")??throw new InvalidOperationException("Held object has no Gun component."); var m=gun.GetType().GetMethod("InstantLoadAsync",Type.EmptyTypes)??throw new MissingMethodException("Gun.InstantLoadAsync"); m.Invoke(gun,null); return ToolResult.Success(c);}
    private ToolResult DamageTarget(ToolCall c)
    {
        var go=NeedObject(c); var amount=Num(c,"amount");
        foreach(var comp in go.GetComponentsInChildren<Component>()){var m=comp.GetType().GetMethod("TAKEDAMAGE",new[]{typeof(float)})??comp.GetType().GetMethod("TakeDamage",new[]{typeof(float)}); if(m!=null){m.Invoke(comp,new object[]{amount}); return ToolResult.Success(c,new{component=comp.GetType().Name,amount});}}
        return ToolResult.Failure(c,"Target exposes no supported damage receiver.");
    }
    private ToolResult MoveTo(ToolCall c){_moveDestination=Vec(c.Arguments["position"]);_followObject=null;_moveSpeed=c.Arguments["speed"]?.Value<float>()??2.5f;return ToolResult.Success(c,new{started=true,destination=V(_moveDestination.Value)});}
    private ToolResult Follow(ToolCall c){NeedObject(c);_followObject=Str(c,"objectId");_moveDestination=null;_moveSpeed=c.Arguments["speed"]?.Value<float>()??2.5f;return ToolResult.Success(c,new{started=true});}
    private ToolResult StopMovement(ToolCall c){_moveDestination=null;_followObject=null;return ToolResult.Success(c);}
    private ToolResult Turn(ToolCall c){var rig=NeedRig();var e=rig.transform.eulerAngles;e.y+=Num(c,"degrees");rig.Teleport(rig.transform.position,e,true);return ToolResult.Success(c,new{yaw=e.y});}
    private ToolResult Jump(ToolCall c){var force=c.Arguments["force"]?.Value<float>()??4.5f;var bodies=NeedRig().GetComponentsInChildren<Rigidbody>();if(bodies.Length==0)return ToolResult.Failure(c,"Physics rig has no rigidbodies.");foreach(var rb in bodies)rb.AddForce(Vector3.up*force,ForceMode.VelocityChange);return ToolResult.Success(c,new{force,rigidbodies=bodies.Length});}
    private ToolResult EnterVehicle(ToolCall c){var go=NeedObject(c);var seat=FindComponentByName(go,"Seat")??throw new InvalidOperationException("No Seat component found.");var m=seat.GetType().GetMethod("IngressRig")??throw new MissingMethodException("Seat.IngressRig");m.Invoke(seat,new object[]{NeedRig()});return ToolResult.Success(c,new{synchronization=_fusion.IsAvailable?"ordinary Fusion seat patch":"offline"});}
    private ToolResult ExitVehicle(ToolCall c){var seat=NeedRig().activeSeat;if(seat==null)return ToolResult.Failure(c,"Player is not seated.");seat.EgressRig(false);return ToolResult.Success(c);}
    private ToolResult LoadedMods(ToolCall c)=>ToolResult.Success(c,AppDomain.CurrentDomain.GetAssemblies().Select(a=>new{name=a.GetName().Name,version=a.GetName().Version?.ToString()}).Where(x=>!string.IsNullOrWhiteSpace(x.name)).OrderBy(x=>x.name).ToArray());
    private ToolResult ModCapabilities(ToolCall c)
    {
        bool Has(string name)=>AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name==name);
        return ToolResult.Success(c,new object[]{
            new{name="SpawnLab",loaded=Has("SpawnLab"),usable=true,integration="spawn catalog and Fusion-aware spawn path"},
            new{name="LabFusion",loaded=Has("LabFusion"),usable=true,integration="session/player queries and existing action replication"},
            new{name="Force Pull Anything",loaded=Has("Force Pull Anything"),usable=true,integration="its Grip patch automatically improves agent grip pulls"},
            new{name="StrengthMod",loaded=Has("StrengthMod"),usable=true,integration="coexists; agent runtime avatar overrides remain separate"},
            new{name="Stat Changer",loaded=Has("Stat_Changer__Simple_Edition_"),usable=true,integration="verified speed/jump fields informed agent modifiers"},
            new{name="QuickItem",loaded=Has("QuickItem"),usable=false,integration="private saved-item workflow; SpawnLab is used instead"},
            new{name="PowerTools",loaded=Has("PowerTools"),usable=false,integration="global cheats deliberately not changed implicitly"},
            new{name="Portals",loaded=Has("Portals"),usable=false,integration="portal spawning is local AssetSpawner and not claimed network-safe"},
            new{name="Echolocation",loaded=Has("Echolocation"),usable=false,integration="internal perception cache is non-public; Fusion players queried directly"},
            new{name="ModioModNetworker",loaded=Has("ModioModNetworker"),usable=true,integration="automatically observes Fusion spawn traffic; no direct calls required"},
            new{name="WristHub",loaded=Has("WristHub.Core"),usable=false,integration="large UI/system surface intentionally isolated from AI actions"}
        });
    }
    private ToolResult RecentErrors(ToolCall c){var path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MelonLoader","Latest.log");if(!File.Exists(path))return ToolResult.Failure(c,"Latest.log was not found.");var limit=c.Arguments["limit"]?.Value<int>()??40;var lines=File.ReadLines(path).Where(x=>x.Contains("error",StringComparison.OrdinalIgnoreCase)||x.Contains("exception",StringComparison.OrdinalIgnoreCase)).TakeLast(limit).ToArray();return ToolResult.Success(c,lines);}
    private ToolResult Notify(ToolCall c){Notifier.Send(new Notification{Title="BONELAB AI Agent",Message=Str(c,"message"),ShowTitleOnPopup=true,Type=NotificationType.Information,PopupLength=4});return ToolResult.Success(c);}

    public void Update()
    {
        _objects.Prune(); if(_followObject!=null&&_objects.TryGet(_followObject,out var target))_moveDestination=target.transform.position-target.transform.forward*1.5f;
        if(_moveDestination is not Vector3 destination||Player.RigManager==null)return; var rig=Player.RigManager;var current=rig.transform.position;var flat=new Vector3(destination.x,current.y,destination.z);if(Vector3.Distance(current,flat)<0.15f){if(_followObject==null)_moveDestination=null;return;}var next=Vector3.MoveTowards(current,flat,_moveSpeed*Time.deltaTime);rig.Teleport(next,rig.transform.eulerAngles,true);
    }

    private List<GameObject> NearbyObjects(float radius,int limit){var center=Player.Head?.transform.position??Vector3.zero;var seen=new HashSet<int>();var result=new List<GameObject>();foreach(var hit in Physics.OverlapSphere(center,Mathf.Clamp(radius,0.5f,50f))){var go=hit.attachedRigidbody?.gameObject??hit.gameObject;if(seen.Add(go.GetInstanceID()))result.Add(go);if(result.Count>=Math.Clamp(limit,1,500))break;}return result;}
    private object[] CompactNearby(float radius,int limit)=>NearbyObjects(radius,limit).Select(x=>Describe(x)).ToArray();
    private object Describe(GameObject go,bool detailed=false){var types=go.GetComponentsInChildren<Component>().Select(x=>x.GetType().Name).Distinct().Where(x=>detailed||new[]{"Rigidbody","Grip","Gun","PuppetMaster","Seat","ButtonNode","LeverNode","MarrowEntity"}.Contains(x)).Take(detailed?30:8).ToArray();var head=Player.Head?.transform.position??Vector3.zero;return new{id=_objects.Register(go),name=go.name,position=V(go.transform.position),distance=Math.Round(Vector3.Distance(head,go.transform.position),2),types};}
    private IEnumerable<(string title,string barcode,string source,string category,bool downloaded)> SpawnCatalog()=>_spawnLab.GetEntries().Select(x=>(x.Title,x.Barcode,x.Source,x.Category,x.Downloaded));
    private static int Score(string value,string query){if(string.IsNullOrWhiteSpace(query))return 0;value=value.ToLowerInvariant();query=query.ToLowerInvariant();if(value==query)return 0;if(value.Contains(query))return 1+value.IndexOf(query);return Levenshtein(value,query)+20;}
    private static int Levenshtein(string a,string b){var d=new int[b.Length+1];for(var j=0;j<=b.Length;j++)d[j]=j;for(var i=1;i<=a.Length;i++){var prev=d[0];d[0]=i;for(var j=1;j<=b.Length;j++){var old=d[j];d[j]=Math.Min(Math.Min(d[j]+1,d[j-1]+1),prev+(a[i-1]==b[j-1]?0:1));prev=old;}}return d[b.Length];}
    private GameObject NeedObject(ToolCall c){var id=Str(c,"objectId");if(!_objects.TryGet(id,out var go))throw new InvalidOperationException("Object handle is missing or expired: "+id);return go;}
    private static RigManager NeedRig()=>Player.RigManager??throw new InvalidOperationException("Local player rig is unavailable.");
    private static Hand Hand(ToolCall c)=>HandName(c)=="left"?(Player.LeftHand??throw new InvalidOperationException("Left hand unavailable.")):(Player.RightHand??throw new InvalidOperationException("Right hand unavailable."));
    private static string HandName(ToolCall c)=>(c.Arguments["hand"]?.Value<string>()??"right").ToLowerInvariant();
    private static GameObject? HeldObject(Hand hand)=>hand.m_CurrentAttachedGO;
    private static string? HeldName(Hand? hand)=>hand==null?null:HeldObject(hand)?.name;
    private static Component? FindComponentByName(GameObject go,string name)=>go.GetComponentsInChildren<Component>().FirstOrDefault(x=>x.GetType().Name==name);
    private static string Str(ToolCall c,string key)=>c.Arguments[key]?.Value<string>()??throw new ArgumentException("Missing string argument: "+key);
    private static float Num(ToolCall c,string key)=>c.Arguments[key]?.Value<float>()??throw new ArgumentException("Missing numeric argument: "+key);
    private static Vector3 Vec(JToken? token,Vector3? fallback=null)=>token==null?(fallback??throw new ArgumentException("Missing vector.")):new Vector3(token["x"]?.Value<float>()??0,token["y"]?.Value<float>()??0,token["z"]?.Value<float>()??0);
    private static object V(Vector3 v)=>new{x=Math.Round(v.x,3),y=Math.Round(v.y,3),z=Math.Round(v.z,3)};
    private static object? ReadMember(object? o,string name){if(o==null)return null;var t=o.GetType();return t.GetProperty(name)?.GetValue(o)??t.GetField(name)?.GetValue(o);}
    private static void SetMember(object o,string name,object value){var t=o.GetType();var p=t.GetProperty(name);if(p!=null){p.SetValue(o,Convert.ChangeType(value,p.PropertyType));return;}var f=t.GetField(name)??throw new MissingMemberException(t.FullName,name);f.SetValue(o,Convert.ChangeType(value,f.FieldType));}
}
