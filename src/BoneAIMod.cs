using BoneAI.AI;
using BoneAI.Fusion;
using BoneAI.Game;
using BoneAI.Infrastructure;
using BoneAI.Tools;
using BoneAI.UI;
using MelonLoader;

namespace BoneAI;

public sealed class BoneAIMod : MelonMod
{
    public static BoneAIMod Instance { get; private set; } = null!;
    public AgentConfig Config { get; private set; } = null!;
    public MainThreadDispatcher Dispatcher { get; } = new();
    public ToolRegistry Tools { get; private set; } = null!;
    public GameToolset Game { get; private set; } = null!;
    public FusionBridge Fusion { get; private set; } = null!;
    public ConversationManager Conversation { get; private set; } = null!;
    public AgentMenu Menu { get; private set; } = null!;
    public PythonBridgeManager PythonBridge { get; private set; } = null!;
    public VoiceAssistant Voice { get; private set; } = null!;

    public override void OnInitializeMelon()
    {
        Instance = this;
        Config = new AgentConfig();
        AgentLog.Verbose = Config.DebugLogging.Value;
        AgentLog.Info("Starting BoneAI 2.5.0 on " + PlatformInfo.DisplayName);
        AgentLog.Info($"Unity {UnityEngine.Application.unityVersion}; BONELAB build {UnityEngine.Application.version}");

        Fusion = new FusionBridge();
        Fusion.Initialize();
        Tools = new ToolRegistry(Dispatcher);
        Tools.RegisterDiscoveryTools();
        Game = new GameToolset(Config, Fusion);
        Game.RegisterTools(Tools);
        Conversation = new ConversationManager(Config, Tools, Game);
        Voice = new VoiceAssistant(Config, Conversation, Dispatcher);
        PythonBridge = new PythonBridgeManager();
        Menu = new AgentMenu(this);
        Menu.Create();
        _ = StartBackendAsync();
    }

    public override void OnUpdate()
    {
        Dispatcher.Drain();
        Game.Update();
        Voice.Update();
        Menu.Refresh();
    }

    public override void OnDeinitializeMelon()
    {
        try { Menu.Dispose(); }
        catch (Exception ex) { AgentLog.Exception("menu shutdown", ex); }
        try { Conversation.Dispose(); }
        catch (Exception ex) { AgentLog.Exception("shutdown", ex); }
        try { PythonBridge.Dispose(); }
        catch (Exception ex) { AgentLog.Exception("Python bridge shutdown", ex); }
        try { Voice.Dispose(); }
        catch (Exception ex) { AgentLog.Exception("voice shutdown", ex); }
    }

    private async Task StartBackendAsync()
    {
        try
        {
            if (!PlatformInfo.IsAndroid && Config.Provider.Value.Equals("Codex", StringComparison.OrdinalIgnoreCase))
                await PythonBridge.EnsureStartedAsync(Config.Endpoint.Value, Config.AutoStartPythonBridge.Value).ConfigureAwait(false);
            await Conversation.ConnectAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AgentLog.Exception("backend startup", ex); }
    }
}
