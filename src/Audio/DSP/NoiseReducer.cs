namespace Mixline.Audio.DSP;

public static class Fft
{
    public static void Forward(float[] real, float[] imag, int n)
    {
        BitReverse(real, imag, n);
        for (var size = 2; size <= n; size <<= 1)
        {
            var half = size >> 1;
            var step = MathF.PI * 2f / size;
            for (var i = 0; i < n; i += size)
            {
                for (var j = 0; j < half; j++)
                {
                    var wr = MathF.Cos(-step * j);
                    var wi = MathF.Sin(-step * j);
                    var evenR = real[i + j];
                    var evenI = imag[i + j];
                    var oddR = real[i + j + half];
                    var oddI = imag[i + j + half];
                    var tr = wr * oddR - wi * oddI;
                    var ti = wr * oddI + wi * oddR;
                    real[i + j] = evenR + tr;
                    imag[i + j] = evenI + ti;
                    real[i + j + half] = evenR - tr;
                    imag[i + j + half] = evenI - ti;
                }
            }
        }
    }

    public static void Inverse(float[] real, float[] imag, int n)
    {
        for (var i = 0; i < n; i++)
            imag[i] = -imag[i];
        Forward(real, imag, n);
        var inv = 1f / n;
        for (var i = 0; i < n; i++)
        {
            real[i] *= inv;
            imag[i] = -imag[i] * inv;
        }
    }

    private static void BitReverse(float[] real, float[] imag, int n)
    {
        var j = 0;
        for (var i = 1; i < n; i++)
        {
            var bit = n >> 1;
            for (; j >= bit; bit >>= 1)
                j -= bit;
            j += bit;
            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }
        }
    }
}

public sealed class NoiseReducer
{
    private const int N = 512;
    private const int Hop = 256;
    private readonly float[] _window = new float[N];
    private readonly float[] _olapL = new float[N];
    private readonly float[] _olapR = new float[N];
    private readonly float[] _fifoL = new float[N];
    private readonly float[] _fifoR = new float[N];
    private readonly float[] _re = new float[N];
    private readonly float[] _im = new float[N];
    private readonly float[] _noise = new float[N / 2];
    private readonly float[] _mag = new float[N / 2];
    private int _fifoFill;
    private int _learn;
    private float _amount;

    public NoiseReducer()
    {
        for (var i = 0; i < N; i++)
            _window[i] = 0.5f - 0.5f * MathF.Cos(2f * MathF.PI * i / (N - 1));
        _learn = 24;
    }

    public void Configure(float amount)
    {
        _amount = Math.Clamp(amount, 0f, 1f);
    }

    public void ProcessStereo(Span<float> buffer, int frames)
    {
        var i = 0;
        while (i < frames)
        {
            var room = N - _fifoFill;
            var take = Math.Min(room, frames - i);
            for (var k = 0; k < take; k++)
            {
                _fifoL[_fifoFill] = buffer[(i + k) * 2];
                _fifoR[_fifoFill] = buffer[(i + k) * 2 + 1];
                _fifoFill++;
            }
            i += take;
            if (_fifoFill < N)
                break;

            ProcessFrame(_fifoL, _olapL);
            ProcessFrame(_fifoR, _olapR);
            Array.Copy(_fifoL, Hop, _fifoL, 0, Hop);
            Array.Copy(_fifoR, Hop, _fifoR, 0, Hop);
            _fifoFill = Hop;

            var start = i - Hop;
            for (var k = 0; k < Hop && start + k < frames; k++)
            {
                buffer[(start + k) * 2] = _olapL[k];
                buffer[(start + k) * 2 + 1] = _olapR[k];
            }
            Array.Copy(_olapL, Hop, _olapL, 0, Hop);
            Array.Copy(_olapR, Hop, _olapR, 0, Hop);
            Array.Clear(_olapL, Hop, Hop);
            Array.Clear(_olapR, Hop, Hop);
        }
    }

    private void ProcessFrame(float[] input, float[] overlap)
    {
        Array.Clear(_im);
        for (var i = 0; i < N; i++)
            _re[i] = input[i] * _window[i];
        Fft.Forward(_re, _im, N);

        for (var i = 0; i < _mag.Length; i++)
            _mag[i] = MathF.Sqrt(_re[i] * _re[i] + _im[i] * _im[i]);

        if (_learn > 0)
        {
            for (var i = 0; i < _mag.Length; i++)
                _noise[i] += _mag[i];
            _learn--;
            if (_learn == 0)
            {
                for (var i = 0; i < _mag.Length; i++)
                    _noise[i] /= 24f;
            }
        }
        else
        {
            for (var i = 0; i < _mag.Length; i++)
                _noise[i] = _noise[i] * 0.9995f + _mag[i] * 0.0005f * (_mag[i] < _noise[i] * 1.2f ? 1f : 0.05f);
        }

        for (var i = 0; i < _mag.Length; i++)
        {
            var n = _noise[i] + 1e-8f;
            var gain = 1f - _amount * Math.Clamp(n / (_mag[i] + 1e-8f), 0f, 1f);
            gain = MathF.Max(gain, 1f - _amount * 0.85f);
            _re[i] *= gain;
            _im[i] *= gain;
            if (i > 0)
            {
                _re[N - i] *= gain;
                _im[N - i] *= gain;
            }
        }

        Fft.Inverse(_re, _im, N);
        for (var i = 0; i < N; i++)
            overlap[i] += _re[i] * _window[i];
    }
}
