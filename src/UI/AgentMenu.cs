using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.BoneMenu.UI;
using BoneLib.Notifications;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using UIRig = Il2CppSLZ.Bonelab.UIRig;

namespace BoneAI.UI;

public sealed class AgentMenu
{
    private readonly BoneAIMod _mod;
    private Page? _root;
    private Page? _conversations;
    private StringElement? _prompt;
    private StringElement? _status;
    private StringElement? _activity;
    private StringElement? _response;
    private StringElement? _voiceStatus;
    private StringElement? _transcript;
    private GameObject? _nativeButton;
    private float _nextRefresh;
    private int _conversationCount = -1;

    public AgentMenu(BoneAIMod mod) => _mod = mod;

    public void Create()
    {
        var orange = new Color(1f, 0.38f, 0.03f);
        _root = new Page(Page.Root, "BoneAI", orange, 9) { BackgroundOpacity = 0.92f };
        var assistant = _root.CreatePage("Assistant", orange, 9);
        _prompt = assistant.CreateString("Prompt", Color.white, "Ask or command BoneAI", _ => { });
        assistant.CreateFunction("Send", Color.green, SendPrompt);
        assistant.CreateFunction("Listen Once (Voice Beta)", orange, _mod.Voice.ListenOnce);
        assistant.CreateFunction("Cancel Request", Color.yellow, _mod.Conversation.Cancel);
        _status = assistant.CreateString("Connection", Color.gray, "Starting", _ => { });
        _activity = assistant.CreateString("Current Action", Color.gray, "Idle", _ => { });
        _response = assistant.CreateString("Last Response", Color.white, "None", _ => { });
        assistant.CreateFunction("Show Full Response", Color.white, ShowResponse);

        _conversations = _root.CreatePage("Saved Conversations", new Color(0.35f, 0.8f, 1f), 8);
        BuildConversations();

        var voice = _root.CreatePage("Voice AI (Beta)", orange, 9);
        Bind(voice, "Voice AI Beta", _mod.Config.VoiceBetaEnabled);
        Bind(voice, "Require Wake Word", _mod.Config.VoiceWakeWordEnabled);
        voice.CreateString("Wake Word", Color.white, _mod.Config.VoiceWakeWord.Value, v => _mod.Config.VoiceWakeWord.Value = string.IsNullOrWhiteSpace(v) ? "Hey BoneAI" : v.Trim());
        Bind(voice, "Speak AI Replies", _mod.Config.VoiceSpeakResponses);
        voice.CreateFunction("Listen Once", Color.green, _mod.Voice.ListenOnce);
        _voiceStatus = voice.CreateString("Voice Status", Color.gray, "Voice beta off", _ => { });
        _transcript = voice.CreateString("Last Transcript", Color.white, "None", _ => { });
        voice.CreateFunction("Voice Setup Help", Color.yellow, () => Notify("Voice Beta", "Set OPENAI_API_KEY in Windows and restart BONELAB. Voice is experimental and uses OpenAI audio APIs."));

        var provider = _root.CreatePage("AI Provider", new Color(0.55f, 0.55f, 1f), 9);
        provider.CreateFunction("Change Provider", Color.cyan, CycleProvider);
        provider.CreateString("Provider", Color.white, _mod.Config.Provider.Value, v => _mod.Config.Provider.Value = v);
        provider.CreateString("Model", Color.white, _mod.Config.ProviderModel.Value, v => _mod.Config.ProviderModel.Value = v);
        provider.CreateString("Custom Base URL", Color.white, _mod.Config.ProviderBaseUrl.Value, v => _mod.Config.ProviderBaseUrl.Value = v);
        provider.CreateFunction("Reconnect", Color.green, () => _ = _mod.Conversation.ConnectAsync());
        provider.CreateFunction("New Conversation", Color.cyan, () => _ = _mod.Conversation.NewConversationAsync());

        var permissions = _root.CreatePage("Game Permissions", Color.yellow, 9);
        permissions.CreateFunction("Enable All Game Controls", Color.green, EnableAll);
        Bind(permissions, "Agent Enabled", _mod.Config.Enabled);
        Bind(permissions, "Gameplay Actions", _mod.Config.AllowActions);
        Bind(permissions, "Player Modification", _mod.Config.AllowPlayerModification);
        Bind(permissions, "Spawning", _mod.Config.AllowSpawning);
        Bind(permissions, "Combat", _mod.Config.AllowCombat);
        Bind(permissions, "Fusion Sync", _mod.Config.FusionSynchronization);
        Bind(permissions, "Show Activity", _mod.Config.ShowActivity);

        var diagnostics = _root.CreatePage("Diagnostics", Color.gray, 8);
        diagnostics.CreateString("Tool Catalog", Color.white, _mod.Tools.Count + " tools", _ => { });
        diagnostics.CreateString("Fusion", Color.white, _mod.Fusion.IsOnline ? "Online" : _mod.Fusion.IsAvailable ? "Installed / offline" : "Not installed", _ => { });
        Bind(diagnostics, "Auto-start Bridge", _mod.Config.AutoStartPythonBridge);
        Bind(diagnostics, "Debug Logging", _mod.Config.DebugLogging, v => Infrastructure.AgentLog.Verbose = v);
        diagnostics.CreateFunction("Reconnect", Color.green, () => _ = _mod.Conversation.ConnectAsync());

        _root.CreateFunction("Close BoneAI", Color.red, Close);
        Hooking.OnUIRigCreated += AddPreferencesButton;
        AddPreferencesButton();
    }

