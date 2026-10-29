namespace VictusChargeControl.Core.Notifications;

public enum NotificationSeverity
{
    Information = 0,
    Warning = 1
}

public sealed record NotificationRequest(
    string Title,
    string Message,
    NotificationSeverity Severity = NotificationSeverity.Information);
