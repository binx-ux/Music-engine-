using Mixline.Core;

namespace Mixline.Audio.DSP;

public sealed class ParametricEq
{
    private Biquad _low;
    private Biquad _lowMid;
    private Biquad _mid;
    private Biquad _highMid;
    private Biquad _high;
    private Biquad _lowR;
    private Biquad _lowMidR;
    private Biquad _midR;
    private Biquad _highMidR;
    private Biquad _highR;
    private int _sampleRate = AudioConstants.DefaultSampleRate;

    public void Configure(int sampleRate, EqSettings settings)
    {
        _sampleRate = sampleRate;
        _low.SetLowShelf(sampleRate, settings.Low.Frequency, settings.Low.Q, settings.Low.GainDb);
        _lowMid.SetPeaking(sampleRate, settings.LowMid.Frequency, settings.LowMid.Q, settings.LowMid.GainDb);
        _mid.SetPeaking(sampleRate, settings.Mid.Frequency, settings.Mid.Q, settings.Mid.GainDb);
        _highMid.SetPeaking(sampleRate, settings.HighMid.Frequency, settings.HighMid.Q, settings.HighMid.GainDb);
        _high.SetHighShelf(sampleRate, settings.High.Frequency, settings.High.Q, settings.High.GainDb);
        _lowR = _low;
        _lowMidR = _lowMid;
        _midR = _mid;
        _highMidR = _highMid;
        _highR = _high;
    }

    public void ProcessStereo(Span<float> buffer, int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            var l = buffer[i * 2];
            var r = buffer[i * 2 + 1];
            l = _high.Process(_highMid.Process(_mid.Process(_lowMid.Process(_low.Process(l)))));
            r = _highR.Process(_highMidR.Process(_midR.Process(_lowMidR.Process(_lowR.Process(r)))));
            buffer[i * 2] = l;
            buffer[i * 2 + 1] = r;
        }
    }

    public int SampleRate => _sampleRate;
}

public static class EqPresets
{
    public static readonly string[] Names =
    [
        "Flat", "Voice", "Deep Voice", "Bright Voice", "Radio", "Streamer", "Custom"
    ];

    public static EqSettings Create(string name)
    {
        return name switch
        {
            "Voice" => new EqSettings
            {
                Low = new EqBand { Frequency = 80, GainDb = -2f, Q = 0.7f },
                LowMid = new EqBand { Frequency = 250, GainDb = -1.5f, Q = 1f },
                Mid = new EqBand { Frequency = 1200, GainDb = 1.5f, Q = 1f },
                HighMid = new EqBand { Frequency = 3500, GainDb = 2.5f, Q = 0.9f },
                High = new EqBand { Frequency = 10000, GainDb = 1f, Q = 0.7f }
            },
            "Deep Voice" => new EqSettings
            {
                Low = new EqBand { Frequency = 90, GainDb = 3.5f, Q = 0.8f },
                LowMid = new EqBand { Frequency = 220, GainDb = 2f, Q = 1f },
                Mid = new EqBand { Frequency = 1000, GainDb = -1f, Q = 1f },
                HighMid = new EqBand { Frequency = 3500, GainDb = -0.5f, Q = 1f },
                High = new EqBand { Frequency = 9000, GainDb = -2f, Q = 0.7f }
            },
            "Bright Voice" => new EqSettings
            {
                Low = new EqBand { Frequency = 80, GainDb = -3f, Q = 0.7f },
                LowMid = new EqBand { Frequency = 300, GainDb = -2f, Q = 1f },
                Mid = new EqBand { Frequency = 1400, GainDb = 1f, Q = 1f },
                HighMid = new EqBand { Frequency = 4000, GainDb = 3.5f, Q = 0.9f },
                High = new EqBand { Frequency = 11000, GainDb = 2.5f, Q = 0.7f }
            },
            "Radio" => new EqSettings
            {
                Low = new EqBand { Frequency = 120, GainDb = -8f, Q = 0.7f },
                LowMid = new EqBand { Frequency = 400, GainDb = 2f, Q = 1.1f },
                Mid = new EqBand { Frequency = 1800, GainDb = 4f, Q = 0.9f },
                HighMid = new EqBand { Frequency = 4500, GainDb = 3f, Q = 1f },
                High = new EqBand { Frequency = 10000, GainDb = -4f, Q = 0.7f }
            },
            "Streamer" => new EqSettings
            {
                Low = new EqBand { Frequency = 80, GainDb = -1.5f, Q = 0.7f },
                LowMid = new EqBand { Frequency = 280, GainDb = -2.5f, Q = 1.2f },
                Mid = new EqBand { Frequency = 1100, GainDb = 1.2f, Q = 1f },
                HighMid = new EqBand { Frequency = 3200, GainDb = 2.2f, Q = 0.9f },
                High = new EqBand { Frequency = 9000, GainDb = 1.5f, Q = 0.7f }
            },
            _ => new EqSettings()
        };
    }
}
