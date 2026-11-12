using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using WindowsCompanion.ViewModels;

namespace WindowsCompanion.Views;

/// <summary>Updates page. Code-behind limited to view plumbing: the log follows its last line (F-MAJ-41), the log block moves under the table on a narrow page (E-03).</summary>
public partial class UpdatesView
{
    private bool? _stacked;

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

    // Side by side (log left, table right) on a wide page; log under the table, both full width, below the breakpoint token.
    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var stacked = e.NewSize.Width < (double)FindResource("UpdatesStackBreakpoint");
        if (stacked == _stacked)
        {
            return;
        }

        _stacked = stacked;
        Grid.SetRow(AppsCard, 0);
        Grid.SetColumn(AppsCard, stacked ? 0 : 1);
        Grid.SetColumnSpan(AppsCard, stacked ? 2 : 1);
        Grid.SetRow(LogCard, stacked ? 1 : 0);
        Grid.SetColumn(LogCard, 0);
        Grid.SetColumnSpan(LogCard, stacked ? 2 : 1);
        if (stacked)
        {
            LogCard.Height = (double)FindResource("LogStackedHeight");
        }
        else
        {
            LogCard.ClearValue(FrameworkElement.HeightProperty);
        }
    }
}
