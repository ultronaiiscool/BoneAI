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
    private StringElement? _loginCode;
    private StringElement? _providerKey;
    private bool _clearingProviderKey;
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
        voice.CreateFunction("Voice Setup Help", Color.yellow, () => Notify("Voice Beta", "Enter an OpenAI API key under AI Provider. BoneAI saves it with your device's protected credential storage until you clear it."));

        var provider = _root.CreatePage("AI Provider", new Color(0.55f, 0.55f, 1f), 9);
        provider.CreateFunction("Quest Standalone OpenAI", Color.green, SelectQuestStandaloneOpenAi);
        provider.CreateFunction("Change Provider", Color.cyan, CycleProvider);
        provider.CreateString("Provider", Color.white, _mod.Config.Provider.Value, v => _mod.Config.Provider.Value = v);
        provider.CreateString("Model", Color.white, _mod.Config.ProviderModel.Value, v => _mod.Config.ProviderModel.Value = v);
        provider.CreateString("Custom Base URL", Color.white, _mod.Config.ProviderBaseUrl.Value, v => _mod.Config.ProviderBaseUrl.Value = v);
        _providerKey = provider.CreateString("API Key (saved securely)", Color.white, string.Empty, SetProviderApiKey);
        provider.CreateFunction("Clear Current API Key", Color.yellow, ClearProviderApiKey);
        provider.CreateFunction("Reconnect", Color.green, () => _ = _mod.Conversation.ConnectAsync());
        provider.CreateFunction("New Conversation", Color.cyan, () => _ = _mod.Conversation.NewConversationAsync());

        var codex = _root.CreatePage("Codex Sign-In", new Color(0.18f, 0.72f, 1f), 9);
        _loginCode = codex.CreateString("One-Time Code", Color.white, "Not started", _ => { });
        codex.CreateFunction("Sign In With Codex", Color.green, () => _ = SignInWithCodexAsync());
        codex.CreateFunction("Open Sign-In Page Again", Color.cyan, OpenLoginPage);
        codex.CreateFunction("Reconnect", Color.white, () => _ = _mod.Conversation.ConnectAsync());
        codex.CreateFunction("Sign Out", Color.red, () => _ = SignOutCodexAsync());
        codex.CreateFunction("About Saved Login", Color.white, () => Notify("Codex Login", "Codex securely keeps and refreshes your login. It remains signed in after restarts until you choose Sign Out."));
        codex.CreateFunction("Quest Standalone Help", Color.yellow, () => Notify("Quest Standalone", "For no-PC Quest use, choose OpenAI or another cloud provider under AI Provider and enter its API key in-game. Codex account sign-in requires the official App Server and is PCVR-only."));

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
        Bind(diagnostics, "Auto-start Codex Host", _mod.Config.AutoStartCodexHost);
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
        if (_loginCode != null) _loginCode.Value = string.IsNullOrWhiteSpace(_mod.Conversation.DeviceLoginCode) ? (_mod.Conversation.CodexSignedIn ? "Signed in (saved by Codex)" : "Not signed in") : _mod.Conversation.DeviceLoginCode;
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
    private void SetProviderApiKey(string value)
    {
        if (_clearingProviderKey || string.IsNullOrWhiteSpace(value)) return;
        var provider = _mod.Config.Provider.Value;
        var persisted = Infrastructure.RuntimeSecrets.SetProviderApiKey(provider, value);
        _clearingProviderKey = true;
        if (_providerKey != null) _providerKey.Value = string.Empty;
        _clearingProviderKey = false;
        Notify("BoneAI", persisted ? provider + " API key encrypted and saved on this device." : provider + " API key is active for this session only; protected storage failed: " + Infrastructure.RuntimeSecrets.LastStorageError);
        _ = _mod.Conversation.ConnectAsync();
    }
    private void ClearProviderApiKey()
    {
        var provider = _mod.Config.Provider.Value;
        var cleared = Infrastructure.RuntimeSecrets.SetProviderApiKey(provider, null);
        Notify("BoneAI", cleared ? provider + " API key cleared from memory and protected storage." : provider + " key cleared from memory, but protected storage reported: " + Infrastructure.RuntimeSecrets.LastStorageError);
    }
    private async Task SignInWithCodexAsync()
    {
        try
        {
            if (!Infrastructure.PlatformInfo.IsAndroid && !await _mod.EnsureCodexHostAsync().ConfigureAwait(false))
                throw new InvalidOperationException(_mod.CodexHost.Status);
            var login = await _mod.Conversation.StartCodexDeviceLoginAsync().ConfigureAwait(false);
            await _mod.Dispatcher.InvokeAsync(() =>
            {
                GUIUtility.systemCopyBuffer = login.UserCode;
                Notify("Codex Sign-In", "Code " + login.UserCode + " copied. Paste it in the browser, finish signing in, then return to BONELAB.");
                Application.OpenURL(ValidateCodexLoginUrl(login.VerificationUrl));
                return true;
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Infrastructure.AgentLog.Exception("Codex sign-in", ex);
            await _mod.Dispatcher.InvokeAsync(() => { Notify("Codex Sign-In Failed", ex.GetBaseException().Message); return true; }).ConfigureAwait(false);
        }
    }
    private async Task SignOutCodexAsync()
    {
        try
        {
            if (!Infrastructure.PlatformInfo.IsAndroid && !await _mod.EnsureCodexHostAsync().ConfigureAwait(false)) throw new InvalidOperationException(_mod.CodexHost.Status);
            _mod.Config.Provider.Value = "Codex";
            await _mod.Conversation.ConnectAsync().ConfigureAwait(false);
            await _mod.Conversation.LogoutCodexAsync().ConfigureAwait(false);
            await _mod.Dispatcher.InvokeAsync(() => { Notify("Codex", "Signed out. Codex's saved login was cleared."); return true; }).ConfigureAwait(false);
        }
        catch (Exception ex) { Infrastructure.AgentLog.Exception("Codex sign-out", ex); await _mod.Dispatcher.InvokeAsync(() => { Notify("Codex Sign-Out Failed", ex.GetBaseException().Message); return true; }).ConfigureAwait(false); }
    }
    private void OpenLoginPage()
    {
        if (string.IsNullOrWhiteSpace(_mod.Conversation.DeviceLoginUrl)) { Notify("Codex Sign-In", "Choose Sign In With Codex first."); return; }
        GUIUtility.systemCopyBuffer = _mod.Conversation.DeviceLoginCode;
        try { Application.OpenURL(ValidateCodexLoginUrl(_mod.Conversation.DeviceLoginUrl)); }
        catch (Exception ex) { Notify("Codex Sign-In", ex.Message); }
    }
    private static string ValidateCodexLoginUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !(uri.Host.Equals("auth.openai.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("chatgpt.com", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Codex returned an untrusted sign-in URL. Sign-in was blocked.");
        return uri.AbsoluteUri;
    }
    private void EnableAll() { _mod.Config.Enabled.Value = _mod.Config.AllowActions.Value = _mod.Config.AllowPlayerModification.Value = _mod.Config.AllowSpawning.Value = _mod.Config.AllowCombat.Value = _mod.Config.FusionSynchronization.Value = true; Notify("BoneAI", "All game controls enabled."); }
    private void SelectQuestStandaloneOpenAi()
    {
        _mod.Config.Provider.Value = "OpenAI";
        _mod.Config.ProviderModel.Value = AI.ProviderCatalog.DefaultModel("OpenAI");
        _mod.Config.ProviderBaseUrl.Value = string.Empty;
        Notify("Quest Standalone", "OpenAI direct mode selected. Enter your OpenAI API key below; no PC or bridge is used.");
    }
    private void CycleProvider() { var names = AI.ProviderCatalog.Names; var at = Array.FindIndex(names, x => x.Equals(_mod.Config.Provider.Value, StringComparison.OrdinalIgnoreCase)); var next = names[(at + 1 + names.Length) % names.Length]; _mod.Config.Provider.Value = next; _mod.Config.ProviderModel.Value = AI.ProviderCatalog.DefaultModel(next); _mod.Config.ProviderBaseUrl.Value = string.Empty; Notify("BoneAI provider", next); _ = _mod.Conversation.ConnectAsync(); }
    private static void Notify(string title, string message) => Notifier.Send(new Notification { Title = title, Message = message, ShowTitleOnPopup = true, PopupLength = 7, Type = NotificationType.Information });
    private static void Bind(Page page, string name, MelonLoader.MelonPreferences_Entry<bool> entry, Action<bool>? extra = null) => page.CreateBool(name, Color.white, entry.Value, v => { entry.Value = v; extra?.Invoke(v); });
    private static string Trim(string? value, int max) => string.IsNullOrWhiteSpace(value) ? "None" : value!.Length <= max ? value : value[..(max - 1)] + "…";
    public void Dispose() { Hooking.OnUIRigCreated -= AddPreferencesButton; if (_nativeButton != null && !_nativeButton.WasCollected) UnityEngine.Object.Destroy(_nativeButton); }
}
