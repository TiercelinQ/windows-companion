using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Abstractions;

namespace WindowsCompanion.Views.Layout;

/// <summary>Resolves the pages of the <c>NavigationView</c> from the container, so each page receives its ViewModel.</summary>
/// <param name="services">The application container.</param>
public sealed class NavigationPageProvider(IServiceProvider services) : INavigationViewPageProvider
{
    /// <inheritdoc />
    public object? GetPage(Type pageType) => services.GetRequiredService(pageType);
}
