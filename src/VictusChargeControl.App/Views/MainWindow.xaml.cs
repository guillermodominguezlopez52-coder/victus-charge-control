using System.ComponentModel;
using System.Windows;
using VictusChargeControl.App.Infrastructure;

namespace VictusChargeControl.App.Views;

public partial class MainWindow : Window
{
    private bool _hiddenNoticeShown;

    public MainWindow()
    {
        InitializeComponent();
        StateChanged += OnStateChanged;
    }

    public bool AllowClose { get; set; }

    public event EventHandler? SettingsRequested;

    public event EventHandler? HistoryRequested;

    public event EventHandler? RefreshRequested;

    public event EventHandler? HiddenToTray;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(this);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            HideToTray();
        }

        base.OnClosing(e);
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            HideToTray();
        }
    }

    private void HideToTray()
    {
        Hide();

        if (!_hiddenNoticeShown)
        {
            _hiddenNoticeShown = true;
            HiddenToTray?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void History_Click(object sender, RoutedEventArgs e) => HistoryRequested?.Invoke(this, EventArgs.Empty);

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshRequested?.Invoke(this, EventArgs.Empty);
}
