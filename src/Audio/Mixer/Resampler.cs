namespace Mixline.Audio.Mixer;

public sealed class CubicResampler
{
    private readonly float[] _hist = new float[8];
    private double _pos;
    private readonly int _channels;

    public CubicResampler(int channels)
    {
        _channels = Math.Max(1, channels);
    }

    public int Process(ReadOnlySpan<float> input, int inFrames, Span<float> output, int outFrames, double ratio)
    {
        if (Math.Abs(ratio - 1.0) < 0.0000001 && inFrames >= outFrames)
        {
            var copy = Math.Min(inFrames, outFrames) * _channels;
            input[..copy].CopyTo(output);
            return Math.Min(inFrames, outFrames);
        }

        var inIndex = 0;
        var produced = 0;
        while (produced < outFrames)
        {
            while (_pos >= 1.0)
            {
                _pos -= 1.0;
                if (inIndex >= inFrames)
                    return produced;
                PushFrame(input, inIndex);
                inIndex++;
            }

            var t = (float)_pos;
            for (var c = 0; c < _channels; c++)
            {
                var y0 = _hist[c];
                var y1 = _hist[_channels + c];
                var y2 = _hist[_channels * 2 + c];
                var y3 = _hist[_channels * 3 + c];
                output[produced * _channels + c] = Hermite(t, y0, y1, y2, y3);
            }

            produced++;
            _pos += ratio;
        }

        while (_pos >= 1.0 && inIndex < inFrames)
        {
            _pos -= 1.0;
            PushFrame(input, inIndex);
            inIndex++;
        }

        return produced;
    }

    private void PushFrame(ReadOnlySpan<float> input, int frame)
    {
        var ch = _channels;
        Array.Copy(_hist, ch, _hist, 0, ch * 3);
        var src = frame * ch;
        for (var c = 0; c < ch; c++)
            _hist[ch * 3 + c] = input[src + c];
    }

    private static float Hermite(float t, float y0, float y1, float y2, float y3)
    {
        var c0 = y1;
        var c1 = 0.5f * (y2 - y0);
        var c2 = y0 - 2.5f * y1 + 2f * y2 - 0.5f * y3;
        var c3 = 0.5f * (y3 - y0) + 1.5f * (y1 - y2);
        return ((c3 * t + c2) * t + c1) * t + c0;
    }

    public void Reset()
    {
        Array.Clear(_hist);
        _pos = 0;
    }
}

public static class ChannelConvert
{
    public static void ToStereo(ReadOnlySpan<float> input, int inChannels, Span<float> stereo, int frames)
    {
        if (inChannels == 2)
        {
            input[..(frames * 2)].CopyTo(stereo);
            return;
        }

        if (inChannels == 1)
        {
            for (var i = 0; i < frames; i++)
            {
                var s = input[i];
                stereo[i * 2] = s;
                stereo[i * 2 + 1] = s;
            }
            return;
        }

        for (var i = 0; i < frames; i++)
        {
            stereo[i * 2] = input[i * inChannels];
            stereo[i * 2 + 1] = inChannels > 1 ? input[i * inChannels + 1] : input[i * inChannels];
        }
    }

    public static void FromStereo(ReadOnlySpan<float> stereo, Span<float> output, int outChannels, int frames)
    {
        if (outChannels == 2)
        {
            stereo[..(frames * 2)].CopyTo(output);
            return;
        }

        if (outChannels == 1)
        {
            for (var i = 0; i < frames; i++)
                output[i] = 0.5f * (stereo[i * 2] + stereo[i * 2 + 1]);
            return;
        }

        for (var i = 0; i < frames; i++)
        {
            output[i * outChannels] = stereo[i * 2];
            if (outChannels > 1)
                output[i * outChannels + 1] = stereo[i * 2 + 1];
            for (var c = 2; c < outChannels; c++)
                output[i * outChannels + c] = 0f;
        }
    }

    public static void ApplyPan(Span<float> stereo, int frames, float pan)
    {
        var p = Math.Clamp(pan, -1f, 1f);
        var left = MathF.Sqrt(0.5f * (1f - p));
        var right = MathF.Sqrt(0.5f * (1f + p));
        for (var i = 0; i < frames; i++)
        {
            stereo[i * 2] *= left;
            stereo[i * 2 + 1] *= right;
        }
    }
}
