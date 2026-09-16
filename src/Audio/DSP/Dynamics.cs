using Mixline.Audio.Mixer;
using Mixline.Core;

namespace Mixline.Audio.DSP;

public sealed class NoiseGate
{
    private float _envelope;
    private float _gain = 1f;
    private float _threshold;
    private float _attack;
    private float _release;
    private int _holdSamples;
    private int _holdLeft;
    private bool _open;

    public bool IsOpen => _open;

    public void Configure(int sampleRate, GateSettings settings)
    {
        _threshold = Gain.FromDb(settings.ThresholdDb);
        _attack = 1f - MathF.Exp(-1f / (MathF.Max(settings.AttackMs, 0.5f) / 1000f * sampleRate));
        _release = 1f - MathF.Exp(-1f / (MathF.Max(settings.ReleaseMs, 1f) / 1000f * sampleRate));
        _holdSamples = Math.Max(1, (int)(settings.HoldMs / 1000f * sampleRate));
    }

    public void ProcessStereo(Span<float> buffer, int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            var peak = MathF.Max(MathF.Abs(buffer[i * 2]), MathF.Abs(buffer[i * 2 + 1]));
            _envelope += (peak > _envelope ? 0.4f : 0.05f) * (peak - _envelope);

            if (_envelope >= _threshold)
            {
                _open = true;
                _holdLeft = _holdSamples;
            }
            else if (_holdLeft > 0)
            {
                _holdLeft--;
            }
            else
            {
                _open = false;
            }

            var target = _open ? 1f : 0f;
            var coeff = target > _gain ? _attack : _release;
            _gain += coeff * (target - _gain);
            buffer[i * 2] *= _gain;
            buffer[i * 2 + 1] *= _gain;
        }
    }
}

public sealed class Limiter
{
    private float _gain = 1f;
    private readonly float _ceiling;
    private readonly float _release;

    public Limiter(float ceiling = AudioConstants.DefaultLimiterCeiling, float releaseMs = 50f, int sampleRate = AudioConstants.DefaultSampleRate)
    {
        _ceiling = ceiling;
        _release = 1f - MathF.Exp(-1f / (releaseMs / 1000f * sampleRate));
    }

    public void ProcessStereo(Span<float> buffer, int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            var peak = MathF.Max(MathF.Abs(buffer[i * 2]), MathF.Abs(buffer[i * 2 + 1]));
            var needed = peak > _ceiling ? _ceiling / peak : 1f;
            if (needed < _gain)
                _gain = needed;
            else
                _gain += _release * (1f - _gain);

            buffer[i * 2] = Math.Clamp(buffer[i * 2] * _gain, -AudioConstants.PeakClip, AudioConstants.PeakClip);
            buffer[i * 2 + 1] = Math.Clamp(buffer[i * 2 + 1] * _gain, -AudioConstants.PeakClip, AudioConstants.PeakClip);
        }
    }
}

public sealed class HighPassFilter
{
    private Biquad _l;
    private Biquad _r;

    public void Configure(int sampleRate, float hz)
    {
        _l.SetHighPass(sampleRate, hz, 0.707f);
        _r = _l;
    }

    public void ProcessStereo(Span<float> buffer, int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            buffer[i * 2] = _l.Process(buffer[i * 2]);
            buffer[i * 2 + 1] = _r.Process(buffer[i * 2 + 1]);
        }
    }
}

public sealed class DeEsser
{
    private Biquad _detect;
    private float _env;
    private float _amount;

    public void Configure(int sampleRate, float amount)
    {
        _amount = Math.Clamp(amount, 0f, 1f);
        _detect.SetPeaking(sampleRate, 6500f, 2.2f, 12f);
    }

    public void ProcessStereo(Span<float> buffer, int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            var l = buffer[i * 2];
            var r = buffer[i * 2 + 1];
            var s = 0.5f * (l + r);
            var d = MathF.Abs(_detect.Process(s));
            _env += (d > _env ? 0.3f : 0.04f) * (d - _env);
            var gr = 1f / (1f + _env * _amount * 8f);
            buffer[i * 2] = l * gr;
            buffer[i * 2 + 1] = r * gr;
        }
    }
}

public sealed class Saturation
{
    private float _amount;

    public void Configure(float amount) => _amount = Math.Clamp(amount, 0f, 1f);

    public void ProcessStereo(Span<float> buffer, int frames)
    {
        var drive = 1f + _amount * 2.2f;
        var mix = _amount;
        for (var i = 0; i < buffer.Length; i++)
        {
            var x = buffer[i];
            var y = MathF.Tanh(x * drive);
            buffer[i] = x + (y - x) * mix;
        }
    }
}
