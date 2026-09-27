using NAudio.Dsp;
using NAudio.Wave;

namespace Mixline.Audio.Sources;

public sealed class HqResampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly WdlResampler _resampler;
    private readonly int _channels;

    public WaveFormat WaveFormat { get; }

    public HqResampleProvider(ISampleProvider source, int newSampleRate)
    {
        _source = source;
        _channels = source.WaveFormat.Channels;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(newSampleRate, _channels);
        _resampler = new WdlResampler();
        _resampler.SetMode(true, 2, true, 128, 64);
        _resampler.SetFilterParms(0.97f, 0.85f);
        _resampler.SetFeedMode(false);
        _resampler.SetRates(source.WaveFormat.SampleRate, newSampleRate);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var outFrames = count / _channels;
        var needed = _resampler.ResamplePrepare(outFrames, _channels, out var input, out var inOffset);
        var got = _source.Read(input, inOffset, needed * _channels) / _channels;
        return _resampler.ResampleOut(buffer, offset, got, outFrames, _channels) * _channels;
    }
}

public sealed class HqBlockResampler
{
    private readonly WdlResampler _resampler;
    private readonly int _channels;
    private readonly bool _passthrough;

    public HqBlockResampler(int inRate, int outRate, int channels)
    {
        _channels = Math.Max(1, channels);
        _passthrough = inRate == outRate;
        _resampler = new WdlResampler();
        _resampler.SetMode(true, 2, true, 128, 64);
        _resampler.SetFilterParms(0.97f, 0.85f);
        _resampler.SetFeedMode(false);
        _resampler.SetRates(Math.Max(1, inRate), Math.Max(1, outRate));
    }

    public int Process(float[] input, int inFrames, float[] output)
    {
        if (inFrames <= 0)
            return 0;
        if (_passthrough)
        {
            var n = Math.Min(inFrames * _channels, output.Length);
            Array.Copy(input, output, n);
            return n / _channels;
        }

        var outFrames = output.Length / _channels;
        if (outFrames < 1)
            return 0;
        var prepared = _resampler.ResamplePrepare(outFrames, _channels, out var inBuf, out var inOff);
        var copyFrames = Math.Min(inFrames, prepared);
        if (copyFrames < 1)
            return 0;
        Array.Copy(input, 0, inBuf, inOff, copyFrames * _channels);
        return _resampler.ResampleOut(output, 0, copyFrames, outFrames, _channels);
    }
}
