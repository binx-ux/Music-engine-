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
        _resampler.SetMode(true, 2, true);
        _resampler.SetFilterParms();
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
