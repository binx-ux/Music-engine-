using NAudio.CoreAudioApi;
using NAudio.MediaFoundation;
using NAudio.Wave;
using Mixline.Audio.Devices;
using Mixline.Audio.DSP;
using Mixline.Audio.Mixer;
using Mixline.Audio.Sources;
using Mixline.Audio.VirtualDevice;
using Mixline.Core;
using Mixline.Logging;

namespace Mixline.Audio;

public sealed class LatencyInfo
{
    public int InputMs { get; init; }
    public int ProcessingMs { get; init; }
    public int OutputMs { get; init; }
    public int TotalMs => InputMs + ProcessingMs + OutputMs;
}

public sealed class AudioEngine : IDisposable
{
    private readonly AppLog _log;
    private readonly DeviceManager _devices;
    private readonly VirtualDeviceManager _virtual;
    private readonly FloatRingBuffer _micRing = new(1 << 15);
    private readonly FloatRingBuffer _virtualRing = new(1 << 15);
    private readonly VoiceChain _voice = new();
    private readonly Limiter _masterLimiter = new();
    private readonly object _startLock = new();
    private readonly object _voiceLock = new();
    private readonly object _paramLock = new();

    private CaptureStream? _capture;
    private WasapiOut? _monitor;
    private WasapiOut? _virtualOut;
    private MonitorProvider? _monitorProvider;
    private VirtualProvider? _virtualProvider;
    private MixerSnapshot _snapshot;
    private VoiceSettings _voiceSettings = new();
    private VoicePlayback[] _voices = [];
    private float[] _micBuf = new float[8192];
    private float[] _musicBuf = new float[8192];
    private float[] _soundBuf = new float[8192];
    private float[] _toneBuf = new float[8192];
    private float[] _mixBuf = new float[8192];
    private PeakTracker _micPeak = new(48000);
    private PeakTracker _musicPeak = new(48000);
    private PeakTracker _soundPeak = new(48000);
    private PeakTracker _masterPeak = new(48000);
    private PeakTracker _virtualPeak = new(48000);
    private ToneGenerator _tone = new(48000);
    private int _sampleRate = AudioConstants.DefaultSampleRate;
    private int _bufferMs = 20;
    private bool _running;
    private bool _micActive;
    private DateTime _startedUtc;
    private string? _inputName;
    private string? _outputName;
    private string? _virtualName;
    private string? _virtualId;

    public DeviceManager Devices => _devices;
    public VirtualDeviceManager Virtual => _virtual;
    public MusicPlayer Music { get; }
    public StreamedSource MusicSource { get; }
    public int SampleRate => _sampleRate;
    public bool IsRunning => _running;
    public bool MicrophoneActive => _micActive && _running;
    public string? InputName => _inputName;
    public string? OutputName => _outputName;
    public string? VirtualName => _virtualName;
    public LatencyInfo Latency { get; private set; } = new();
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<string>? ErrorRaised;

    public AudioEngine(AppLog log)
    {
        _log = log;
        _devices = new DeviceManager(log);
        _virtual = new VirtualDeviceManager(log, _devices);
        MusicSource = new StreamedSource(log, _sampleRate);
        Music = new MusicPlayer(MusicSource);
        MediaFoundationApi.Startup();
        _devices.DevicesChanged += OnDevicesChanged;
    }

    public MeterState ReadMeters()
    {
        return new MeterState
        {
            Mic = _micPeak.Peak,
            MicHold = _micPeak.Hold,
            Music = _musicPeak.Peak,
            MusicHold = _musicPeak.Hold,
            Soundboard = _soundPeak.Peak,
            SoundboardHold = _soundPeak.Hold,
            Master = _masterPeak.Peak,
            MasterHold = _masterPeak.Hold,
            Virtual = _virtualPeak.Peak,
            VirtualHold = _virtualPeak.Hold,
            GainReductionDb = _voice.GainReductionDb,
            GateOpen = _voice.GateOpen,
            MicActive = MicrophoneActive
        };
    }

    public void UpdateMixer(MixerSettings mixer, bool bypass, bool testTone)
    {
        var snap = MixerSnapshot.From(mixer, bypass, testTone);
        lock (_paramLock)
            _snapshot = snap;
        _tone.Enabled = testTone;
    }

    public void UpdateVoice(VoiceSettings voice)
    {
        _voiceSettings = voice;
        _voice.Configure(_sampleRate, voice);
    }

    public void PlaySound(VoicePlayback voice)
    {
        lock (_voiceLock)
        {
            var next = new VoicePlayback[_voices.Length + 1];
            Array.Copy(_voices, next, _voices.Length);
            next[^1] = voice;
            _voices = next;
        }
    }

