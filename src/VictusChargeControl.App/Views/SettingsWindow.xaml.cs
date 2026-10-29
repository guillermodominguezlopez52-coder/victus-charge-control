using System.Windows;
using System.Windows.Controls;
using VictusChargeControl.App.Infrastructure;
using VictusChargeControl.Core.Settings;

namespace VictusChargeControl.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        LoadSettings(settings.Normalized());
    }

    public AppSettings? Result { get; private set; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(this);
    }

    private void LoadSettings(AppSettings settings)
    {
        UpperSlider.Value = settings.UpperThreshold;
        LowerSlider.Value = settings.LowerThreshold;
        UpperAlertCheck.IsChecked = settings.UpperAlertEnabled;
        LowerAlertCheck.IsChecked = settings.LowerAlertEnabled;
        NotificationsCheck.IsChecked = settings.NotificationsEnabled;
        AutoStartCheck.IsChecked = settings.StartWithWindows;

        foreach (var option in IntervalPanel.Children.OfType<RadioButton>())
        {
            option.IsChecked = option.Tag is string tag &&
                               int.TryParse(tag, out var seconds) &&
                               seconds == settings.PollIntervalSeconds;
        }

        UpperValue.Text = $"{(int)settings.UpperThreshold} %";
        LowerInline.Text = $"{(int)settings.LowerThreshold} %";
    }

    private void UpperSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (UpperValue is not null)
        {
            UpperValue.Text = $"{(int)e.NewValue} %";
        }

        if (ValidationText is not null)
        {
            ValidationText.Visibility = Visibility.Collapsed;
        }
    }

    private void LowerSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LowerInline is not null)
        {
            LowerInline.Text = $"{(int)e.NewValue} %";
        }

        if (ValidationText is not null)
        {
            ValidationText.Visibility = Visibility.Collapsed;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var upper = (int)UpperSlider.Value;
        var lower = (int)LowerSlider.Value;

        if (upper - lower < AppSettings.MinGap)
        {
            ValidationText.Visibility = Visibility.Visible;
            return;
        }

        var interval = IntervalPanel.Children
            .OfType<RadioButton>()
            .Where(option => option.IsChecked == true)
            .Select(option => int.TryParse(option.Tag as string, out var seconds) ? seconds : 30)
            .FirstOrDefault(30);

        Result = new AppSettings
        {
            UpperThreshold = upper,
            LowerThreshold = lower,
            UpperAlertEnabled = UpperAlertCheck.IsChecked == true,
            LowerAlertEnabled = LowerAlertCheck.IsChecked == true,
            NotificationsEnabled = NotificationsCheck.IsChecked == true,
            StartWithWindows = AutoStartCheck.IsChecked == true,
            PollIntervalSeconds = interval
        }.Normalized();

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
