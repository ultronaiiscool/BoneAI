using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.BoneMenu.UI;
using BoneLib.Notifications;
using Il2CppTMPro;
using BoneAI.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using UIRig = Il2CppSLZ.Bonelab.UIRig;

namespace BoneAI.UI;

public sealed class AgentMenu
{
    private readonly BoneAIMod _mod;
    private Page? _root;
    private Page? _conversations;
    private Page? _codexModels;
    private StringElement? _prompt;
    private StringElement? _status;
    private StringElement? _activity;
    private StringElement? _response;
    private StringElement? _voiceStatus;
    private StringElement? _transcript;
    private StringElement? _loginCode;
    private StringElement? _providerKey;
    private StringElement? _runtimeStatus;
    private StringElement? _spawnQuery;
    private StringElement? _spawnStatus;
    private string? _lastSpawnActionId;
    private bool _clearingProviderKey;
    private GameObject? _nativeButton;
    private float _nextRefresh;
    private int _conversationCount = -1;
    private string _modelQueryText = string.Empty;
    private bool _showHiddenModels;
    private int _modelPage;

    public AgentMenu(BoneAIMod mod) => _mod = mod;

    public void Create()
    {
        var orange = new Color(1f, 0.42f, 0.08f);
        var cyan = new Color(0.28f, 0.82f, 1f);
        var mint = new Color(0.32f, 0.94f, 0.68f);
        _root = new Page(Page.Root, "BoneAI", orange, 9) { BackgroundOpacity = 0.96f };
        var assistant = _root.CreatePage("Assistant · Home", orange, 9);
        _status = assistant.CreateString("● Connection", mint, "Starting", _ => { });
        _activity = assistant.CreateString("Current action", cyan, "Idle", _ => { });
        _prompt = assistant.CreateString("Ask BoneAI", Color.white, "Type a question or game command", _ => { });
        assistant.CreateFunction("Send prompt", mint, SendPrompt);
        assistant.CreateFunction("Listen Once (Voice Beta)", orange, _mod.Voice.ListenOnce);
        assistant.CreateFunction("Cancel Request", Color.yellow, _mod.Conversation.Cancel);
        _response = assistant.CreateString("AI reply", Color.white, "None", _ => { });
        assistant.CreateFunction("Read full reply", cyan, ShowResponse);

        _conversations = _root.CreatePage("Conversations", cyan, 8);
        BuildConversations();

        var spawns = _root.CreatePage("Spawn Catalog", mint, 8);
        _spawnQuery = spawns.CreateString("Name or Barcode", Color.white, string.Empty, _ => { });
        _spawnStatus = spawns.CreateString("Last spawn", Color.white, "None", _ => { });
        spawns.CreateFunction("Search loaded spawnables", cyan, () => _ = SearchSpawnsAsync());
        spawns.CreateFunction("Spawn unique match", mint, () => _ = SpawnFromMenuAsync());
        spawns.CreateFunction("Check last spawn", Color.yellow, () => _ = CheckLastSpawnAsync());
        spawns.CreateFunction("Refresh catalog", Color.white, () => _ = RefreshSpawnsAsync());

        var voice = _root.CreatePage("Voice · Beta", orange, 9);
        Bind(voice, "Voice AI Beta", _mod.Config.VoiceBetaEnabled);
        Bind(voice, "Require Wake Word", _mod.Config.VoiceWakeWordEnabled);
        voice.CreateString("Wake Word", Color.white, _mod.Config.VoiceWakeWord.Value, v => _mod.Config.VoiceWakeWord.Value = string.IsNullOrWhiteSpace(v) ? "Hey BoneAI" : v.Trim());
        Bind(voice, "Speak AI Replies", _mod.Config.VoiceSpeakResponses);
        voice.CreateFunction("Listen Once", Color.green, _mod.Voice.ListenOnce);
        voice.CreateFunction("Open Free Browser Voice", Color.cyan, OpenBrowserVoice);
        voice.CreateFunction("Open On-Device Voice", Color.green, OpenLocalBrowserVoice);
        voice.CreateFunction("Stop Browser Voice", Color.yellow, _mod.BrowserVoice.Stop);
        _voiceStatus = voice.CreateString("Voice Status", Color.gray, "Voice beta off", _ => { });
        _transcript = voice.CreateString("Last Transcript", Color.white, "None", _ => { });
        voice.CreateFunction("Voice Setup Help", Color.yellow, () => Notify("Voice Beta", "Browser Voice tries on-device speech first where supported, then the browser service. On-device may need a model download. Typed commands always work. In-game microphone voice requires an OpenAI key."));

        var provider = _root.CreatePage("AI Provider", cyan, 9);
        provider.CreateFunction("Codex account sign-in", mint, SelectCodex);
        provider.CreateFunction("Free OpenRouter", mint, SelectFreeOpenRouter);
        provider.CreateFunction("Paid OpenAI Direct", Color.white, SelectQuestStandaloneOpenAi);
        provider.CreateFunction("Change Provider", Color.cyan, CycleProvider);
        provider.CreateString("Provider", Color.white, _mod.Config.Provider.Value, v => _mod.Config.Provider.Value = v);
        provider.CreateString("Model", Color.white, _mod.Config.ProviderModel.Value, v => _mod.Config.ProviderModel.Value = v);
        provider.CreateString("Custom Base URL", Color.white, _mod.Config.ProviderBaseUrl.Value, v => _mod.Config.ProviderBaseUrl.Value = v);
        _providerKey = provider.CreateString("API Key (saved securely)", Color.white, string.Empty, SetProviderApiKey);
        provider.CreateFunction("Clear Current API Key", Color.yellow, ClearProviderApiKey);
        provider.CreateFunction("Reconnect", Color.green, () => _ = _mod.Conversation.ConnectAsync());
        provider.CreateFunction("New Conversation", Color.cyan, () => _ = _mod.Conversation.NewConversationAsync());

        var codex = _root.CreatePage("Codex Account", cyan, 9);
        _runtimeStatus = codex.CreateString("Local App Server", Color.white, "Checking", _ => { });
        _loginCode = codex.CreateString("One-Time Code", Color.white, "Not started", _ => { });
        codex.CreateFunction("Sign In With Codex", Color.green, () => _ = SignInWithCodexAsync());
        codex.CreateFunction("Open Sign-In Page Again", Color.cyan, OpenLoginPage);
        codex.CreateFunction("Reconnect", Color.white, () => _ = _mod.Conversation.ConnectAsync());
        codex.CreateFunction("Sign Out", Color.red, () => _ = SignOutCodexAsync());
        codex.CreateFunction("About Saved Login", Color.white, () => Notify("Codex Login", "Codex securely keeps and refreshes your login. It remains signed in after restarts until you choose Sign Out."));
        codex.CreateFunction("Quest Setup", Color.yellow, () => Notify("Quest Codex", "Install the v3 Quest package with its native library in UserLibs. Choose Codex account sign-in, enter the one-time code in the browser, then return to BONELAB."));
        _codexModels = codex.CreatePage("Choose Codex Model", mint, 8);
        BuildCodexModels();

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
        if (_voiceStatus != null) _voiceStatus.Value = Trim(_mod.BrowserVoice.Running ? _mod.BrowserVoice.Status : _mod.Voice.Status, 100);
        if (_transcript != null) _transcript.Value = Trim(_mod.Voice.LastTranscript, 100);
        if (_loginCode != null) _loginCode.Value = string.IsNullOrWhiteSpace(_mod.Conversation.DeviceLoginCode) ? (_mod.Conversation.CodexSignedIn ? "Signed in (saved by Codex)" : "Not signed in") : _mod.Conversation.DeviceLoginCode;
        if (_runtimeStatus != null) _runtimeStatus.Value = Trim(Infrastructure.PlatformInfo.IsAndroid ? _mod.QuestCodexHost.Status : _mod.CodexHost.Status, 100);
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

    private void BuildCodexModels()
    {
        if (_codexModels == null) return;
        var models = _mod.Conversation.CodexModels
            .Where(x => _showHiddenModels || !x.Hidden)
            .Where(x => string.IsNullOrWhiteSpace(_modelQueryText) ||
                x.DisplayName.Contains(_modelQueryText, StringComparison.OrdinalIgnoreCase) ||
                x.Id.Contains(_modelQueryText, StringComparison.OrdinalIgnoreCase)).ToArray();
        _modelPage = Math.Clamp(_modelPage, 0, Math.Max(0, (models.Length - 1) / 8));
        _codexModels.RemoveAll();
        var selected = string.IsNullOrWhiteSpace(_mod.Config.CodexModel.Value) ? "Codex default" : _mod.Config.CodexModel.Value;
        _codexModels.CreateString("Selected", Color.white, selected, _ => { });
        _codexModels.CreateString("Search", Color.white, _modelQueryText, value => _modelQueryText = value.Trim());
        _codexModels.CreateFunction("Search models", Color.cyan, () => { _modelPage = 0; BuildCodexModels(); });
        _codexModels.CreateBool("Include older / hidden", Color.white, _showHiddenModels, value => { _showHiddenModels = value; _modelPage = 0; _ = RefreshCodexModelsAsync(); });
        _codexModels.CreateFunction("Refresh available models", Color.green, () => _ = RefreshCodexModelsAsync());
        _codexModels.CreateFunction("Use Codex default", Color.yellow, () =>
        {
            _mod.Config.CodexModel.Value = _mod.Config.CodexEffort.Value = string.Empty;
            Notify("Codex model", "Using the model and effort selected by your Codex account.");
            BuildCodexModels();
        });
        if (models.Length == 0) _codexModels.CreateFunction("No models loaded — choose Refresh", Color.gray, () => { });
        foreach (var item in models.Skip(_modelPage * 8).Take(8))
        {
            var model = item;
            var label = Trim(model.DisplayName + (model.Hidden ? " · older" : "") + (model.IsDefault ? " · default" : ""), 44);
            _codexModels.CreateFunction(label, model.Id == _mod.Config.CodexModel.Value ? Color.green : Color.white, () =>
            {
                _mod.Config.CodexModel.Value = model.Id;
                _mod.Config.CodexEffort.Value = model.DefaultEffort;
                Notify("Codex model", model.DisplayName + " selected for the next request. Effort: " + (string.IsNullOrEmpty(model.DefaultEffort) ? "account default" : model.DefaultEffort));
                BuildCodexModels();
            });
        }
        if (_modelPage > 0) _codexModels.CreateFunction("Previous models", Color.cyan, () => { _modelPage--; BuildCodexModels(); });
        if ((_modelPage + 1) * 8 < models.Length) _codexModels.CreateFunction("Next models", Color.cyan, () => { _modelPage++; BuildCodexModels(); });
    }

    private async Task RefreshCodexModelsAsync()
    {
        try
        {
            var models = await _mod.Conversation.ListCodexModelsAsync(_showHiddenModels).ConfigureAwait(false);
            await _mod.Dispatcher.InvokeAsync(() =>
            {
                BuildCodexModels();
                Notify("Codex models", models.Count + " models returned by your Codex App Server.");
                return true;
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Infrastructure.AgentLog.Warn("Codex model list: " + ex.GetBaseException().Message);
            await _mod.Dispatcher.InvokeAsync(() => { Notify("Codex models", "Could not load: " + Trim(ex.GetBaseException().Message, 180)); return true; }).ConfigureAwait(false);
        }
    }

    private void SendPrompt() { var value = _prompt?.Value ?? string.Empty; if (string.IsNullOrWhiteSpace(value)) return; if (_prompt != null) _prompt.Value = string.Empty; _ = _mod.Conversation.SendAsync(value); }
    private async Task SearchSpawnsAsync()
    {
        var result = await _mod.Tools.ExecuteAsync(new ToolCall { Name = "spawn.list", Arguments = new JObject { ["query"] = _spawnQuery?.Value ?? string.Empty, ["limit"] = 8 } }, CancellationToken.None).ConfigureAwait(false);
        var items = result.Data == null ? new JArray() : JArray.FromObject(result.Data);
        var summary = result.Result == "success" ? string.Join("; ", items.Take(5).Select(x => x["title"] + " (" + x["barcode"] + ")")) : result.Reason ?? "Catalog unavailable";
        await _mod.Dispatcher.InvokeAsync(() => { Notify("Spawn catalog", Trim(string.IsNullOrWhiteSpace(summary) ? "No matches" : summary, 450)); return true; }).ConfigureAwait(false);
    }
    private async Task SpawnFromMenuAsync()
    {
        var query = _spawnQuery?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(query)) { Notify("Spawn catalog", "Enter a name or barcode first."); return; }
        var result = await _mod.Tools.ExecuteAsync(new ToolCall { Name = "spawn.spawn", Arguments = new JObject { ["query"] = query } }, CancellationToken.None).ConfigureAwait(false);
        await _mod.Dispatcher.InvokeAsync(() =>
        {
            if (result.Result == "pending") _lastSpawnActionId = result.ActionId;
            var message = result.Result == "pending" ? "Requested " + query + ". Choose Check last spawn for confirmation." : result.Reason ?? result.Result;
            if (_spawnStatus != null) _spawnStatus.Value = Trim(message, 100);
            Notify("Spawn catalog", Trim(message, 450));
            return true;
        }).ConfigureAwait(false);
    }
    private async Task CheckLastSpawnAsync()
    {
        if (string.IsNullOrWhiteSpace(_lastSpawnActionId)) { Notify("Spawn catalog", "No spawn was requested from this menu yet."); return; }
        var result = await _mod.Tools.ExecuteAsync(new ToolCall { Name = "spawn.status", Arguments = new JObject { ["actionId"] = _lastSpawnActionId } }, CancellationToken.None).ConfigureAwait(false);
        var message = result.Result == "success" ? "Spawn confirmed on this client; peer visibility is not confirmed." : result.Reason ?? "Still waiting for local confirmation.";
        await _mod.Dispatcher.InvokeAsync(() => { if (_spawnStatus != null) _spawnStatus.Value = Trim(message, 100); Notify("Spawn catalog", Trim(message, 450)); return true; }).ConfigureAwait(false);
    }
    private async Task RefreshSpawnsAsync()
    {
        var result = await _mod.Tools.ExecuteAsync(new ToolCall { Name = "spawn.refresh" }, CancellationToken.None).ConfigureAwait(false);
        var message = result.Result == "success" ? JsonConvert.SerializeObject(result.Data) : result.Reason ?? "Catalog refresh failed";
        await _mod.Dispatcher.InvokeAsync(() => { Notify("Spawn catalog", Trim(message, 450)); return true; }).ConfigureAwait(false);
    }
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
            if (!await _mod.EnsureCodexHostAsync().ConfigureAwait(false))
                throw new InvalidOperationException(Infrastructure.PlatformInfo.IsAndroid ? _mod.QuestCodexHost.Status : _mod.CodexHost.Status);
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
            if (!await _mod.EnsureCodexHostAsync().ConfigureAwait(false))
                throw new InvalidOperationException(Infrastructure.PlatformInfo.IsAndroid ? _mod.QuestCodexHost.Status : _mod.CodexHost.Status);
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
    private void SelectCodex()
    {
        _mod.Config.Provider.Value = "Codex";
        _mod.Config.ProviderModel.Value = string.Empty;
        _mod.Config.ProviderBaseUrl.Value = string.Empty;
        Notify("Codex Account", "Codex selected. Open Codex Account and choose Sign In With Codex.");
        _ = _mod.Conversation.ConnectAsync();
    }
    private void SelectFreeOpenRouter()
    {
        _mod.Config.Provider.Value = "OpenRouter Free";
        _mod.Config.ProviderModel.Value = AI.ProviderCatalog.DefaultModel("OpenRouter Free");
        _mod.Config.ProviderBaseUrl.Value = string.Empty;
        Notify("Free OpenRouter", "Free-model routing selected. Bring your OpenRouter API key, enter it under API Key, then reconnect.");
    }
    private void OpenBrowserVoice()
    {
        try
        {
            var url = _mod.BrowserVoice.Start();
            GUIUtility.systemCopyBuffer = url;
            Application.OpenURL(url);
            Notify("Browser Voice", "Voice page opened and its private local URL was copied. Press Start there and keep the page open.");
        }
        catch (Exception ex) { Infrastructure.AgentLog.Exception("Browser voice start", ex); Notify("Browser Voice Failed", ex.GetBaseException().Message); }
    }
    private void OpenLocalBrowserVoice()
    {
        try
        {
            var url = _mod.BrowserVoice.Start() + "#local";
            GUIUtility.systemCopyBuffer = url;
            Application.OpenURL(url);
            Notify("On-Device Voice", "Opened the local speech option. Your browser must support it and may need a one-time model download.");
        }
        catch (Exception ex) { Infrastructure.AgentLog.Exception("local browser voice start", ex); Notify("On-Device Voice Failed", ex.GetBaseException().Message); }
    }
    private void CycleProvider() { var names = AI.ProviderCatalog.Names; var at = Array.FindIndex(names, x => x.Equals(_mod.Config.Provider.Value, StringComparison.OrdinalIgnoreCase)); var next = names[(at + 1 + names.Length) % names.Length]; _mod.Config.Provider.Value = next; _mod.Config.ProviderModel.Value = AI.ProviderCatalog.DefaultModel(next); _mod.Config.ProviderBaseUrl.Value = string.Empty; Notify("BoneAI provider", next); _ = _mod.Conversation.ConnectAsync(); }
    private static void Notify(string title, string message) => Notifier.Send(new Notification { Title = title, Message = message, ShowTitleOnPopup = true, PopupLength = 7, Type = NotificationType.Information });
    private static void Bind(Page page, string name, MelonLoader.MelonPreferences_Entry<bool> entry, Action<bool>? extra = null) => page.CreateBool(name, Color.white, entry.Value, v => { entry.Value = v; extra?.Invoke(v); });
    private static string Trim(string? value, int max) => string.IsNullOrWhiteSpace(value) ? "None" : value!.Length <= max ? value : value[..(max - 1)] + "…";
    public void Dispose() { Hooking.OnUIRigCreated -= AddPreferencesButton; if (_nativeButton != null && !_nativeButton.WasCollected) UnityEngine.Object.Destroy(_nativeButton); }
}
