using Mixline.Audio.Mixer;
using Mixline.Core;

namespace Mixline.Audio.DSP;

public sealed class PitchCorrector
{
    private readonly float[] _ana = new float[2048];
    private readonly float[] _delay = new float[4096];
    private int _fill;
    private int _write;
    private float _read;
    private float _targetRatio = 1f;
    private float _ratio = 1f;
    private int _sampleRate = AudioConstants.DefaultSampleRate;
    private AutotuneMode _mode = AutotuneMode.Off;
    private AutotuneSettings _settings = new();
    private float _amount;
    private float _slew;
    private int _hold;

    public void Configure(int sampleRate, AutotuneMode mode, AutotuneSettings settings)
    {
        if (_sampleRate != sampleRate)
        {
            Array.Clear(_delay);
            _write = 0;
            _read = 0f;
            _fill = 0;
        }
        _sampleRate = sampleRate;
        _mode = mode;
        _settings = settings;
        _amount = mode switch
        {
            AutotuneMode.Light => 0.5f + 0.4f * settings.Amount,
            AutotuneMode.Medium => 0.78f + 0.22f * settings.Amount,
            AutotuneMode.Strong => 0.94f + 0.06f * settings.Amount,
            _ => 0f
        };
        var tauMs = mode switch
        {
            AutotuneMode.Strong => 1.2f + (1f - settings.RetuneSpeed) * 10f,
            AutotuneMode.Medium => 7f + (1f - settings.RetuneSpeed) * 32f,
            AutotuneMode.Light => 22f + (1f - settings.RetuneSpeed) * 80f,
            _ => 40f
        };
        _slew = 1f - MathF.Exp(-1f / MathF.Max(1f, tauMs * 0.001f * sampleRate));
    }

    public void ProcessStereo(Span<float> buffer, int frames)
    {
        if (_mode == AutotuneMode.Off || _amount <= 0.001f)
            return;

        for (var i = 0; i < frames; i++)
        {
            var sample = 0.5f * (buffer[i * 2] + buffer[i * 2 + 1]);
            if (_fill < _ana.Length)
                _ana[_fill++] = sample;
        }

        if (_fill >= 1024)
        {
            var window = _ana.AsSpan(0, Math.Min(_fill, 2048));
            var freq = MixNative.Yin(window, _sampleRate);
            if (freq <= 0f)
                freq = DetectPitch(window, _sampleRate);
            if (freq > 70f && freq < 900f)
            {
                var snapped = MixNative.SnapHz(freq, _settings.Key, (int)_settings.Scale);
                if (snapped <= 0f)
                    snapped = Snap(freq, _settings.Key, _settings.Scale);
                var desired = Math.Clamp(snapped / freq, 0.5f, 2f);
                _targetRatio = desired;
                _hold = (int)(_sampleRate * 0.04f);
            }
            else
            {
                _hold -= frames;
                if (_hold <= 0)
                    _targetRatio += 0.08f * (1f - _targetRatio);
            }
            var keep = Math.Min(768, _fill);
            Array.Copy(_ana, _fill - keep, _ana, 0, keep);
            _fill = keep;
        }

        _ratio += (1f - MathF.Pow(1f - _slew, Math.Max(1, frames))) * (_targetRatio - _ratio);
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
        if (best > 0.35f)
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
