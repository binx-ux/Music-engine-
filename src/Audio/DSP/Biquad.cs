namespace Mixline.Audio.DSP;

public struct Biquad
{
    private float _b0, _b1, _b2, _a1, _a2;
    private float _z1, _z2;

    public void SetPeaking(float sampleRate, float freq, float q, float gainDb)
    {
        var a = MathF.Pow(10f, gainDb / 40f);
        var w0 = 2f * MathF.PI * freq / sampleRate;
        var cos = MathF.Cos(w0);
        var sin = MathF.Sin(w0);
        var alpha = sin / (2f * MathF.Max(q, 0.05f));
        var b0 = 1f + alpha * a;
        var b1 = -2f * cos;
        var b2 = 1f - alpha * a;
        var a0 = 1f + alpha / a;
        var a1 = -2f * cos;
        var a2 = 1f - alpha / a;
        Normalize(b0, b1, b2, a0, a1, a2);
    }

    public void SetLowShelf(float sampleRate, float freq, float q, float gainDb)
    {
        var a = MathF.Pow(10f, gainDb / 40f);
        var w0 = 2f * MathF.PI * freq / sampleRate;
        var cos = MathF.Cos(w0);
        var sin = MathF.Sin(w0);
        var alpha = sin / (2f * MathF.Max(q, 0.05f));
        var twoSqrtAAlpha = 2f * MathF.Sqrt(a) * alpha;
        var b0 = a * ((a + 1) - (a - 1) * cos + twoSqrtAAlpha);
        var b1 = 2f * a * ((a - 1) - (a + 1) * cos);
        var b2 = a * ((a + 1) - (a - 1) * cos - twoSqrtAAlpha);
        var a0 = (a + 1) + (a - 1) * cos + twoSqrtAAlpha;
        var a1 = -2f * ((a - 1) + (a + 1) * cos);
        var a2 = (a + 1) + (a - 1) * cos - twoSqrtAAlpha;
        Normalize(b0, b1, b2, a0, a1, a2);
    }

    public void SetHighShelf(float sampleRate, float freq, float q, float gainDb)
    {
        var a = MathF.Pow(10f, gainDb / 40f);
        var w0 = 2f * MathF.PI * freq / sampleRate;
        var cos = MathF.Cos(w0);
        var sin = MathF.Sin(w0);
        var alpha = sin / (2f * MathF.Max(q, 0.05f));
        var twoSqrtAAlpha = 2f * MathF.Sqrt(a) * alpha;
        var b0 = a * ((a + 1) + (a - 1) * cos + twoSqrtAAlpha);
        var b1 = -2f * a * ((a - 1) + (a + 1) * cos);
        var b2 = a * ((a + 1) + (a - 1) * cos - twoSqrtAAlpha);
        var a0 = (a + 1) - (a - 1) * cos + twoSqrtAAlpha;
        var a1 = 2f * ((a - 1) - (a + 1) * cos);
        var a2 = (a + 1) - (a - 1) * cos - twoSqrtAAlpha;
        Normalize(b0, b1, b2, a0, a1, a2);
    }

    public void SetHighPass(float sampleRate, float freq, float q)
    {
        var w0 = 2f * MathF.PI * freq / sampleRate;
        var cos = MathF.Cos(w0);
        var sin = MathF.Sin(w0);
        var alpha = sin / (2f * MathF.Max(q, 0.05f));
        var b0 = (1f + cos) / 2f;
        var b1 = -(1f + cos);
        var b2 = (1f + cos) / 2f;
        var a0 = 1f + alpha;
        var a1 = -2f * cos;
        var a2 = 1f - alpha;
        Normalize(b0, b1, b2, a0, a1, a2);
    }

    public void SetLowPass(float sampleRate, float freq, float q)
    {
        var w0 = 2f * MathF.PI * freq / sampleRate;
        var cos = MathF.Cos(w0);
        var sin = MathF.Sin(w0);
        var alpha = sin / (2f * MathF.Max(q, 0.05f));
        var b0 = (1f - cos) / 2f;
        var b1 = 1f - cos;
        var b2 = (1f - cos) / 2f;
        var a0 = 1f + alpha;
        var a1 = -2f * cos;
        var a2 = 1f - alpha;
        Normalize(b0, b1, b2, a0, a1, a2);
    }

    private void Normalize(float b0, float b1, float b2, float a0, float a1, float a2)
    {
        _b0 = b0 / a0;
        _b1 = b1 / a0;
        _b2 = b2 / a0;
        _a1 = a1 / a0;
        _a2 = a2 / a0;
    }

    public float Process(float x)
    {
        var y = _b0 * x + _z1;
        _z1 = _b1 * x - _a1 * y + _z2;
        _z2 = _b2 * x - _a2 * y;
        return y;
    }

    public void Reset()
    {
        _z1 = 0;
        _z2 = 0;
    }
}