    public void Refresh()
    {
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + 0.5f;
        if (_status != null) _status.Value = Trim(_mod.Conversation.Status, 80);
        if (_activity != null) _activity.Value = Trim(_mod.Conversation.CurrentAction, 80);
        if (_response != null) _response.Value = Trim(_mod.Conversation.LastResponse, 120);
        if (_voiceStatus != null) _voiceStatus.Value = Trim(_mod.Voice.Status, 100);
        if (_transcript != null) _transcript.Value = Trim(_mod.Voice.LastTranscript, 100);
        if (_conversationCount != _mod.Conversation.SavedConversationCount) BuildConversations();
    }

    private void AddPreferencesButton()
    {
        try
        {
            if (_nativeButton != null && !_nativeButton.WasCollected) return;
            var rig = UIRig.Instance; if (rig == null) return;
            var grid = rig.popUpMenu.preferencesPanelView.transform.Find("page_OPTIONS/grid_Options");
            var source = grid?.Find("button_Control")?.gameObject; if (source == null) return;
            _nativeButton = GameObject.Instantiate(source, source.transform.parent, false); _nativeButton.name = "button_BoneAI";
            var quit = grid!.Find("button_Quit"); if (quit != null) _nativeButton.transform.SetSiblingIndex(Math.Max(0, quit.GetSiblingIndex() - 1));
            var label = _nativeButton.GetComponentInChildren<TMP_Text>(true); if (label != null) { label.text = "BoneAI"; label.gameObject.name = "text_BoneAI"; }
            var button = _nativeButton.GetComponent<Button>(); button.onClick = new Button.ButtonClickedEvent(); button.onClick.AddListener(new Action(Open));
            Infrastructure.AgentLog.Info("Standalone BoneAI Preferences button created.");
        }
        catch (Exception ex) { Infrastructure.AgentLog.Exception("Preferences button", ex); }
    }

    private void Open()
    {
        var panel = UIRig.Instance?.popUpMenu?.preferencesPanelView; if (panel == null || _root == null) return;
        for (var i = 0; i < panel.pages.Length; i++) if (panel.pages[i] == GUIMenu.Instance.gameObject) { panel.PAGESELECT(i); var bg = panel.transform.Find("image_bgFade"); if (bg != null) bg.gameObject.SetActive(false); Menu.OpenPage(_root); return; }
        Notify("BoneAI", "BoneLib's menu page is not ready yet. Close Preferences and try again.");
    }

    private void Close()
    {
        var panel = UIRig.Instance?.popUpMenu?.preferencesPanelView; if (panel == null) return;
        panel.PAGESELECT(panel.defaultPage); var bg = panel.transform.Find("image_bgFade"); if (bg != null) bg.gameObject.SetActive(true);
    }

    private void BuildConversations()
    {
        if (_conversations == null) return;
        var items = _mod.Conversation.SavedConversations.Take(20).ToArray(); _conversationCount = items.Length;
        _conversations.RemoveAll();
        _conversations.CreateFunction("New Conversation", Color.green, () => _ = _mod.Conversation.NewConversationAsync());
        foreach (var saved in items) { var copy = saved; _conversations.CreateFunction(Trim(copy.Title, 38), Color.white, () => _ = _mod.Conversation.OpenConversationAsync(copy)); }
        if (items.Length == 0) _conversations.CreateFunction("No saved conversations yet", Color.gray, () => { });
    }

    private void SendPrompt() { var value = _prompt?.Value ?? string.Empty; if (string.IsNullOrWhiteSpace(value)) return; if (_prompt != null) _prompt.Value = string.Empty; _ = _mod.Conversation.SendAsync(value); }
    private void ShowResponse() => Notify("BoneAI", Trim(_mod.Conversation.LastResponse, 480));
    private void EnableAll() { _mod.Config.Enabled.Value = _mod.Config.AllowActions.Value = _mod.Config.AllowPlayerModification.Value = _mod.Config.AllowSpawning.Value = _mod.Config.AllowCombat.Value = _mod.Config.FusionSynchronization.Value = true; Notify("BoneAI", "All game controls enabled."); }
    private void CycleProvider() { var names = AI.ProviderCatalog.Names; var at = Array.FindIndex(names, x => x.Equals(_mod.Config.Provider.Value, StringComparison.OrdinalIgnoreCase)); var next = names[(at + 1 + names.Length) % names.Length]; _mod.Config.Provider.Value = next; _mod.Config.ProviderModel.Value = AI.ProviderCatalog.DefaultModel(next); _mod.Config.ProviderBaseUrl.Value = string.Empty; Notify("BoneAI provider", next); _ = _mod.Conversation.ConnectAsync(); }
    private static void Notify(string title, string message) => Notifier.Send(new Notification { Title = title, Message = message, ShowTitleOnPopup = true, PopupLength = 7, Type = NotificationType.Information });
    private static void Bind(Page page, string name, MelonLoader.MelonPreferences_Entry<bool> entry, Action<bool>? extra = null) => page.CreateBool(name, Color.white, entry.Value, v => { entry.Value = v; extra?.Invoke(v); });
    private static string Trim(string? value, int max) => string.IsNullOrWhiteSpace(value) ? "None" : value!.Length <= max ? value : value[..(max - 1)] + "…";
    public void Dispose() { Hooking.OnUIRigCreated -= AddPreferencesButton; if (_nativeButton != null && !_nativeButton.WasCollected) UnityEngine.Object.Destroy(_nativeButton); }
}
