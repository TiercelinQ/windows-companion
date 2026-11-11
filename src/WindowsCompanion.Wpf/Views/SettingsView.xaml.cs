using WindowsCompanion.ViewModels;

namespace WindowsCompanion.Views;

/// <summary>Settings page.</summary>
public partial class SettingsView
{
    /// <summary>Initializes a new instance of the <see cref="SettingsView"/> class.</summary>
    /// <param name="viewModel">Page ViewModel, resolved by the container.</param>
    public SettingsView(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
