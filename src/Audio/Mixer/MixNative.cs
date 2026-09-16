using System.Runtime.InteropServices;

namespace Mixline.Audio.Mixer;

public static unsafe class MixNative
{
    private static readonly bool Native;

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

    [DllImport("cuebox_dsp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "cuebox_route")]
    internal static extern void NativeRoute(
        float* dest,
        float* a, float ga,
        float* b, float gb,
        float* c, float gc,
        nuint len,
        float master);
}
