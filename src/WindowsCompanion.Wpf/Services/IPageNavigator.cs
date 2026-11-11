using WindowsCompanion.Models;

namespace WindowsCompanion.Services;

/// <summary>Navigation by page key, so no ViewModel knows a view type (D-09). Implemented in <c>Views/Layout</c>.</summary>
public interface IPageNavigator
{
    /// <summary>Raised after a page has been shown.</summary>
    event EventHandler<PageKey>? Navigated;

    /// <summary>Gets the page currently shown.</summary>
    PageKey Current { get; }

    /// <summary>Shows a page.</summary>
    /// <param name="page">The page.</param>
    void Navigate(PageKey page);
}
