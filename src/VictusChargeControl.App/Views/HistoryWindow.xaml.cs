using System.Windows;
using VictusChargeControl.App.Infrastructure;
using VictusChargeControl.App.ViewModels;

namespace VictusChargeControl.App.Views;

public partial class HistoryWindow : Window
{
    public HistoryWindow(IReadOnlyList<HistoryRow> rows)
    {
        InitializeComponent();
        DataContext = rows;

        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(this);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
