using Mixline.Audio.Mixer;
using Mixline.Core;

namespace Mixline.Audio.DSP;

public sealed class Compressor
{
    private float _envelope;
    private float _gainLin = 1f;
    private float _attack;
    private float _release;
    private float _thresholdLin;
    private float _ratio = 3f;
    private float _makeup = 1f;
    private float _gr = 1f;
    private int _sampleRate = AudioConstants.DefaultSampleRate;

    public float GainReductionDb => -Gain.ToDb(_gr);

    public void Configure(int sampleRate, CompressorSettings settings)
    {
        _sampleRate = sampleRate;
        _thresholdLin = Gain.FromDb(settings.ThresholdDb);
        _ratio = MathF.Max(1f, settings.Ratio);
        _makeup = Gain.FromDb(settings.MakeupDb);
        _attack = TimeCoeff(settings.AttackMs, sampleRate);
        _release = TimeCoeff(settings.ReleaseMs, sampleRate);
    }

    public void ProcessStereo(Span<float> buffer, int frames)
    {
        var minGr = 1f;
        for (var i = 0; i < frames; i++)
        {
            var l = buffer[i * 2];
            var r = buffer[i * 2 + 1];
            var peak = MathF.Max(MathF.Abs(l), MathF.Abs(r));
            var coeff = peak > _envelope ? _attack : _release;
            _envelope += coeff * (peak - _envelope);

            var over = _envelope / MathF.Max(_thresholdLin, 1e-6f);
            var target = 1f;
            if (over > 1f)
            {
                var overDb = Gain.ToDb(over);
                var grDb = overDb - (overDb / _ratio);
                target = Gain.FromDb(-grDb);
            }

            var gCoeff = target < _gainLin ? _attack : _release;
            _gainLin += gCoeff * (target - _gainLin);
            if (_gainLin < minGr)
                minGr = _gainLin;

            buffer[i * 2] = l * _gainLin * _makeup;
            buffer[i * 2 + 1] = r * _gainLin * _makeup;
        }
        _gr = minGr;
    }

    private static float TimeCoeff(float ms, int sampleRate)
    {
        var t = MathF.Max(ms, 0.1f) / 1000f;
        return 1f - MathF.Exp(-1f / (t * sampleRate));
    }
}
