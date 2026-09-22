using System.Net.Http.Headers;
using System.Text;
using BoneAI.Infrastructure;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BoneAI.AI;

/// <summary>Opt-in beta voice loop. Audio never becomes a general OS tool: it is sent only to OpenAI transcription/speech endpoints.</summary>
public sealed class VoiceAssistant : IDisposable
{
    private const int Rate = 16000;
    private readonly AgentConfig _config;
    private readonly ConversationManager _conversation;
    private readonly MainThreadDispatcher _dispatcher;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(45) };
    private AudioClip? _clip;
    private int _lastPosition;
    private float _lastPoll;
    private float _silentFor;
    private bool _heardSpeech;
    private bool _busy;
    private bool _listenOnce;
    private Il2CppStructArray<float>? _pollSamples;
    private float _nextStartAttempt;

    public string Status { get; private set; } = "Voice beta off";
    public string LastTranscript { get; private set; } = string.Empty;

    public VoiceAssistant(AgentConfig config, ConversationManager conversation, MainThreadDispatcher dispatcher)
    {
        _config = config; _conversation = conversation; _dispatcher = dispatcher;
        _conversation.ResponseCompleted += OnResponse;
    }

    public void ListenOnce() { _listenOnce = true; _config.VoiceBetaEnabled.Value = true; Status = "Listening once…"; }

    public void Update()
    {
        if (!_config.VoiceBetaEnabled.Value) { Stop(); Status = "Voice beta off"; return; }
        if (_busy) return;
        if (_clip == null) { if (Time.unscaledTime >= _nextStartAttempt) Start(); return; }
        if (Time.unscaledTime < _lastPoll + 0.1f) return;
        var elapsed = Time.unscaledTime - _lastPoll; _lastPoll = Time.unscaledTime;
        var position = Microphone.GetPosition(Device());
        if (position <= _lastPosition) return;
        var count = Math.Min(position - _lastPosition, 4096);
        var sampleCount = count * Math.Max(1, _clip.channels);
        _pollSamples ??= new Il2CppStructArray<float>(4096 * Math.Max(1, _clip.channels));
        if (!_clip.GetData(_pollSamples, position - count)) return;
        var peak = 0f;
        for (var i = 0; i < sampleCount; i++) peak = Math.Max(peak, Math.Abs(_pollSamples[i]));
        _lastPosition = position;
        if (peak >= _config.VoiceSilenceThreshold.Value) { _heardSpeech = true; _silentFor = 0; Status = "Hearing speech…"; }
        else if (_heardSpeech) _silentFor += elapsed;
        if ((_heardSpeech && _silentFor >= _config.VoiceSilenceSeconds.Value) || position >= Rate * _config.VoiceMaxSeconds.Value - Rate / 4)
            Finish(position);
    }

    private void Start()
    {
        try
        {
            var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(key)) { Status = "Voice needs OPENAI_API_KEY"; _nextStartAttempt = Time.unscaledTime + 2f; return; }
            _clip = Microphone.Start(Device(), false, Math.Clamp(_config.VoiceMaxSeconds.Value, 3, 30), Rate);
            _pollSamples = new Il2CppStructArray<float>(4096 * Math.Max(1, _clip.channels));
            _lastPosition = 0; _silentFor = 0; _heardSpeech = false; _lastPoll = Time.unscaledTime;
            Status = _listenOnce ? "Listening once…" : "Waiting for “" + _config.VoiceWakeWord.Value + "”…";
        }
        catch (Exception ex) { Status = "Microphone error: " + ex.GetBaseException().Message; AgentLog.Warn(Status); Stop(); }
    }

    private void Finish(int sampleCount)
    {
        if (_clip == null) return;
        var channels = Math.Max(1, _clip.channels);
        var samples = new Il2CppStructArray<float>(Math.Max(1, sampleCount * channels));
        _clip.GetData(samples, 0);
        Stop();
        if (!_heardSpeech) return;
        var managed = new float[samples.Length];
        for (var i = 0; i < samples.Length; i++) managed[i] = samples[i];
        _busy = true; Status = "Transcribing…";
        _ = TranscribeAndSendAsync(BuildWav(managed, channels));
    }

    private async Task TranscribeAndSendAsync(byte[] wav)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var audio = new ByteArrayContent(wav); audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            form.Add(audio, "file", "boneai-voice.wav"); form.Add(new StringContent("gpt-4o-transcribe"), "model");
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions") { Content = form };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Transcription failed: " + body);
            var text = JObject.Parse(body)["text"]?.Value<string>()?.Trim() ?? string.Empty;
            LastTranscript = text;
            var accepted = _listenOnce || !_config.VoiceWakeWordEnabled.Value;
            var wake = _config.VoiceWakeWord.Value.Trim();
            if (!accepted && !string.IsNullOrWhiteSpace(wake))
            {
                var index = text.IndexOf(wake, StringComparison.OrdinalIgnoreCase);
                if (index >= 0) { accepted = true; text = (text[..index] + text[(index + wake.Length)..]).Trim(' ', ',', ':', '-'); }
            }
            _listenOnce = false;
            if (accepted && !string.IsNullOrWhiteSpace(text)) { Status = "Sending: " + Trim(text, 64); await _conversation.SendAsync(text).ConfigureAwait(false); }
            else Status = "Wake word not heard";
        }
        catch (Exception ex) { Status = "Voice error: " + Trim(ex.GetBaseException().Message, 100); AgentLog.Warn(Status); }
        finally { _busy = false; }
    }

    private void OnResponse(string text)
    {
        if (!_config.VoiceBetaEnabled.Value || !_config.VoiceSpeakResponses.Value || string.IsNullOrWhiteSpace(text)) return;
        _ = SpeakAsync(text);
    }

    private async Task SpeakAsync(string text)
    {
        try
        {
            var body = new JObject { ["model"] = "gpt-4o-mini-tts", ["voice"] = "coral", ["input"] = Trim(text, 1800), ["response_format"] = "wav" };
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/speech");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
            request.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Speech request failed.");
            await _dispatcher.InvokeAsync(() => { PlayWav(bytes); return true; });
        }
        catch (Exception ex) { AgentLog.Warn("Voice reply failed: " + ex.GetBaseException().Message); }
    }

    private static void PlayWav(byte[] wav)
    {
        if (wav.Length < 44) return;
        var channels = BitConverter.ToInt16(wav, 22); var rate = BitConverter.ToInt32(wav, 24); var bits = BitConverter.ToInt16(wav, 34);
        var offset = FindData(wav); if (offset < 0 || bits != 16) return;
        var count = (wav.Length - offset) / 2; var data = new Il2CppStructArray<float>(count);
        for (var i = 0; i < count; i++) data[i] = BitConverter.ToInt16(wav, offset + i * 2) / 32768f;
        var clip = AudioClip.Create("BoneAI Voice", count / channels, channels, rate, false); clip.SetData(data, 0);
        var go = new GameObject("BoneAI Voice"); var source = go.AddComponent<AudioSource>(); source.clip = clip; source.spatialBlend = 0; source.Play(); UnityEngine.Object.Destroy(go, clip.length + 0.2f);
    }

    private static int FindData(byte[] wav)
    {
        for (var i = 12; i + 8 < wav.Length; i++) if (wav[i] == 100 && wav[i + 1] == 97 && wav[i + 2] == 116 && wav[i + 3] == 97) return i + 8;
        return -1;
    }

    private static byte[] BuildWav(float[] samples, int channels)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        var size = samples.Length * 2;
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + size); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)channels); writer.Write(Rate); writer.Write(Rate * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(size);
        foreach (var sample in samples) writer.Write((short)(Math.Clamp(sample, -1f, 1f) * short.MaxValue));
        return stream.ToArray();
    }

    private string Device() => string.IsNullOrWhiteSpace(_config.VoiceInputDevice.Value) ? null! : _config.VoiceInputDevice.Value;
    private void Stop() { if (_clip != null) { try { Microphone.End(Device()); } catch { } _clip = null; } _pollSamples = null; }
    private static string Trim(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
    public void Dispose() { _conversation.ResponseCompleted -= OnResponse; Stop(); _http.Dispose(); }
}
