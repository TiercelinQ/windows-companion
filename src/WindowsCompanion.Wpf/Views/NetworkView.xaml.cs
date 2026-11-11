using WindowsCompanion.ViewModels;

namespace WindowsCompanion.Views;

/// <summary>Network page.</summary>
public partial class NetworkView
{
    /// <summary>Initializes a new instance of the <see cref="NetworkView"/> class.</summary>
    /// <param name="viewModel">Page ViewModel, resolved by the container.</param>
    public NetworkView(NetworkViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
