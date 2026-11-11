using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WindowsCompanion.ViewModels;

namespace WindowsCompanion.Views;

/// <summary>History page. Code-behind limited to view plumbing: the wheel over the table scrolls the page.</summary>
public partial class HistoryView
{
    /// <summary>Initializes a new instance of the <see cref="HistoryView"/> class.</summary>
    /// <param name="viewModel">Page ViewModel, resolved by the container.</param>
    public HistoryView(HistoryViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    // The table has no own scroll (it grows with its rows); its inner ScrollViewer would still swallow the wheel.
    private void OnGridPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not DependencyObject grid || VisualTreeHelper.GetParent(grid) is not UIElement parent)
        {
            return;
        }

        e.Handled = true;
        parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = sender });
    }
}
