using BonelabAIAgent.AI;
using BonelabAIAgent.Fusion;
using BonelabAIAgent.Game;
using BonelabAIAgent.Infrastructure;
using BonelabAIAgent.Tools;
using BonelabAIAgent.UI;
using MelonLoader;

namespace BonelabAIAgent;

public sealed class AIAgentMod : MelonMod
{
    public static AIAgentMod Instance { get; private set; } = null!;
    public AgentConfig Config { get; private set; } = null!;
    public MainThreadDispatcher Dispatcher { get; } = new();
    public ToolRegistry Tools { get; private set; } = null!;
    public GameToolset Game { get; private set; } = null!;
    public FusionBridge Fusion { get; private set; } = null!;
    public ConversationManager Conversation { get; private set; } = null!;
    public AgentMenu Menu { get; private set; } = null!;
    public PythonBridgeManager PythonBridge { get; private set; } = null!;

    public override void OnInitializeMelon()
    {
        Instance = this;
        Config = new AgentConfig();
        AgentLog.Verbose = Config.DebugLogging.Value;
        AgentLog.Info("Starting BONELAB AI Agent 1.0.4");
        AgentLog.Info($"Unity {UnityEngine.Application.unityVersion}; BONELAB build {UnityEngine.Application.version}");

        Fusion = new FusionBridge();
        Fusion.Initialize();
        Tools = new ToolRegistry(Dispatcher);
        Game = new GameToolset(Config, Fusion);
        Game.RegisterTools(Tools);
        Conversation = new ConversationManager(Config, Tools, Game);
        PythonBridge = new PythonBridgeManager();
        Menu = new AgentMenu(this);
        Menu.Create();
        _ = StartBackendAsync();
    }

    public override void OnUpdate()
    {
        Dispatcher.Drain();
        Game.Update();
        Menu.Refresh();
    }

    public override void OnDeinitializeMelon()
    {
        try { Conversation.Dispose(); }
        catch (Exception ex) { AgentLog.Exception("shutdown", ex); }
        try { PythonBridge.Dispose(); }
        catch (Exception ex) { AgentLog.Exception("Python bridge shutdown", ex); }
    }

    private async Task StartBackendAsync()
    {
        try
        {
            await PythonBridge.EnsureStartedAsync(Config.Endpoint.Value, Config.AutoStartPythonBridge.Value).ConfigureAwait(false);
            await Conversation.ConnectAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AgentLog.Exception("backend startup", ex); }
    }
}
