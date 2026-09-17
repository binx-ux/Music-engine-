using Mixline.Core;

namespace Mixline.Audio.Sources;

public sealed class MusicPlayer
{
    private readonly StreamedSource _source;
    private readonly List<TrackInfo> _queue = [];
    private int _index = -1;
    private bool _shuffle;
    private LoopMode _loop = LoopMode.Off;
    private readonly Random _rng = new();
    private readonly List<int> _shuffleBag = [];
    private readonly List<int> _history = [];

    public event Action? Finished;
    public event Action<TrackInfo?>? TrackChanged;

    public MusicPlayer(StreamedSource source)
    {
        _source = source;
    }

    public IReadOnlyList<TrackInfo> Queue => _queue;
    public int Index => _index;
    public TrackInfo? Current => _source.Track;
    public bool IsPlaying => _source.IsPlaying;
    public TimeSpan Position => _source.Position;
    public TimeSpan Duration => _source.Duration;
    public bool Shuffle => _shuffle;
    public LoopMode Loop => _loop;

    public void SetShuffle(bool shuffle) => _shuffle = shuffle;
    public void SetLoop(LoopMode loop) => _loop = loop;

    public void ReplaceQueue(IEnumerable<TrackInfo> tracks, int startIndex = 0)
    {
        _queue.Clear();
        _history.Clear();
        _queue.AddRange(tracks);
        _index = Math.Clamp(startIndex, 0, Math.Max(0, _queue.Count - 1));
        if (_queue.Count == 0)
            _index = -1;
        RebuildShuffle();
    }

    public void Add(TrackInfo track) => _queue.Add(track);

    public void Clear()
    {
        _queue.Clear();
        _history.Clear();
        _index = -1;
        _source.Close();
        TrackChanged?.Invoke(null);
    }

    public Result PlayIndex(int index) => PlayAt(index, true);

    private Result PlayAt(int index, bool recordHistory)
    {
        if (_queue.Count == 0)
            return Result.Fail("Nothing is queued.");

        var from = _index;
        var start = Math.Clamp(index, 0, _queue.Count - 1);
        var i = start;
        for (var n = 0; n < _queue.Count; n++)
        {
            _index = i;
            var track = _queue[i];
            var opened = _source.Open(track.Path, track.IsUrl, _loop == LoopMode.One);
            if (opened.Success)
            {
                if (recordHistory && from >= 0 && from != i)
                {
                    _history.Add(from);
                    if (_history.Count > 64)
                        _history.RemoveAt(0);
                }
                TrackChanged?.Invoke(_source.Track);
                return opened;
            }
            i++;
            if (i >= _queue.Count)
                i = 0;
            if (i == start)
                break;
        }

        _source.Close();
        TrackChanged?.Invoke(null);
        return Result.Fail("Could not play that track.");
    }

    public Result Play()
    {
        if (_source.Track is not null)
        {
            _source.Resume();
            return Result.Ok();
        }
        if (_queue.Count == 0)
            return Result.Fail("Add a song first.");
        return PlayIndex(Math.Max(0, _index));
    }

    public void Pause() => _source.Pause();

    public void Stop()
    {
        _source.Close();
        TrackChanged?.Invoke(null);
    }

    public Result Next()
    {
        if (_queue.Count == 0)
            return Result.Fail("The queue is empty.");
        if (_loop == LoopMode.One && _index >= 0)
            return PlayIndex(_index);

        var next = NextIndex();
        if (next < 0)
        {
            Stop();
            Finished?.Invoke();
            return Result.Ok();
        }
        return PlayIndex(next);
    }

    public Result Previous()
    {
        if (_queue.Count == 0)
            return Result.Fail("The queue is empty.");

        while (_history.Count > 0)
        {
            var prev = _history[^1];
            _history.RemoveAt(_history.Count - 1);
            if (prev >= 0 && prev < _queue.Count && prev != _index)
                return PlayAt(prev, false);
        }

        if (_queue.Count == 1)
            return PlayAt(0, false);

        var target = _index <= 0 ? _queue.Count - 1 : _index - 1;
        return PlayAt(target, false);
    }

    public void Seek(TimeSpan position) => _source.Seek(position);

    public int Read(Span<float> stereo, int frames)
    {
        return _source.Read(stereo, frames);
    }

    public bool ConsumeEnded()
    {
        if (!_source.Ended)
            return false;
        _source.AcknowledgeEnd();
        return true;
    }

    private int NextIndex()
    {
        if (_queue.Count == 0)
            return -1;
        if (_shuffle)
        {
            if (_shuffleBag.Count == 0)
            {
                if (_loop != LoopMode.All)
                    return -1;
                RebuildShuffle();
            }
            if (_shuffleBag.Count == 0)
                return -1;
            var idx = _shuffleBag[0];
            _shuffleBag.RemoveAt(0);
            return idx;
        }

        var next = _index + 1;
        if (next >= _queue.Count)
            return _loop == LoopMode.All ? 0 : -1;
        return next;
    }

    private void RebuildShuffle()
    {
        _shuffleBag.Clear();
        for (var i = 0; i < _queue.Count; i++)
        {
            if (i != _index)
                _shuffleBag.Add(i);
        }
        for (var i = _shuffleBag.Count - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (_shuffleBag[i], _shuffleBag[j]) = (_shuffleBag[j], _shuffleBag[i]);
        }
    }
}
