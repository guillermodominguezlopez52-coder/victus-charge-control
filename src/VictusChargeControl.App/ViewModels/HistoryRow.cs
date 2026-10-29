using VictusChargeControl.Core.History;

namespace VictusChargeControl.App.ViewModels;

public sealed record HistoryRow(
    string Time,
    string Percent,
    string State,
    string Ac,
    string Change)
{
    public static HistoryRow FromEntry(HistoryEntry entry) => new(
        entry.Timestamp.ToLocalTime().ToString("dd/MM HH:mm"),
        DisplayText.Percent(entry.Percent),
        DisplayText.State(entry.State),
        entry.AcConnected is bool ac ? (ac ? "AC" : "Batería") : DisplayText.Missing,
        DisplayText.Changes(entry.Changes));
}
