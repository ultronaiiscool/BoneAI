using MelonLoader;

namespace BoneAI.Infrastructure;

public sealed class AgentConfig
{
    public MelonPreferences_Entry<bool> Enabled { get; }
    public MelonPreferences_Entry<bool> AllowActions { get; }
    public MelonPreferences_Entry<bool> AllowPlayerModification { get; }
    public MelonPreferences_Entry<bool> AllowSpawning { get; }
    public MelonPreferences_Entry<bool> AllowCombat { get; }
    public MelonPreferences_Entry<bool> FusionSynchronization { get; }
    public MelonPreferences_Entry<bool> ShowActivity { get; }
    public MelonPreferences_Entry<bool> DebugLogging { get; }
    public MelonPreferences_Entry<string> Endpoint { get; }
    public MelonPreferences_Entry<int> TimeoutSeconds { get; }
    public MelonPreferences_Entry<float> WorldQueryRadius { get; }
    public MelonPreferences_Entry<int> WorldQueryLimit { get; }
    public MelonPreferences_Entry<string> ConversationThreadId { get; }
    public MelonPreferences_Entry<bool> AutoStartPythonBridge { get; }
    public MelonPreferences_Entry<string> Provider { get; }
    public MelonPreferences_Entry<string> ProviderModel { get; }
    public MelonPreferences_Entry<string> ProviderBaseUrl { get; }
    public MelonPreferences_Entry<string> ProviderConversationId { get; }

    public AgentConfig()
    {
        var category = MelonPreferences.CreateCategory("BoneAI");
        Enabled = category.CreateEntry("Enabled", true);
        AllowActions = category.CreateEntry("AllowGameplayActions", true);
        AllowPlayerModification = category.CreateEntry("AllowPlayerModification", true);
        AllowSpawning = category.CreateEntry("AllowSpawning", true);
        AllowCombat = category.CreateEntry("AllowCombatActions", true);
        FusionSynchronization = category.CreateEntry("FusionSynchronization", true);
        ShowActivity = category.CreateEntry("ShowAgentActivity", true);
        DebugLogging = category.CreateEntry("DebugLogging", false);
        Endpoint = category.CreateEntry("CodexAppServerEndpoint", "ws://127.0.0.1:4500");
        TimeoutSeconds = category.CreateEntry("RequestTimeoutSeconds", 120);
        WorldQueryRadius = category.CreateEntry("WorldQueryRadius", 12f);
        WorldQueryLimit = category.CreateEntry("WorldQueryLimit", 40);
        ConversationThreadId = category.CreateEntry("CodexThreadId", string.Empty);
        AutoStartPythonBridge = category.CreateEntry("AutoStartPythonBridge", true);
        Provider = category.CreateEntry("Provider", "Codex");
        ProviderModel = category.CreateEntry("ProviderModel", string.Empty);
        ProviderBaseUrl = category.CreateEntry("ProviderBaseUrl", string.Empty);
        ProviderConversationId = category.CreateEntry("ProviderConversationId", string.Empty);
    }
}
