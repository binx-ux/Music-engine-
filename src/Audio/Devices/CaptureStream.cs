using NAudio.CoreAudioApi;
using NAudio.Wave;
using Mixline.Audio.Mixer;
using Mixline.Core;
using Mixline.Logging;

namespace Mixline.Audio.Devices;

public sealed class CaptureStream : IDisposable
{
    private readonly AppLog _log;
    private readonly FloatRingBuffer _ring;
    private readonly int _engineRate;
    private WasapiCapture? _capture;
    private CubicResampler? _resampler;
    private float[] _convert = [];
    private float[] _stereo = [];
    private float[] _resampled = [];
    private WaveFormat? _format;
    private bool _running;

    public bool IsRunning => _running;
    public string? DeviceName { get; private set; }
    public int DeviceSampleRate { get; private set; }
    public int DeviceChannels { get; private set; }
    public long Underruns;

    public CaptureStream(AppLog log, FloatRingBuffer ring, int engineRate)
    {
        _log = log;
        _ring = ring;
        _engineRate = engineRate;
    }

    public Result Start(MMDevice device, int bufferMs, AudioClientShareMode shareMode)
    {
        Stop();
        try
        {
            DeviceName = device.FriendlyName;
            _capture = new WasapiCapture(device, true, bufferMs)
            {
                ShareMode = shareMode
            };
            _format = _capture.WaveFormat;
            DeviceSampleRate = _format.SampleRate;
            DeviceChannels = _format.Channels;
            _resampler = new CubicResampler(2);
            _convert = new float[Math.Max(4096, _engineRate)];
            _stereo = new float[Math.Max(4096, _engineRate * 2)];
            _resampled = new float[Math.Max(8192, _engineRate * 2)];
            _capture.DataAvailable += OnData;
            _capture.RecordingStopped += OnStopped;
            _capture.StartRecording();
            _running = true;
            _log.Info("audio", $"Microphone started: {DeviceName} {_format.SampleRate} Hz {_format.Channels} ch");
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _log.Error("audio", "Could not start the microphone.", ex);
            Stop();
            return Result.Fail("Microphone could not be started.", ex.Message);
        }
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        if (_format is null || e.BytesRecorded <= 0)
            return;

        try
        {
            var frames = e.BytesRecorded / _format.BlockAlign;
            var needed = frames * Math.Max(2, _format.Channels);
            if (_convert.Length < needed)
                return;
            var floats = Decode(e.Buffer, e.BytesRecorded, _format, _convert);
            if (_stereo.Length < frames * 2)
                return;
            ChannelConvert.ToStereo(floats, _format.Channels, _stereo, frames);
            var ratio = (double)_format.SampleRate / _engineRate;
            var outFrames = Math.Max(1, (int)Math.Round(frames / ratio));
            if (_resampled.Length < outFrames * 2)
                return;
            var produced = _resampler!.Process(_stereo, frames, _resampled, outFrames, ratio);
            _ring.Write(_resampled.AsSpan(0, produced * 2));
        }
        catch
        {
        }
    }

    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        _running = false;
        if (e.Exception is not null)
            _log.Warning("audio", "Microphone stopped unexpectedly.", e.Exception.Message);
    }

    public static Span<float> Decode(byte[] data, int bytes, WaveFormat format, float[] dest)
    {
        var frames = bytes / format.BlockAlign;
        var samples = frames * format.Channels;
        if (dest.Length < samples)
            samples = dest.Length / Math.Max(1, format.Channels) * format.Channels;

        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            Buffer.BlockCopy(data, 0, dest, 0, Math.Min(bytes, samples * 4));
            return dest.AsSpan(0, samples);
        }

        if (format.BitsPerSample == 16)
        {
            for (var i = 0; i < samples; i++)
                dest[i] = BitConverter.ToInt16(data, i * 2) / 32768f;
            return dest.AsSpan(0, samples);
        }

        if (format.BitsPerSample == 24)
        {
            for (var i = 0; i < samples; i++)
            {
                var o = i * 3;
                var v = data[o] | (data[o + 1] << 8) | (data[o + 2] << 16);
                if ((v & 0x800000) != 0)
                    v |= unchecked((int)0xFF000000);
                dest[i] = v / 8388608f;
            }
            return dest.AsSpan(0, samples);
        }

        if (format.BitsPerSample == 32)
        {
            for (var i = 0; i < samples; i++)
                dest[i] = BitConverter.ToInt32(data, i * 4) / 2147483648f;
            return dest.AsSpan(0, samples);
        }

        dest.AsSpan(0, samples).Clear();
        return dest.AsSpan(0, samples);
    }

    public void Stop()
    {
        _running = false;
        if (_capture is null)
            return;
        try
        {
            _capture.DataAvailable -= OnData;
            _capture.RecordingStopped -= OnStopped;
            _capture.StopRecording();
        }
        catch
        {
        }
        _capture.Dispose();
        _capture = null;
        _ring.Clear();
    }

    public void Dispose() => Stop();
}
