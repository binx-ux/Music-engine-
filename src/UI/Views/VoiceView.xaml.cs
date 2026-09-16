using System.Windows;
using System.Windows.Controls;
using Mixline.Audio.DSP;
using Mixline.Audio.Mixer;
using Mixline.Core;

namespace Mixline.App.Views;

public partial class VoiceView : UserControl
{
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
            foreach (var k in new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" })
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
        _suppress = false;
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

    private void SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => Changed(sender, e);
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
}
