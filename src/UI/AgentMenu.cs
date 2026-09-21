using BoneLib.BoneMenu;
using BoneLib.Notifications;
using UnityEngine;

namespace BoneAI.UI;

public sealed class AgentMenu
{
    private readonly BoneAIMod _mod;
    private StringElement? _prompt;
    private StringElement? _status;
    private StringElement? _activity;
    private StringElement? _response;
    private float _nextRefresh;

    public AgentMenu(BoneAIMod mod) => _mod = mod;

    public void Create()
    {
        var page = Page.Root.CreatePage("BoneAI", Color.cyan);
        _prompt = page.CreateString("Prompt", Color.white, "Ask or command the agent", _ => { });
        page.CreateFunction("Send", Color.green, () =>
        {
            var text = _prompt?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return;
            if (_prompt != null) _prompt.Value = string.Empty;
            _ = _mod.Conversation.SendAsync(text);
        });
        page.CreateFunction("Cancel Request", Color.yellow, _mod.Conversation.Cancel);
        page.CreateFunction("Reconnect", Color.cyan, () => _ = _mod.Conversation.ConnectAsync());
        page.CreateFunction("New Conversation", Color.cyan, () => _ = _mod.Conversation.NewConversationAsync());
        page.CreateFunction("Enable All Game Controls", Color.green, () =>
        {
            _mod.Config.Enabled.Value = true;
            _mod.Config.AllowActions.Value = true;
            _mod.Config.AllowPlayerModification.Value = true;
            _mod.Config.AllowSpawning.Value = true;
            _mod.Config.AllowCombat.Value = true;
            _mod.Config.FusionSynchronization.Value = true;
            Notifier.Send(new Notification { Title = "BoneAI", Message = "All game controls enabled.", ShowTitleOnPopup = true, PopupLength = 4, Type = NotificationType.Information });
        });
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
            Notifier.Send(new Notification { Title = "BoneAI", Message = Trim(_mod.Conversation.LastResponse, 480), ShowTitleOnPopup = true, PopupLength = 8, Type = NotificationType.Information });
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
