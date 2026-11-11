using WindowsCompanion.ViewModels;

namespace WindowsCompanion.Views;

/// <summary>Overview page.</summary>
public partial class OverviewView
{
    /// <summary>Initializes a new instance of the <see cref="OverviewView"/> class.</summary>
    /// <param name="viewModel">Page ViewModel, resolved by the container.</param>
    public OverviewView(OverviewViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
