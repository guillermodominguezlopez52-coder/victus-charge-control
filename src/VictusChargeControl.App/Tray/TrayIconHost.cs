using System.Drawing;
using System.Windows.Forms;
using VictusChargeControl.Core.Notifications;

namespace VictusChargeControl.App.Tray;

/// <summary>
/// Icono de bandeja y notificaciones del shell. Implementa <see cref="INotifier"/>:
/// si el shell no puede mostrar el globo, la aplicación usa su ventana emergente.
/// </summary>
public sealed class TrayIconHost : INotifier, IDisposable
{
    private readonly NotifyIcon _icon;
    private bool _disposed;

    public TrayIconHost(Action open, Action settings, Action history, Action exit)
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()),
            ShowImageMargin = false,
            Font = new Font("Segoe UI", 9F),
            BackColor = Color.FromArgb(18, 24, 33),
            ForeColor = Color.FromArgb(230, 237, 245)
        };

        menu.Items.Add(CreateItem("Abrir Victus Charge Control", open));
        menu.Items.Add(CreateItem("Configuración…", settings));
        menu.Items.Add(CreateItem("Historial…", history));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(CreateItem("Salir", exit));

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Victus Charge Control — la app no detiene la carga",
            ContextMenuStrip = menu,
            Visible = true
        };

        _icon.DoubleClick += (_, _) => open();
    }

    public bool IsAvailable => !_disposed && _icon.Visible;

    public bool Notify(NotificationRequest request)
    {
        if (!IsAvailable)
        {
            return false;
        }

        try
        {
            _icon.BalloonTipTitle = request.Title;
            _icon.BalloonTipText = request.Message;
            _icon.BalloonTipIcon = request.Severity == NotificationSeverity.Warning
                ? ToolTipIcon.Warning
                : ToolTipIcon.Info;
            _icon.ShowBalloonTip(10_000);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            return false;
        }
    }

    private static ToolStripMenuItem CreateItem(string text, Action action)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) => action();
        return item;
    }

    private static Icon LoadIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
            {
                var extracted = Icon.ExtractAssociatedIcon(path);
                if (extracted is not null)
                {
                    return extracted;
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            // Sin icono propio: se usa el genérico.
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _icon.Visible = false;
        _icon.Dispose();
    }

    private sealed class DarkMenuColors : ProfessionalColorTable
    {
        private static readonly Color Background = Color.FromArgb(18, 24, 33);
        private static readonly Color Border = Color.FromArgb(44, 58, 77);
        private static readonly Color Highlight = Color.FromArgb(29, 58, 102);

        public override Color ToolStripDropDownBackground => Background;
        public override Color ImageMarginGradientBegin => Background;
        public override Color ImageMarginGradientMiddle => Background;
        public override Color ImageMarginGradientEnd => Background;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Highlight;
        public override Color MenuItemSelected => Highlight;
        public override Color MenuItemSelectedGradientBegin => Highlight;
        public override Color MenuItemSelectedGradientEnd => Highlight;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Border;
    }
}