    public void StopSound(Guid id)
    {
        lock (_voiceLock)
        {
            foreach (var v in _voices)
            {
                if (v.Id == id)
                    v.StopRequested = true;
            }
        }
    }

    public void StopAllSounds()
    {
        lock (_voiceLock)
        {
            foreach (var v in _voices)
                v.StopRequested = true;
        }
    }

    public Result Start(AppConfig config)
    {
        lock (_startLock)
        {
            Stop();
            try
            {
                _sampleRate = AudioConstants.DefaultSampleRate;
                _bufferMs = config.Audio.BufferMilliseconds();
                _micPeak = new PeakTracker(_sampleRate);
                _musicPeak = new PeakTracker(_sampleRate);
                _soundPeak = new PeakTracker(_sampleRate);
                _masterPeak = new PeakTracker(_sampleRate);
                _virtualPeak = new PeakTracker(_sampleRate);
                _tone = new ToneGenerator(_sampleRate);
                _voice.Configure(_sampleRate, config.Voice);
                UpdateMixer(config.Mixer, config.Audio.BypassProcessing, config.Advanced.TestToneOnStart);

                var share = config.Audio.ShareMode == ShareModeSetting.Exclusive
                    ? AudioClientShareMode.Exclusive
                    : AudioClientShareMode.Shared;

                var output = ResolveRender(config.Devices.OutputId, true);
                if (output is null)
                    return Result.Fail("No playback device is available. Plug in headphones or speakers.");

                _outputName = output.FriendlyName;
                _monitorProvider = new MonitorProvider(this, _sampleRate);
                _monitor = new WasapiOut(output, share, true, _bufferMs);
                _monitor.PlaybackStopped += OnMonitorStopped;
                _monitor.Init(_monitorProvider);

                var input = ResolveCapture(config.Devices.InputId);
                if (input is not null)
                {
                    _capture = new CaptureStream(_log, _micRing, _sampleRate);
                    var cap = _capture.Start(input, _bufferMs, share);
                    if (!cap.Success)
                    {
                        _log.Warning("audio", cap.Error ?? "Microphone failed.", cap.Details);
                        RaiseError(cap.Error ?? "Microphone failed.");
                    }
                    else
                    {
                        _inputName = input.FriendlyName;
                        _micActive = true;
                    }
                }
                else
                {
                    _micActive = false;
                    _inputName = null;
                    _log.Warning("audio", "No microphone selected. Music and soundboard still work.");
                }

                _virtualId = config.Devices.VirtualOutputId;
                var virt = ResolveRender(config.Devices.VirtualOutputId, false);
                if (virt is not null)
                {
                    _virtualName = virt.FriendlyName;
                    _virtualProvider = new VirtualProvider(this, _sampleRate);
                    _virtualOut = new WasapiOut(virt, AudioClientShareMode.Shared, true, Math.Max(_bufferMs, 20));
                    _virtualOut.PlaybackStopped += OnVirtualStopped;
                    _virtualOut.Init(_virtualProvider);
                    _virtualOut.Play();
                }
                else
                {
                    _virtualName = null;
                }

                _monitor.Play();
                _running = true;
                _startedUtc = DateTime.UtcNow;
                Latency = new LatencyInfo
                {
                    InputMs = _micActive ? _bufferMs : 0,
                    ProcessingMs = 2,
                    OutputMs = _bufferMs
                };
                _log.Info("audio", $"Engine started. In={_inputName ?? "none"} Out={_outputName} Virtual={_virtualName ?? "none"} {_sampleRate} Hz {_bufferMs} ms");
                StatusChanged?.Invoke(this, "Audio engine running.");
                return Result.Ok();
            }
            catch (Exception ex)
            {
                _log.Error("audio", "Audio engine failed to start.", ex);
                Stop();
                return Result.Fail("Audio could not start.", ex.Message);
            }
        }
    }

    public void Stop()
    {
        _running = false;
        _micActive = false;
        try { _monitor?.Stop(); } catch { }
        try { _virtualOut?.Stop(); } catch { }
        _capture?.Dispose();
        _capture = null;
        DisposeOut(ref _monitor);
        DisposeOut(ref _virtualOut);
        _micRing.Clear();
        _virtualRing.Clear();
        _inputName = null;
        _outputName = null;
        _virtualName = null;
    }

    public VirtualRouteStatus VirtualStatus()
        => _virtual.Status(_virtualId, _running && _virtualOut is not null);

