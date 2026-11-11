using WindowsCompanion.ViewModels;

namespace WindowsCompanion.Views;

/// <summary>Hardware page.</summary>
public partial class HardwareView
{
    /// <summary>Initializes a new instance of the <see cref="HardwareView"/> class.</summary>
    /// <param name="viewModel">Page ViewModel, resolved by the container.</param>
    public HardwareView(HardwareViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
