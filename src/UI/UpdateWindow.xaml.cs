using System.Windows;
using Mixline.Core;

namespace Mixline.App;

public partial class UpdateWindow : Window
{
    private readonly UpdateOffer _offer;
    private bool _busy;

    public bool Skipped { get; private set; }
    public bool Install { get; private set; }

    public UpdateWindow(UpdateOffer offer)
    {
        _offer = offer;
        InitializeComponent();
        TitleText.Text = "Cuebox " + offer.Version;
        KindText.Text = UpdateService.KindLabel(offer.Kind) + "  ·  you have " + AppInfo.Version;
        NotesText.Text = offer.Notes;
        SkipBtn.Visibility = offer.CanSkip ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void UpdateClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        SkipBtn.IsEnabled = false;
        LaterBtn.IsEnabled = false;
        GoBtn.IsEnabled = false;
        BarWell.Visibility = Visibility.Visible;
        Status.Text = "Downloading...";
        var progress = new Progress<double>(p =>
        {
            var w = BarWell.ActualWidth;
            BarFill.Width = w <= 0 ? 0 : w * Math.Clamp(p, 0, 1);
            Status.Text = p >= 1 ? "Starting setup..." : "Downloading... " + (int)(p * 100) + "%";
        });
        var r = await UpdateService.InstallAsync(_offer, progress, CancellationToken.None);
        if (!r.Success)
        {
            _busy = false;
            SkipBtn.IsEnabled = true;
            LaterBtn.IsEnabled = true;
            GoBtn.IsEnabled = true;
            Status.Text = r.Error ?? "Update failed.";
            return;
        }
        Install = true;
        DialogResult = true;
        Close();
        Application.Current.Shutdown();
    }

    private void SkipClick(object sender, RoutedEventArgs e)
    {
        if (_busy || !_offer.CanSkip) return;
        Skipped = true;
        DialogResult = true;
        Close();
    }

    private void LaterClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        DialogResult = false;
        Close();
    }
}
