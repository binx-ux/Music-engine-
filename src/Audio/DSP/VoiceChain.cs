using Mixline.Core;

namespace Mixline.Audio.DSP;

public sealed class VoiceChain
{
    private readonly HighPassFilter _hpf = new();
    private readonly NoiseGate _gate = new();
    private readonly NoiseReducer _nr = new();
    private readonly ParametricEq _eq = new();
    private readonly Compressor _comp = new();
    private readonly DeEsser _de = new();
    private readonly PitchCorrector _tune = new();
    private readonly Saturation _sat = new();
    private readonly Limiter _limiter = new();
    private VoiceSettings _settings = new();
    private int _sampleRate = AudioConstants.DefaultSampleRate;

    public bool GateOpen => _gate.IsOpen;
    public float GainReductionDb => _comp.GainReductionDb;

    public void Configure(int sampleRate, VoiceSettings settings)
    {
        _sampleRate = sampleRate;
        _settings = settings;
        var voice = settings.VoiceEnhance ? Enhanced(settings) : settings;
        _hpf.Configure(sampleRate, voice.HighPassHz);
        _gate.Configure(sampleRate, voice.GateSettings);
        _nr.Configure(voice.NoiseReductionAmount);
        _eq.Configure(sampleRate, voice.EqSettings);
        _comp.Configure(sampleRate, voice.CompressorSettings);
        _de.Configure(sampleRate, voice.DeEsserAmount);
        _tune.Configure(sampleRate, voice.Autotune, voice.AutotuneSettings);
        _sat.Configure(voice.SaturationAmount);
    }

    public void Process(Span<float> stereo, int frames, bool bypass)
    {
        if (bypass)
            return;

        var s = _settings.VoiceEnhance ? Enhanced(_settings) : _settings;
        if (s.HighPass || s.VoiceEnhance)
            _hpf.ProcessStereo(stereo, frames);
        if (s.Gate || s.VoiceEnhance)
            _gate.ProcessStereo(stereo, frames);
        if (s.NoiseReduction)
            _nr.ProcessStereo(stereo, frames);
        if (s.Eq || s.VoiceEnhance)
            _eq.ProcessStereo(stereo, frames);
        if (s.Compressor || s.VoiceEnhance)
            _comp.ProcessStereo(stereo, frames);
        if (s.DeEsser)
            _de.ProcessStereo(stereo, frames);
        if (s.Autotune != AutotuneMode.Off)
            _tune.ProcessStereo(stereo, frames);
        if (s.Saturation)
            _sat.ProcessStereo(stereo, frames);
        if (s.Limiter || s.VoiceEnhance)
            _limiter.ProcessStereo(stereo, frames);
    }

    private static VoiceSettings Enhanced(VoiceSettings src)
    {
        return new VoiceSettings
        {
            VoiceEnhance = true,
            HighPass = true,
            HighPassHz = MathF.Max(src.HighPassHz, 80f),
            Gate = true,
            GateSettings = new GateSettings
            {
                ThresholdDb = MathF.Min(src.GateSettings.ThresholdDb, -46f),
                AttackMs = 4f,
                HoldMs = 60f,
                ReleaseMs = 90f
            },
            Eq = true,
            EqSettings = EqPresets.Create("Voice"),
            EqPreset = "Voice",
            Compressor = true,
            CompressorSettings = new CompressorSettings
            {
                ThresholdDb = -20f,
                Ratio = 2.8f,
                AttackMs = 10f,
                ReleaseMs = 70f,
                MakeupDb = 2.5f
            },
            DeEsser = src.DeEsser,
            DeEsserAmount = src.DeEsserAmount,
            NoiseReduction = src.NoiseReduction,
            NoiseReductionAmount = MathF.Min(src.NoiseReductionAmount, 0.3f),
            Saturation = src.Saturation,
            SaturationAmount = src.SaturationAmount,
            Limiter = true,
            Autotune = src.Autotune,
            AutotuneSettings = src.AutotuneSettings
        };
    }
}
