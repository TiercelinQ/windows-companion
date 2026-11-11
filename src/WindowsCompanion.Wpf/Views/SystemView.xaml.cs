using WindowsCompanion.ViewModels;

namespace WindowsCompanion.Views;

/// <summary>System page.</summary>
public partial class SystemView
{
    /// <summary>Initializes a new instance of the <see cref="SystemView"/> class.</summary>
    /// <param name="viewModel">Page ViewModel, resolved by the container.</param>
    public SystemView(SystemViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