    private MMDevice? ResolveCapture(string? id)
        => _devices.GetDevice(id) ?? _devices.GetDefault(DeviceFlow.Capture);

    private MMDevice? ResolveRender(string? id, bool fallbackDefault)
    {
        var device = _devices.GetDevice(id);
        if (device is not null)
            return device;
        return fallbackDefault ? _devices.GetDefault(DeviceFlow.Render) : null;
    }

    internal int MixMonitorAndVirtual(Span<float> dest, int frames)
    {
        EnsureBuf(frames);
        MixerSnapshot snap;
        lock (_paramLock)
            snap = _snapshot;

        var mic = _micBuf.AsSpan(0, frames * 2);
        var music = _musicBuf.AsSpan(0, frames * 2);
        var sound = _soundBuf.AsSpan(0, frames * 2);
        var tone = _toneBuf.AsSpan(0, frames * 2);
        var bus = _mixBuf.AsSpan(0, frames * 2);

        _micRing.ReadOrZero(mic);
        Music.Read(music, frames);
        MixSounds(sound, frames);
        _tone.Read(tone, frames);

        _voice.Process(mic, frames, snap.BypassProcessing);
        ChannelConvert.ApplyPan(mic, frames, snap.MicPan);
        ChannelConvert.ApplyPan(music, frames, snap.MusicPan);
        ChannelConvert.ApplyPan(sound, frames, snap.SoundPan);

        var solo = AnySolo(snap);
        GainChannel(_micBuf, frames, snap.MicVolume * snap.MicGain, snap.MicMute, snap.MicSolo, solo);
        GainChannel(_musicBuf, frames, snap.MusicVolume * snap.MusicGain, snap.MusicMute, snap.MusicSolo, solo);
        GainChannel(_soundBuf, frames, snap.SoundVolume * snap.SoundGain, snap.SoundMute, snap.SoundSolo, solo);

        _micPeak.Process(mic);
        _musicPeak.Process(music);
        _soundPeak.Process(sound);

        dest[..(frames * 2)].Clear();
        if (snap.MasterMonitorEnabled)
        {
            if (snap.MicMonitorEnabled) MixNative.Add(dest, _micBuf.AsSpan(0, frames * 2), snap.MicMonitor);
            if (snap.MusicMonitorEnabled) MixNative.Add(dest, _musicBuf.AsSpan(0, frames * 2), snap.MusicMonitor);
            if (snap.SoundMonitorEnabled) MixNative.Add(dest, _soundBuf.AsSpan(0, frames * 2), snap.SoundMonitor);
            if (snap.TestTone) MixNative.Add(dest, _toneBuf.AsSpan(0, frames * 2), 1f);
            MixNative.Scale(dest[..(frames * 2)], snap.MonitorVolume * (snap.MasterMute ? 0f : snap.MasterMonitor * snap.MasterVolume));
            if (!snap.BypassProcessing)
                _masterLimiter.ProcessStereo(dest[..(frames * 2)], frames);
        }
        _masterPeak.Process(dest[..(frames * 2)]);

        bus.Clear();
        MixNative.Add(bus, _micBuf.AsSpan(0, frames * 2), snap.MicVirtual);
        MixNative.Add(bus, _musicBuf.AsSpan(0, frames * 2), snap.MusicVirtual);
        MixNative.Add(bus, _soundBuf.AsSpan(0, frames * 2), snap.SoundVirtual);
        if (snap.TestTone)
            MixNative.Add(bus, _toneBuf.AsSpan(0, frames * 2), 1f);
        MixNative.Scale(bus, snap.MasterMute ? 0f : snap.MasterVirtual * snap.MasterVolume);
        if (!snap.BypassProcessing)
            _masterLimiter.ProcessStereo(bus, frames);
        _virtualPeak.Process(bus);
        _virtualRing.Write(bus);
        return frames;
    }

    internal int ReadVirtual(Span<float> dest, int frames)
    {
        _virtualRing.ReadOrZero(dest[..(frames * 2)]);
        return frames;
    }

