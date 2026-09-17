using Mixline.Audio.Sources;
using Mixline.Core;

namespace Mixline.Audio.Mixer;

public struct MixerSnapshot
{
    public float MicVolume;
    public float MicGain;
    public float MicPan;
    public float MicMonitor;
    public float MicVirtual;
    public bool MicMute;
    public bool MicSolo;

    public float MusicVolume;
    public float MusicGain;
    public float MusicPan;
    public float MusicMonitor;
    public float MusicVirtual;
    public bool MusicMute;
    public bool MusicSolo;

    public float SoundVolume;
    public float SoundGain;
    public float SoundPan;
    public float SoundMonitor;
    public float SoundVirtual;
    public bool SoundMute;
    public bool SoundSolo;

    public float MasterVolume;
    public float MasterMonitor;
    public float MasterVirtual;
    public bool MasterMute;

    public float MonitorVolume;
    public bool MicMonitorEnabled;
    public bool MusicMonitorEnabled;
    public bool SoundMonitorEnabled;
    public bool MasterMonitorEnabled;
    public bool BypassProcessing;
    public bool TestTone;

    public static MixerSnapshot From(MixerSettings mixer, bool bypass, bool testTone)
    {
        return new MixerSnapshot
        {
            MicVolume = mixer.Mic.Volume,
            MicGain = mixer.Mic.Gain,
            MicPan = mixer.Mic.Pan,
            MicMonitor = Send(mixer.MicMonitor, mixer.Mic.MonitorSend),
            MicVirtual = mixer.Mic.VirtualSend,
            MicMute = mixer.Mic.Mute,
            MicSolo = mixer.Mic.Solo,
            MusicVolume = mixer.Music.Volume,
            MusicGain = mixer.Music.Gain,
            MusicPan = mixer.Music.Pan,
            MusicMonitor = Send(mixer.MusicMonitor, mixer.Music.MonitorSend),
            MusicVirtual = mixer.Music.VirtualSend,
            MusicMute = mixer.Music.Mute,
            MusicSolo = mixer.Music.Solo,
            SoundVolume = mixer.Soundboard.Volume,
            SoundGain = mixer.Soundboard.Gain,
            SoundPan = mixer.Soundboard.Pan,
            SoundMonitor = Send(mixer.SoundboardMonitor, mixer.Soundboard.MonitorSend),
            SoundVirtual = mixer.Soundboard.VirtualSend,
            SoundMute = mixer.Soundboard.Mute,
            SoundSolo = mixer.Soundboard.Solo,
            MasterVolume = mixer.Master.Volume,
            MasterMonitor = Send(mixer.MasterMonitor, mixer.Master.MonitorSend),
            MasterVirtual = mixer.Master.VirtualSend,
            MasterMute = mixer.Master.Mute,
            MonitorVolume = mixer.MonitorVolume,
            MicMonitorEnabled = mixer.MicMonitor,
            MusicMonitorEnabled = mixer.MusicMonitor,
            SoundMonitorEnabled = mixer.SoundboardMonitor,
            MasterMonitorEnabled = mixer.MasterMonitor,
            BypassProcessing = bypass,
            TestTone = testTone
        };
    }

    private static float Send(bool on, float send)
        => on ? (send > 0.0001f ? send : 1f) : 0f;
}

public sealed class VoicePlayback
{
    public required float[] Samples;
    public int Position;
    public float Volume = 1f;
    public float Pitch = 1f;
    public float Speed = 1f;
    public bool Loop;
    public float FadeIn;
    public float FadeOut;
    public double Cursor;
    public bool StopRequested;
    public Guid Id;
}

public sealed class MeterState
{
    public float Mic;
    public float MicHold;
    public float Music;
    public float MusicHold;
    public float Soundboard;
    public float SoundboardHold;
    public float Master;
    public float MasterHold;
    public float Virtual;
    public float VirtualHold;
    public float GainReductionDb;
    public float TuneHz;
    public bool GateOpen;
    public bool MicActive;
}
