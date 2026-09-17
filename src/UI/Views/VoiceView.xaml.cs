using System.Windows;
using System.Windows.Controls;
using Mixline.Audio.DSP;
using Mixline.Audio.Mixer;
using Mixline.Core;

namespace Mixline.App.Views;

public partial class VoiceView : UserControl
{
    private static readonly string[] Notes = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    private AppSession? _session;
    private bool _suppress;
    private bool _bound;

    public VoiceView() => InitializeComponent();

    public void Bind(AppSession session)
    {
        _session = session;
        if (!_bound)
        {
            _bound = true;
            foreach (var n in EqPresets.Names)
                EqPreset.Items.Add(n);
            foreach (var k in Notes)
                TuneKey.Items.Add(k);
            TuneScale.Items.Add("Chromatic");
            TuneScale.Items.Add("Major");
            TuneScale.Items.Add("Minor");
            session.Changed += () => Dispatcher.BeginInvoke(Load);
        }
        Load();
    }

    public void UpdateLive(MeterState meters)
    {
        if (_session is null) return;
        Live.Text = _session.Engine.MicrophoneActive
            ? (meters.GateOpen ? "Mic live  ·  gate open" : "Mic live  ·  gate closed")
            : "Mic idle";
        Gr.Text = $"Gain reduction  {meters.GainReductionDb:0.0} dB";

        var on = _session.Config.Voice.Autotune != AutotuneMode.Off;
        if (!on)
        {
            TuneNote.Text = "Off";
            TuneHz.Text = "Turn on a style to pitch-correct";
            TuneLive.Text = "";
            return;
        }

        if (meters.TuneHz >= 70f)
        {
            TuneNote.Text = NoteName(meters.TuneHz);
            TuneHz.Text = $"{meters.TuneHz:0} Hz";
            TuneLive.Text = "Locked";
        }
        else
        {
            TuneNote.Text = "—";
            TuneHz.Text = _session.Engine.MicrophoneActive ? "Listening" : "Waiting for voice";
            TuneLive.Text = "";
        }
    }

    private void Load()
    {
        if (_session is null) return;
        _suppress = true;
        var v = _session.Config.Voice;
        Enhance.IsChecked = v.VoiceEnhance;
        Hpf.IsChecked = v.HighPass;
        HpfHz.Value = v.HighPassHz;
        Gate.IsChecked = v.Gate;
        GateTh.Value = v.GateSettings.ThresholdDb;
        Nr.IsChecked = v.NoiseReduction;
        NrAmt.Value = v.NoiseReductionAmount;
        EqOn.IsChecked = v.Eq;
        EqPreset.SelectedItem = v.EqPreset;
        Comp.IsChecked = v.Compressor;
        CompTh.Value = v.CompressorSettings.ThresholdDb;
        CompRatio.Value = v.CompressorSettings.Ratio;
        Tune.SelectedIndex = (int)v.Autotune;
        TuneKey.SelectedIndex = Math.Clamp(v.AutotuneSettings.Key, 0, 11);
        TuneScale.SelectedIndex = Math.Clamp((int)v.AutotuneSettings.Scale, 0, 2);
        TuneAmt.Value = v.AutotuneSettings.Amount;
        TuneSpeed.Value = v.AutotuneSettings.RetuneSpeed;
        Formant.IsChecked = v.AutotuneSettings.FormantPreservation;
        DeEss.IsChecked = v.DeEsser;
        Sat.IsChecked = v.Saturation;
        Lim.IsChecked = v.Limiter;
        Labels();
        _suppress = false;
    }

    private void Labels()
    {
        if (TuneAmtLabel is null || TuneSpeedLabel is null)
            return;
        TuneAmtLabel.Text = $"{TuneAmt.Value * 100:0}%";
        TuneSpeedLabel.Text = TuneSpeed.Value < 0.34 ? "Slow" : TuneSpeed.Value < 0.7 ? "Medium" : "Fast";
    }

    private void PresetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || _session is null) return;
        var name = EqPreset.SelectedItem as string ?? "Flat";
        _session.Config.Voice.EqPreset = name;
        if (name != "Custom")
            _session.Config.Voice.EqSettings = EqPresets.Create(name);
        _session.VoiceChanged();
    }

    private void SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TuneAmtLabel is not null)
            Labels();
        Changed(sender, e);
    }

    private void ComboChanged(object sender, SelectionChangedEventArgs e) => Changed(sender, e);

    private void Changed(object sender, RoutedEventArgs e)
    {
        if (_suppress || _session is null) return;
        var v = _session.Config.Voice;
        v.VoiceEnhance = Enhance.IsChecked == true;
        v.HighPass = Hpf.IsChecked == true;
        v.HighPassHz = (float)HpfHz.Value;
        v.Gate = Gate.IsChecked == true;
        v.GateSettings.ThresholdDb = (float)GateTh.Value;
        v.NoiseReduction = Nr.IsChecked == true;
        v.NoiseReductionAmount = (float)NrAmt.Value;
        v.Eq = EqOn.IsChecked == true;
        v.Compressor = Comp.IsChecked == true;
        v.CompressorSettings.ThresholdDb = (float)CompTh.Value;
        v.CompressorSettings.Ratio = (float)CompRatio.Value;
        v.Autotune = (AutotuneMode)Math.Max(0, Tune.SelectedIndex);
        v.AutotuneSettings.Key = Math.Max(0, TuneKey.SelectedIndex);
        v.AutotuneSettings.Scale = (MusicalScale)Math.Max(0, TuneScale.SelectedIndex);
        v.AutotuneSettings.Amount = (float)TuneAmt.Value;
        v.AutotuneSettings.RetuneSpeed = (float)TuneSpeed.Value;
        v.AutotuneSettings.FormantPreservation = Formant.IsChecked == true;
        v.DeEsser = DeEss.IsChecked == true;
        v.Saturation = Sat.IsChecked == true;
        v.Limiter = Lim.IsChecked == true;
        _session.VoiceChanged();
    }

    private static string NoteName(float hz)
    {
        var midi = 69 + 12 * MathF.Log2(hz / 440f);
        var n = (int)MathF.Round(midi);
        var pc = ((n % 12) + 12) % 12;
        var oct = n / 12 - 1;
        return Notes[pc] + oct;
    }
}
