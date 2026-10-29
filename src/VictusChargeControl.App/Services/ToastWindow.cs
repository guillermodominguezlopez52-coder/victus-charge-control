using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using VictusChargeControl.Core.Notifications;

namespace VictusChargeControl.App.Services;

/// <summary>
/// Alternativa a la notificación del shell: ventana flotante abajo a la derecha.
/// Se usa solo cuando el icono de bandeja no puede mostrar el globo.
/// </summary>
public static class ToastWindow
{
    public static void Show(NotificationRequest request)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        app.Dispatcher.BeginInvoke(() => ShowCore(request));
    }

    private static void ShowCore(NotificationRequest request)
    {
        var workArea = SystemParameters.WorkArea;

        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = request.Title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF5)),
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = request.Message,
            FontSize = 12,
            Margin = new Thickness(0, 6, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(0x9F, 0xAD, 0xBE)),
            TextWrapping = TextWrapping.Wrap
        });

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x14, 0x1B, 0x25)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Child = panel
        };

        const double width = 360;

        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            Topmost = true,
            Width = width,
            SizeToContent = SizeToContent.Height,
            Content = border,
            Left = Math.Max(0, workArea.Right - width - 16),
            Top = Math.Max(0, workArea.Bottom - 140)
        };

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(9) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            window.Close();
        };

        window.MouseLeftButtonDown += (_, _) => window.Close();
        window.Loaded += (_, _) => timer.Start();
        window.Closed += (_, _) => timer.Stop();
        window.Show();
    }
}
