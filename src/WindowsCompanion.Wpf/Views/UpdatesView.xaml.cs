using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WindowsCompanion.ViewModels;

namespace WindowsCompanion.Views;

/// <summary>Updates page. Code-behind limited to view plumbing: the log follows its last line (F-MAJ-41), the wheel over the table scrolls the page.</summary>
public partial class UpdatesView
{
    /// <summary>Initializes a new instance of the <see cref="UpdatesView"/> class.</summary>
    /// <param name="viewModel">Page ViewModel, resolved by the container.</param>
    public UpdatesView(UpdatesViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
        InitializeComponent();

        Loaded += (_, _) => viewModel.Log.CollectionChanged += OnLogChanged;
        Unloaded += (_, _) => viewModel.Log.CollectionChanged -= OnLogChanged;
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (LogList.Items.Count > 0)
        {
            LogList.ScrollIntoView(LogList.Items[^1]);
        }
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
