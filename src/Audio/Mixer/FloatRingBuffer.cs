namespace Mixline.Audio.Mixer;

public sealed class FloatRingBuffer
{
    private readonly float[] _buffer;
    private readonly int _mask;
    private volatile int _write;
    private volatile int _read;

    public FloatRingBuffer(int capacityPow2)
    {
        if (capacityPow2 <= 0 || (capacityPow2 & (capacityPow2 - 1)) != 0)
            throw new ArgumentException("Capacity must be a power of two.");
        _buffer = new float[capacityPow2];
        _mask = capacityPow2 - 1;
    }

    public int Capacity => _buffer.Length;
    public int AvailableRead => (_write - _read) & _mask;
    public int AvailableWrite => (_mask - AvailableRead);

    public int Write(ReadOnlySpan<float> data)
    {
        var w = _write;
        var r = _read;
        var space = (_mask - ((w - r) & _mask));
        var count = Math.Min(space, data.Length);
        for (var i = 0; i < count; i++)
            _buffer[(w + i) & _mask] = data[i];
        _write = (w + count) & _mask;
        return count;
    }

    public int Read(Span<float> dest)
    {
        var w = _write;
        var r = _read;
        var avail = (w - r) & _mask;
        var count = Math.Min(avail, dest.Length);
        for (var i = 0; i < count; i++)
            dest[i] = _buffer[(r + i) & _mask];
        _read = (r + count) & _mask;
        return count;
    }

    public int ReadOrZero(Span<float> dest)
    {
        var n = Read(dest);
        if (n < dest.Length)
            dest[n..].Clear();
        return n;
    }

    public void Clear()
    {
        _read = 0;
        _write = 0;
        Array.Clear(_buffer);
    }
}
