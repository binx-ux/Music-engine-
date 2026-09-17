using System.Runtime.InteropServices;

namespace Mixline.Audio.Mixer;

public static unsafe class MixNative
{
    private static readonly bool Native;
    private static readonly bool Tune;

    static MixNative()
    {
        try
        {
            var one = stackalloc float[1];
            one[0] = 1f;
            NativeScale(one, 1, 1f);
            Native = true;
        }
        catch
        {
            Native = false;
        }

        try
        {
            var probe = stackalloc float[1024];
            NativeYin(probe, 1024, 48000, 0.15f);
            Tune = true;
        }
        catch
        {
            Tune = false;
        }
    }

    public static bool UsingRust => Native;

    public static void Scale(Span<float> buf, float g)
    {
        if (buf.IsEmpty)
            return;
        if (Native)
        {
            fixed (float* p = buf)
                NativeScale(p, (nuint)buf.Length, g);
            return;
        }
        for (var i = 0; i < buf.Length; i++)
            buf[i] *= g;
    }

    public static void Add(Span<float> dest, ReadOnlySpan<float> src, float gain)
    {
        var n = Math.Min(dest.Length, src.Length);
        if (n <= 0)
            return;
        if (Native)
        {
            fixed (float* d = dest)
            fixed (float* s = src)
                NativeAdd(d, s, (nuint)n, gain);
            return;
        }
        for (var i = 0; i < n; i++)
            dest[i] += src[i] * gain;
    }

    public static float Peak(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty)
            return 0f;
        if (Native)
        {
            fixed (float* p = samples)
                return NativePeak(p, (nuint)samples.Length);
        }
        var peak = 0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var a = MathF.Abs(samples[i]);
            if (a > peak)
                peak = a;
        }
        return peak;
    }

    [DllImport("cuebox_dsp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "cuebox_scale")]
    private static extern void NativeScale(float* buf, nuint len, float gain);

    [DllImport("cuebox_dsp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "cuebox_add")]
    private static extern void NativeAdd(float* dest, float* src, nuint len, float gain);

    [DllImport("cuebox_dsp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "cuebox_peak")]
    private static extern float NativePeak(float* buf, nuint len);

    public static float Yin(ReadOnlySpan<float> samples, int sampleRate)
    {
        if (samples.IsEmpty || !Tune)
            return 0f;
        fixed (float* p = samples)
            return NativeYin(p, (nuint)samples.Length, sampleRate, 0.12f);
    }

    public static float SnapHz(float freq, int key, int scale)
    {
        if (!Tune || freq <= 0f)
            return 0f;
        return NativeSnap(freq, key, scale);
    }

    public static void PitchShift(
        Span<float> stereo,
        int frames,
        float[] delay,
        ref int write,
        ref float read,
        float ratio,
        float amount,
        bool formant)
    {
        if (frames <= 0 || delay.Length < 64)
            return;
        if (!Tune)
        {
            ShiftManaged(stereo, frames, delay, ref write, ref read, ratio, amount, formant);
            return;
        }
        var w = write;
        var r = read;
        fixed (float* s = stereo)
        fixed (float* d = delay)
            NativeShift(s, (nuint)frames, d, (nuint)delay.Length, &w, &r, ratio, amount, formant ? 1 : 0);
        write = w;
        read = r;
    }

    private static void ShiftManaged(
        Span<float> stereo,
        int frames,
        float[] delay,
        ref int write,
        ref float read,
        float ratio,
        float amount,
        bool formant)
    {
        var n = delay.Length;
        var dl = (float)n;
        var third = dl / 3f;
        ratio = Math.Clamp(ratio, 0.5f, 2f);
        amount = Math.Clamp(amount, 0f, 1f);
        var w = ((write % n) + n) % n;
        var rp = Wrap(read, dl);
        for (var i = 0; i < frames; i++)
        {
            var dry = 0.5f * (stereo[i * 2] + stereo[i * 2 + 1]);
            delay[w] = dry;
            rp = Wrap(rp + ratio, dl);
            var r2 = Wrap(rp + third, dl);
            var r3 = Wrap(rp + third * 2f, dl);
            var g1 = TapGain(rp, w, n);
            var g2 = TapGain(r2, w, n);
            var g3 = TapGain(r3, w, n);
            var gsum = MathF.Max(g1 + g2 + g3, 1e-4f);
            var wet = (Interp(delay, rp) * g1 + Interp(delay, r2) * g2 + Interp(delay, r3) * g3) / gsum;
            if (formant)
            {
                var k = ratio >= 1f ? Math.Clamp(2.15f - ratio, 0.42f, 1f) : Math.Clamp(0.55f + ratio * 0.45f, 0.55f, 1f);
                wet = wet * k + dry * (1f - k) * 0.4f;
            }
            var o = dry + (wet - dry) * amount;
            stereo[i * 2] = o;
            stereo[i * 2 + 1] = o;
            w++;
            if (w >= n)
                w = 0;
        }
        write = w;
        read = rp;
    }

    private static float Wrap(float x, float n)
    {
        var v = x % n;
        return v < 0f ? v + n : v;
    }

    private static float Interp(float[] d, float pos)
    {
        var n = d.Length;
        if (n < 4)
            return 0f;
        var p = Wrap(pos, n);
        var i1 = (int)MathF.Floor(p);
        var f = p - i1;
        var y0 = d[Mod(i1 - 1, n)];
        var y1 = d[Mod(i1, n)];
        var y2 = d[Mod(i1 + 1, n)];
        var y3 = d[Mod(i1 + 2, n)];
        var a0 = y3 - y2 - y0 + y1;
        var a1 = y0 - y1 - a0;
        var a2 = y2 - y0;
        return ((a0 * f + a1) * f + a2) * f + y1;
    }

    private static int Mod(int i, int n)
    {
        var v = i % n;
        return v < 0 ? v + n : v;
    }

    private static float TapGain(float read, int write, int len)
    {
        var dist = Wrap(write - read, len) / len;
        return 0.5f - 0.5f * MathF.Cos(MathF.Tau * dist);
    }

    [DllImport("cuebox_dsp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "cuebox_route")]
    internal static extern void NativeRoute(
        float* dest,
        float* a, float ga,
        float* b, float gb,
        float* c, float gc,
        nuint len,
        float master);

    [DllImport("cuebox_dsp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "cuebox_yin")]
    private static extern float NativeYin(float* buf, nuint len, int sampleRate, float thresh);

    [DllImport("cuebox_dsp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "cuebox_snap_hz")]
    private static extern float NativeSnap(float freq, int key, int scale);

    [DllImport("cuebox_dsp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "cuebox_pitch_shift")]
    private static extern void NativeShift(
        float* stereo,
        nuint frames,
        float* delay,
        nuint delayLen,
        int* write,
        float* read,
        float ratio,
        float amount,
        int formant);
}
