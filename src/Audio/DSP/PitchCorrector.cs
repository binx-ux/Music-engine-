using Mixline.Audio.Mixer;
using Mixline.Core;

namespace Mixline.Audio.DSP;

public sealed class PitchCorrector
{
    private readonly float[] _ana = new float[2048];
    private readonly float[] _win = new float[2048];
    private readonly float[] _delay = new float[8192];
    private readonly float[] _hist = new float[5];
    private int _histCount;
    private int _histWrite;
    private int _fill;
    private int _write;
    private float _read;
    private float _targetRatio = 1f;
    private float _ratio = 1f;
    private float _prevHz;
    private int _sampleRate = AudioConstants.DefaultSampleRate;
    private AutotuneMode _mode = AutotuneMode.Off;
    private AutotuneSettings _settings = new();
    private float _amount;
    private float _slew;
    private int _hold;

    public float LastHz { get; private set; }

    public void Configure(int sampleRate, AutotuneMode mode, AutotuneSettings settings)
    {
        if (_sampleRate != sampleRate)
        {
            Array.Clear(_delay);
            Array.Clear(_ana);
            Array.Clear(_hist);
            _write = 0;
            _read = 0f;
            _fill = 0;
            _histCount = 0;
            _prevHz = 0f;
        }
        _sampleRate = sampleRate;
        _mode = mode;
        _settings = settings;
        _amount = mode switch
        {
            AutotuneMode.Light => 0.42f + 0.5f * settings.Amount,
            AutotuneMode.Medium => 0.72f + 0.26f * settings.Amount,
            AutotuneMode.Strong => 0.92f + 0.08f * settings.Amount,
            _ => 0f
        };
        var tauMs = mode switch
        {
            AutotuneMode.Strong => 0.8f + (1f - settings.RetuneSpeed) * 8f,
            AutotuneMode.Medium => 6f + (1f - settings.RetuneSpeed) * 28f,
            AutotuneMode.Light => 18f + (1f - settings.RetuneSpeed) * 70f,
            _ => 40f
        };
        _slew = 1f - MathF.Exp(-1f / MathF.Max(1f, tauMs * 0.001f * sampleRate));
    }

    public void ProcessStereo(Span<float> buffer, int frames)
    {
        if (_mode == AutotuneMode.Off || _amount <= 0.001f)
        {
            LastHz = 0f;
            return;
        }

        var rms = 0f;
        for (var i = 0; i < frames; i++)
        {
            var sample = 0.5f * (buffer[i * 2] + buffer[i * 2 + 1]);
            rms += sample * sample;
            if (_fill < _ana.Length)
                _ana[_fill++] = sample;
        }
        rms = MathF.Sqrt(rms / Math.Max(1, frames));

        if (_fill >= 1024)
        {
            var n = Math.Min(_fill, 2048);
            Hann(_ana.AsSpan(0, n), _win);
            var freq = MixNative.Yin(_win.AsSpan(0, n), _sampleRate);
            if (freq <= 0f)
                freq = DetectPitch(_win.AsSpan(0, n), _sampleRate);
            freq = Stabilize(freq, rms);

            if (freq > 70f && freq < 900f)
            {
                LastHz = freq;
                var snapped = MixNative.SnapHz(freq, _settings.Key, (int)_settings.Scale);
                if (snapped <= 0f)
                    snapped = Snap(freq, _settings.Key, _settings.Scale);
                _targetRatio = Math.Clamp(snapped / freq, 0.5f, 2f);
                _hold = (int)(_sampleRate * 0.05f);
            }
            else
            {
                _hold -= frames;
                if (_hold <= 0)
                {
                    _targetRatio += 0.06f * (1f - _targetRatio);
                    LastHz = 0f;
                }
            }

            var hop = Math.Min(256, _fill / 2);
            var keep = _fill - hop;
            Array.Copy(_ana, hop, _ana, 0, keep);
            _fill = keep;
        }

        var steps = Math.Max(1, frames);
        _ratio += (1f - MathF.Pow(1f - _slew, steps)) * (_targetRatio - _ratio);
        var shift = 1f + (_ratio - 1f) * _amount;
        MixNative.PitchShift(
            buffer,
            frames,
            _delay,
            ref _write,
            ref _read,
            shift,
            _amount,
            _settings.FormantPreservation);
    }

    private float Stabilize(float hz, float rms)
    {
        if (rms < 0.012f || hz <= 0f)
            return 0f;

        if (_prevHz > 70f)
        {
            var oct = hz / _prevHz;
            if (oct > 1.87f && oct < 2.15f)
                hz *= 0.5f;
            else if (oct > 0.46f && oct < 0.54f)
                hz *= 2f;
        }

        _hist[_histWrite] = hz;
        _histWrite = (_histWrite + 1) % _hist.Length;
        if (_histCount < _hist.Length)
            _histCount++;

        Span<float> tmp = stackalloc float[5];
        for (var i = 0; i < _histCount; i++)
            tmp[i] = _hist[i];
        tmp[.._histCount].Sort();
        var mid = tmp[_histCount / 2];
        _prevHz = mid;
        return mid;
    }

    private static void Hann(ReadOnlySpan<float> src, float[] dest)
    {
        var n = src.Length;
        var s = MathF.Tau / Math.Max(1, n - 1);
        for (var i = 0; i < n; i++)
            dest[i] = src[i] * (0.5f - 0.5f * MathF.Cos(s * i));
    }

    private static float DetectPitch(ReadOnlySpan<float> x, int sampleRate)
    {
        var n = x.Length;
        if (n < 64)
            return 0f;
        var minLag = Math.Max(2, sampleRate / 900);
        var maxLag = Math.Min(sampleRate / 70, n / 2 - 2);
        if (maxLag <= minLag)
            return 0f;
        var bestLag = minLag;
        var best = float.MaxValue;
        var running = 0f;
        for (var lag = 1; lag <= maxLag; lag++)
        {
            var sum = 0f;
            var last = n - lag;
            for (var i = 0; i < last; i++)
            {
                var d = x[i] - x[i + lag];
                sum += d * d;
            }
            running += sum;
            var cmndf = sum * lag / MathF.Max(running, 1e-12f);
            if (lag >= minLag && cmndf < best)
            {
                best = cmndf;
                bestLag = lag;
            }
        }
        if (best > 0.28f)
            return 0f;
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
            var allowed = scale == MusicalScale.Major ? Major : Minor;
            if (!InScale(nearest, key, allowed))
            {
                var down = nearest - 1;
                var up = nearest + 1;
                for (var i = 0; i < 6; i++)
                {
                    if (InScale(down, key, allowed))
                    {
                        nearest = down;
                        break;
                    }
                    if (InScale(up, key, allowed))
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

    private static bool InScale(float midi, int key, int[] allowed)
    {
        var pc = ((int)midi - key) % 12;
        if (pc < 0) pc += 12;
        return Array.IndexOf(allowed, pc) >= 0;
    }
}
