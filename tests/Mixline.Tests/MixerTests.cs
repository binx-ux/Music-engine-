using Mixline.Audio.DSP;
using Mixline.Audio.Mixer;
using Mixline.Audio.Sources;
using Mixline.Core;
using Mixline.IPC;
using Mixline.Soundboard;
using Xunit;

namespace Mixline.Tests;

public class MixerTests
{
    [Fact]
    public void GainFromDb_IsSymmetric()
    {
        var lin = Gain.FromDb(-6f);
        Assert.InRange(lin, 0.49f, 0.51f);
        Assert.InRange(Gain.ToDb(lin), -6.2f, -5.8f);
    }

    [Fact]
    public void ChannelConvert_MonoToStereo()
    {
        float[] mono = [0.5f, -0.25f];
        var stereo = new float[4];
        ChannelConvert.ToStereo(mono, 1, stereo, 2);
        Assert.Equal(0.5f, stereo[0]);
        Assert.Equal(0.5f, stereo[1]);
        Assert.Equal(-0.25f, stereo[2]);
        Assert.Equal(-0.25f, stereo[3]);
    }

    [Fact]
    public void Limiter_StopsClipping()
    {
        var limiter = new Limiter(0.95f, 20f, 48000);
        var buf = new float[256];
        for (var i = 0; i < buf.Length; i++)
            buf[i] = i % 2 == 0 ? 4f : -4f;
        limiter.ProcessStereo(buf, 128);
        Assert.All(buf, s => Assert.InRange(s, -1f, 1f));
    }

    [Fact]
    public void Snapshot_CopiesVolumes()
    {
        var mixer = new MixerSettings { Mic = { Volume = 0.4f } };
        var snap = MixerSnapshot.From(mixer, false, false);
        Assert.Equal(0.4f, snap.MicVolume);
    }

    [Fact]
    public void RingBuffer_WriteRead()
    {
        var ring = new FloatRingBuffer(8);
        float[] src = [1, 2, 3];
        Assert.Equal(3, ring.Write(src));
        var dest = new float[3];
        Assert.Equal(3, ring.Read(dest));
        Assert.Equal(src, dest);
    }
}

public class DspTests
{
    [Fact]
    public void EqPreset_VoiceChangesHighMid()
    {
        var flat = EqPresets.Create("Flat");
        var voice = EqPresets.Create("Voice");
        Assert.Equal(0, flat.HighMid.GainDb);
        Assert.True(voice.HighMid.GainDb > 0);
    }

    [Fact]
    public void Gate_ClosesOnSilence()
    {
        var gate = new NoiseGate();
        gate.Configure(48000, new GateSettings { ThresholdDb = -20, AttackMs = 1, HoldMs = 1, ReleaseMs = 1 });
        var buf = new float[480];
        gate.ProcessStereo(buf, 240);
        Assert.False(gate.IsOpen);
    }
}

public class HotkeyTests
{
    [Fact]
    public void ParsesCtrlAlt1()
    {
        var r = HotkeyParser.Parse("pad", "Ctrl+Alt+1");
        Assert.True(r.Success);
        Assert.Equal(HotkeyParser.ModControl | HotkeyParser.ModAlt, r.Value!.Modifiers);
        Assert.Equal(0x31, r.Value.Key);
    }

    [Fact]
    public void RejectsBareKey()
    {
        var r = HotkeyParser.Parse("pad", "A");
        Assert.False(r.Success);
    }
}

public class ConfigTests
{
    [Fact]
    public void DefaultConfig_HasVersion()
    {
        var cfg = new AppConfig();
        Assert.Equal(AppInfo.ConfigVersion, cfg.Version);
        Assert.Equal(BufferPreset.Balanced, cfg.Audio.BufferPreset);
    }
}

public class UrlTests
{
    [Fact]
    public async Task RejectsYoutube()
    {
        var v = new UrlAudioValidator();
        var r = await v.ValidateAsync("https://www.youtube.com/watch?v=dQw4w9wgGcQ", CancellationToken.None);
        Assert.False(r.Ok);
    }

    [Fact]
    public async Task RejectsSpotifyOpen()
    {
        var v = new UrlAudioValidator();
        var r = await v.ValidateAsync("https://open.spotify.com/track/abc", CancellationToken.None);
        Assert.False(r.Ok);
    }
}

public class SoundboardTests
{
    [Fact]
    public void Pad_HasIdentity()
    {
        var pad = new SoundPad { Name = "Airhorn" };
        Assert.NotEqual(Guid.Empty, pad.Id);
        Assert.Equal("Airhorn", pad.Name);
    }
}
