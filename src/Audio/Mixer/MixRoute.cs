using System.Runtime.InteropServices;

namespace Mixline.Audio.Mixer;

public static unsafe class MixRoute
{
    public static void Bus(Span<float> dest, ReadOnlySpan<float> a, float ga, ReadOnlySpan<float> b, float gb, ReadOnlySpan<float> c, float gc, float master)
    {
        var n = dest.Length;
        if (n <= 0)
            return;
        dest.Clear();
        if (MixNative.UsingRust && a.Length >= n && b.Length >= n && c.Length >= n)
        {
            fixed (float* d = dest)
            fixed (float* pa = a)
            fixed (float* pb = b)
            fixed (float* pc = c)
                MixNative.NativeRoute(d, pa, ga, pb, gb, pc, gc, (nuint)n, master);
            return;
        }
        MixNative.Add(dest, a, ga * master);
        MixNative.Add(dest, b, gb * master);
        MixNative.Add(dest, c, gc * master);
    }
}
