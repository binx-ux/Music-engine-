using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Mixline.Audio.Mixer;
using Mixline.Core;
using Mixline.Logging;

namespace Mixline.Audio.Sources;

public sealed class StreamedSource : IAudioSource
{
    private readonly AppLog _log;
    private readonly int _engineRate;
    private readonly FloatRingBuffer _ring = new(1 << 16);
    private readonly object _gate = new();
    private readonly float[] _pumpBuf = new float[2048];
    private WaveStream? _stream;
    private ISampleProvider? _provider;
    private Thread? _thread;
    private volatile bool _run;
    private volatile bool _playing;
    private volatile bool _loop;
    private volatile bool _ended;
    private long _seekTicks = -1;

    public bool IsPlaying => _playing && !_ended;
    public bool Ended => _ended;
    public TimeSpan Position => _stream?.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan Duration => _stream?.TotalTime ?? TimeSpan.Zero;
    public TrackInfo? Track { get; private set; }

    public StreamedSource(AppLog log, int engineRate)
    {
        _log = log;
        _engineRate = engineRate;
        _run = true;
        _thread = new Thread(Pump)
        {
            IsBackground = true,
            Name = "Cuebox.MusicDecode",
            Priority = ThreadPriority.AboveNormal
        };
        _thread.Start();
    }

    public Result Open(string pathOrUrl, bool isUrl, bool loop)
    {
        lock (_gate)
        {
            InnerClose();
            try
            {
                _loop = loop;
                _ended = false;
                _stream = OpenStream(pathOrUrl, isUrl);
                ISampleProvider sample = _stream.ToSampleProvider();
                if (sample.WaveFormat.Channels == 1)
                    sample = new MonoToStereoSampleProvider(sample);
                if (sample.WaveFormat.SampleRate != _engineRate)
                    sample = new WdlResamplingSampleProvider(sample, _engineRate);
                _provider = sample;
                Track = isUrl
                    ? new TrackInfo
                    {
                        Path = pathOrUrl,
                        FileName = pathOrUrl,
                        Title = pathOrUrl,
                        IsUrl = true,
                        Duration = _stream.TotalTime
                    }
                    : AudioFileSupport.ReadMetadata(pathOrUrl) with { Duration = _stream.TotalTime };
                _ring.Clear();
                _playing = true;
                return Result.Ok();
            }
            catch (Exception ex)
            {
                InnerClose();
                var msg = isUrl ? "This URL could not be played." : "This audio file could not be opened.";
                _log.Warning("music", msg, ex.Message);
                return Result.Fail(msg, ex.Message);
            }
        }
    }

    public int Read(Span<float> stereo, int frames)
    {
        if (!_playing)
        {
            stereo[..(frames * 2)].Clear();
            return 0;
        }

        var n = _ring.ReadOrZero(stereo[..(frames * 2)]);
        return n / 2;
    }

    public void Seek(TimeSpan position)
    {
        Interlocked.Exchange(ref _seekTicks, position.Ticks);
    }

    public void Pause() => _playing = false;
    public void Resume()
    {
        if (_stream is not null)
        {
            _ended = false;
            _playing = true;
        }
    }

    public void AcknowledgeEnd() => _ended = false;

    public void Close()
    {
        lock (_gate)
            InnerClose();
    }

    public void Dispose()
    {
        _run = false;
        Close();
        _thread?.Join(200);
        _thread = null;
    }

    private void InnerClose()
    {
        _playing = false;
        _ended = false;
        _provider = null;
        _stream?.Dispose();
        _stream = null;
        Track = null;
        _ring.Clear();
    }

    private void Pump()
    {
        while (_run)
        {
            try
            {
                var seek = Interlocked.Exchange(ref _seekTicks, -1);
                if (seek >= 0)
                {
                    lock (_gate)
                    {
                        if (_stream is not null)
                        {
                            _stream.CurrentTime = TimeSpan.FromTicks(seek);
                            _ring.Clear();
                        }
                    }
                }

                if (!_playing || _provider is null)
                {
                    Thread.Sleep(8);
                    continue;
                }

                if (_ring.AvailableWrite < _pumpBuf.Length + 8)
                {
                    Thread.Sleep(2);
                    continue;
                }

                int read;
                lock (_gate)
                {
                    if (_provider is null)
                        continue;
                    read = _provider.Read(_pumpBuf, 0, _pumpBuf.Length);
                    if (read <= 0)
                    {
                        if (_loop && _stream is not null)
                        {
                            _stream.Position = 0;
                            read = _provider.Read(_pumpBuf, 0, _pumpBuf.Length);
                        }
                    }
                }

                if (read <= 0)
                {
                    _playing = false;
                    _ended = true;
                    Thread.Sleep(8);
                    continue;
                }

                _ring.Write(_pumpBuf.AsSpan(0, read));
            }
            catch
            {
                Thread.Sleep(8);
            }
        }
    }

    private static WaveStream OpenStream(string pathOrUrl, bool isUrl)
    {
        if (!isUrl && Path.GetExtension(pathOrUrl).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
            return new VorbisWaveReader(pathOrUrl);

        if (isUrl)
            return new MediaFoundationReader(pathOrUrl);

        return new AudioFileReader(pathOrUrl);
    }
}
