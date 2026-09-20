using BoneLib.BoneMenu;
using BoneLib.Notifications;
using UnityEngine;

namespace BonelabAIAgent.UI;

public sealed class AgentMenu
{
    private readonly AIAgentMod _mod;
    private StringElement? _prompt;
    private StringElement? _status;
    private StringElement? _activity;
    private StringElement? _response;
    private float _nextRefresh;

    public AgentMenu(AIAgentMod mod) => _mod = mod;

    public void Create()
    {
        var page = Page.Root.CreatePage("AI Agent", Color.cyan);
        _prompt = page.CreateString("Prompt", Color.white, "Ask or command the agent", _ => { });
        page.CreateFunction("Send", Color.green, () => _ = _mod.Conversation.SendAsync(_prompt?.Value ?? string.Empty));
        page.CreateFunction("Cancel Request", Color.yellow, _mod.Conversation.Cancel);
        page.CreateFunction("Reconnect", Color.cyan, () => _ = _mod.Conversation.ConnectAsync());
        page.CreateFunction("New Conversation", Color.cyan, () => _ = _mod.Conversation.NewConversationAsync());
        Bind(page, "Agent Enabled", _mod.Config.Enabled);
        Bind(page, "Allow Gameplay Actions", _mod.Config.AllowActions);
        Bind(page, "Allow Player Modification", _mod.Config.AllowPlayerModification);
        Bind(page, "Allow Spawning", _mod.Config.AllowSpawning);
        Bind(page, "Allow Combat Actions", _mod.Config.AllowCombat);
        Bind(page, "Fusion Synchronization", _mod.Config.FusionSynchronization);
        Bind(page, "Show Agent Activity", _mod.Config.ShowActivity);
        Bind(page, "Auto-start Python Bridge", _mod.Config.AutoStartPythonBridge);
        Bind(page, "Debug Logging", _mod.Config.DebugLogging, value => Infrastructure.AgentLog.Verbose = value);
        _status = page.CreateString("Connection Status", Color.gray, "Starting", _ => { });
        _activity = page.CreateString("Current Action", Color.gray, "Idle", _ => { });
        _response = page.CreateString("Last Response", Color.white, "None", _ => { });
        page.CreateFunction("Show Last Response", Color.white, () =>
        {
            Notifier.Send(new Notification { Title = "AI Assistant", Message = Trim(_mod.Conversation.LastResponse, 480), ShowTitleOnPopup = true, PopupLength = 8, Type = NotificationType.Information });
        });
    }

    public void Refresh()
    {
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + 0.5f;
        if (_status != null) _status.Value = Trim(_mod.Conversation.Status, 80);
        if (_activity != null) _activity.Value = Trim(_mod.Conversation.CurrentAction, 80);
        if (_response != null) _response.Value = Trim(_mod.Conversation.LastResponse, 120);
    }

    private static void Bind(Page page, string name, MelonLoader.MelonPreferences_Entry<bool> entry, Action<bool>? extra = null)
        => page.CreateBool(name, Color.white, entry.Value, value => { entry.Value = value; extra?.Invoke(value); });
    private static string Trim(string? value, int max) => string.IsNullOrWhiteSpace(value) ? "None" : value!.Length <= max ? value : value.Substring(0, max - 1) + "…";
}
