using Mixline.Core;

namespace Mixline.Audio.DSP;

public sealed class PitchCorrector
{
    private readonly float[] _buf = new float[2048];
    private int _fill;
    private float _targetRatio = 1f;
    private float _ratio = 1f;
    private float _phase;
    private int _sampleRate = AudioConstants.DefaultSampleRate;
    private AutotuneMode _mode = AutotuneMode.Off;
    private AutotuneSettings _settings = new();
    private float _amount;

    public void Configure(int sampleRate, AutotuneMode mode, AutotuneSettings settings)
    {
        _sampleRate = sampleRate;
        _mode = mode;
        _settings = settings;
        _amount = mode switch
        {
            AutotuneMode.Light => 0.25f * settings.Amount,
            AutotuneMode.Medium => 0.55f * settings.Amount,
            AutotuneMode.Strong => MathF.Max(0.75f, settings.Amount),
            _ => 0f
        };
    }

    public void ProcessStereo(Span<float> buffer, int frames)
    {
        if (_mode == AutotuneMode.Off || _amount <= 0.001f)
            return;

        for (var i = 0; i < frames; i++)
        {
            var sample = 0.5f * (buffer[i * 2] + buffer[i * 2 + 1]);
            if (_fill < _buf.Length)
                _buf[_fill++] = sample;

            if (_fill >= 1024)
            {
                var freq = DetectPitch(_buf.AsSpan(0, 1024), _sampleRate);
                if (freq > 70f && freq < 800f)
                {
                    var snapped = Snap(freq, _settings.Key, _settings.Scale);
                    var desired = snapped / freq;
                    desired = Math.Clamp(desired, 0.5f, 2f);
                    var speed = 0.02f + _settings.RetuneSpeed * 0.2f;
                    _targetRatio += speed * (desired - _targetRatio);
                }
                Array.Copy(_buf, 512, _buf, 0, _fill - 512);
                _fill -= 512;
            }

            _ratio += 0.05f * (_targetRatio - _ratio);
            var shift = 1f + (_ratio - 1f) * _amount;
            _phase += shift;
            if (_phase >= 1f)
                _phase -= 1f;

            if (_settings.FormantPreservation)
            {
                var wet = sample * (2f - shift);
                buffer[i * 2] = buffer[i * 2] + (wet - buffer[i * 2]) * _amount * 0.35f;
                buffer[i * 2 + 1] = buffer[i * 2 + 1] + (wet - buffer[i * 2 + 1]) * _amount * 0.35f;
            }
            else
            {
                var wet = sample * shift;
                buffer[i * 2] = Lerp(buffer[i * 2], wet, _amount);
                buffer[i * 2 + 1] = Lerp(buffer[i * 2 + 1], wet, _amount);
            }
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float DetectPitch(ReadOnlySpan<float> x, int sampleRate)
    {
        var n = x.Length;
        var minLag = sampleRate / 800;
        var maxLag = Math.Min(sampleRate / 70, n / 2);
        var bestLag = minLag;
        var best = float.MinValue;
        for (var lag = minLag; lag <= maxLag; lag++)
        {
            var sum = 0f;
            for (var i = 0; i < n - lag; i++)
                sum += x[i] * x[i + lag];
            if (sum > best)
            {
                best = sum;
                bestLag = lag;
            }
        }
        return sampleRate / (float)bestLag;
    }

    private static readonly int[] Major = [0, 2, 4, 5, 7, 9, 11];
    private static readonly int[] Minor = [0, 2, 3, 5, 7, 8, 10];

    private static float Snap(float freq, int key, MusicalScale scale)
    {
        var midi = 69f + 12f * MathF.Log2(freq / 440f);
        var nearest = MathF.Round(midi);
        if (scale != MusicalScale.Chromatic)
        {
            var pc = ((int)nearest - key) % 12;
            if (pc < 0) pc += 12;
            var allowed = scale == MusicalScale.Major ? Major : Minor;
            if (Array.IndexOf(allowed, pc) < 0)
            {
                var down = nearest - 1;
                var up = nearest + 1;
                for (var i = 0; i < 6; i++)
                {
                    var dpc = ((int)down - key) % 12;
                    if (dpc < 0) dpc += 12;
                    if (Array.IndexOf(allowed, dpc) >= 0)
                    {
                        nearest = down;
                        break;
                    }
                    var upc = ((int)up - key) % 12;
                    if (upc < 0) upc += 12;
                    if (Array.IndexOf(allowed, upc) >= 0)
                    {
                        nearest = up;
                        break;
                    }
                    down--;
                    up++;
                }
            }
        }
        return 440f * MathF.Pow(2f, (nearest - 69f) / 12f);
    }
}