    private void MixSounds(Span<float> dest, int frames)
    {
        dest.Clear();
        VoicePlayback[] voices;
        lock (_voiceLock)
            voices = _voices;
        if (voices.Length == 0)
            return;

        var live = false;
        foreach (var v in voices)
        {
            if (v.StopRequested || v.Samples.Length < 2)
                continue;
            live = true;
            var fadeInSamples = Math.Max(1, (int)(v.FadeIn * _sampleRate));
            var fadeOutSamples = Math.Max(1, (int)(v.FadeOut * _sampleRate));
            var total = v.Samples.Length / 2;
            for (var i = 0; i < frames; i++)
            {
                if (v.StopRequested)
                    break;
                var pos = v.Cursor;
                if (pos >= total)
                {
                    if (v.Loop)
                        pos = 0;
                    else
                    {
                        v.StopRequested = true;
                        break;
                    }
                }

                var i0 = (int)pos;
                var i1 = i0 + 1;
                if (i1 >= total)
                    i1 = v.Loop ? 0 : i0;
                var t = (float)(pos - i0);
                var l = Lerp(v.Samples[i0 * 2], v.Samples[i1 * 2], t);
                var r = Lerp(v.Samples[i0 * 2 + 1], v.Samples[i1 * 2 + 1], t);
                var env = 1f;
                if (pos < fadeInSamples)
                    env *= (float)(pos / fadeInSamples);
                if (!v.Loop && pos > total - fadeOutSamples)
                    env *= (float)((total - pos) / fadeOutSamples);
                dest[i * 2] += l * v.Volume * env;
                dest[i * 2 + 1] += r * v.Volume * env;
                v.Cursor = pos + Math.Max(0.05, v.Speed * v.Pitch);
            }
        }

        if (live)
        {
            lock (_voiceLock)
            {
                var w = 0;
                for (var i = 0; i < _voices.Length; i++)
                {
                    var v = _voices[i];
                    var keep = !v.StopRequested && (v.Loop || v.Cursor < v.Samples.Length / 2d);
                    if (keep)
                        _voices[w++] = v;
                }
                if (w != _voices.Length)
                {
                    var next = new VoicePlayback[w];
                    Array.Copy(_voices, next, w);
                    _voices = next;
                }
            }
        }
    }

    private void EnsureBuf(int frames)
    {
        var n = frames * 2;
        if (_micBuf.Length < n)
        {
            _micBuf = new float[n];
            _musicBuf = new float[n];
            _soundBuf = new float[n];
            _toneBuf = new float[n];
            _mixBuf = new float[n];
        }
    }

    private static bool AnySolo(MixerSnapshot s) => s.MicSolo || s.MusicSolo || s.SoundSolo;

    private static void GainChannel(float[] buf, int frames, float gain, bool mute, bool solo, bool anySolo)
    {
        var g = mute || (anySolo && !solo) ? 0f : Math.Clamp(gain, 0f, AudioConstants.MaxGain);
        MixNative.Scale(buf.AsSpan(0, frames * 2), g);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private void OnDevicesChanged(object? sender, DeviceListChangedEventArgs e)
    {
        StatusChanged?.Invoke(this, e.Reason);
        if (!_running)
            return;
        if (e.Reason.Contains("disconnected", StringComparison.OrdinalIgnoreCase))
        {
            if (_inputName is not null && e.DeviceId is not null && _devices.GetDevice(e.DeviceId) is null)
            {
                _micActive = false;
                RaiseError("Microphone disconnected. Music and soundboard still work.");
            }
        }
    }

    private void OnMonitorStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            _log.Warning("audio", "Headphones/speakers stopped.", e.Exception.Message);
            RaiseError("Playback device disconnected.");
        }
        _running = false;
    }

    private void OnVirtualStopped(object? sender, StoppedEventArgs e)
    {
        _virtual.LogMissing();
        RaiseError("Virtual microphone device disconnected.");
        _virtualName = null;
    }

    private void RaiseError(string message)
    {
        _log.Warning("audio", message);
        ErrorRaised?.Invoke(this, message);
    }

    private static void DisposeOut(ref WasapiOut? output)
    {
        if (output is null)
            return;
        try { output.Dispose(); } catch { }
        output = null;
    }

    public void Dispose()
    {
        _devices.DevicesChanged -= OnDevicesChanged;
        Stop();
        MusicSource.Dispose();
        _devices.Dispose();
        MediaFoundationApi.Shutdown();
    }

    private sealed class MonitorProvider : ISampleProvider
    {
        private readonly AudioEngine _engine;
        public WaveFormat WaveFormat { get; }

        public MonitorProvider(AudioEngine engine, int sampleRate)
        {
            _engine = engine;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var frames = count / 2;
            _engine.MixMonitorAndVirtual(buffer.AsSpan(offset, count), frames);
            return count;
        }
    }

    private sealed class VirtualProvider : ISampleProvider
    {
        private readonly AudioEngine _engine;
        public WaveFormat WaveFormat { get; }

        public VirtualProvider(AudioEngine engine, int sampleRate)
        {
            _engine = engine;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var frames = count / 2;
            _engine.ReadVirtual(buffer.AsSpan(offset, count), frames);
            return count;
        }
    }
}
