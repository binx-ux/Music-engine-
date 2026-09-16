namespace Mixline.Audio.Mixer;

public sealed class PeakTracker
{
    private float _peak;
    private float _hold;
    private int _holdSamples;
    private readonly int _holdTime;

    public PeakTracker(int sampleRate)
    {
        _holdTime = Math.Max(1, sampleRate / 4);
    }

    public float Peak => _peak;
    public float Hold => _hold;

    public void Process(ReadOnlySpan<float> samples)
    {
        var peak = MixNative.Peak(samples);
        _peak = peak;
        if (peak >= _hold)
        {
            _hold = peak;
            _holdSamples = _holdTime;
        }
        else if (_holdSamples > 0)
        {
            _holdSamples -= samples.Length;
        }
        else
        {
            _hold *= 0.97f;
        }
    }

    public void Decay()
    {
        _peak *= 0.82f;
    }
}

public static class Gain
{
    public static float FromDb(float db) => MathF.Pow(10f, db / 20f);
    public static float ToDb(float linear)
    {
        var v = MathF.Max(linear, 1e-8f);
        return 20f * MathF.Log10(v);
    }
}
