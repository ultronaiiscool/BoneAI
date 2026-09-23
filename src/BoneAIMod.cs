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
    public CodexHostManager CodexHost { get; private set; } = null!;
    public QuestCodexHostManager QuestCodexHost { get; private set; } = null!;
    public VoiceAssistant Voice { get; private set; } = null!;
    public BrowserVoiceServer BrowserVoice { get; private set; } = null!;

    public override void OnInitializeMelon()
    {
        Instance = this;
        Config = new AgentConfig();
        AgentLog.Verbose = Config.DebugLogging.Value;
        RuntimeSecrets.Initialize();
        AgentLog.Info("Starting BoneAI 3.0.0 on " + PlatformInfo.DisplayName);
        AgentLog.Info($"Unity {UnityEngine.Application.unityVersion}; BONELAB build {UnityEngine.Application.version}");

        Fusion = new FusionBridge();
        Fusion.Initialize();
        Tools = new ToolRegistry(Dispatcher);
        Tools.RegisterDiscoveryTools();
        Game = new GameToolset(Config, Fusion);
        Game.RegisterTools(Tools);
        QuestCodexHost = new QuestCodexHostManager(Dispatcher);
        Conversation = new ConversationManager(Config, Tools, Game, EnsureCodexHostAsync, () => QuestCodexHost.BearerToken);
        Voice = new VoiceAssistant(Config, Conversation, Dispatcher);
        BrowserVoice = new BrowserVoiceServer(Conversation, Config);
        CodexHost = new CodexHostManager();
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
        try { CodexHost.Dispose(); }
        catch (Exception ex) { AgentLog.Exception("Codex host shutdown", ex); }
        try { QuestCodexHost.Dispose(); }
        catch (Exception ex) { AgentLog.Exception("Quest Codex host shutdown", ex); }
        try { Voice.Dispose(); }
        catch (Exception ex) { AgentLog.Exception("voice shutdown", ex); }
        try { BrowserVoice.Dispose(); }
        catch (Exception ex) { AgentLog.Exception("browser voice shutdown", ex); }
    }

    private async Task StartBackendAsync()
    {
        try
        {
            if (Config.Provider.Value.Equals("Codex", StringComparison.OrdinalIgnoreCase))
            {
                await EnsureCodexHostAsync().ConfigureAwait(false);
            }
            await Conversation.ConnectAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AgentLog.Exception("backend startup", ex); }
    }

    public async Task<bool> EnsureCodexHostAsync()
    {
        if (PlatformInfo.IsAndroid)
        {
            var questReady = await QuestCodexHost.EnsureStartedAsync().ConfigureAwait(false);
            if (questReady) Config.Endpoint.Value = QuestCodexHost.Endpoint;
            return questReady;
        }
        var ready = await CodexHost.EnsureStartedAsync(Config.Endpoint.Value, Config.AutoStartCodexHost.Value).ConfigureAwait(false);
        if (ready && !string.IsNullOrWhiteSpace(CodexHost.Endpoint)) Config.Endpoint.Value = CodexHost.Endpoint;
        return ready;
    }
}
