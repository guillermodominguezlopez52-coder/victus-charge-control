namespace VictusChargeControl.Core.Notifications;

/// <summary>
/// Emisor de notificaciones. Devuelve <c>false</c> si el sistema no pudo mostrarla,
/// para que la aplicación use su alternativa interna.
/// </summary>
public interface INotifier
{
    bool IsAvailable { get; }

    bool Notify(NotificationRequest request);
}
