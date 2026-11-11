using WindowsCompanion.Models;
using WindowsCompanion.Services;
using Wpf.Ui.Controls;

namespace WindowsCompanion.Views.Layout;

/// <summary>WPF UI <see cref="NavigationView"/> implementation of <see cref="IPageNavigator"/> (D-09): only this class knows the view types.</summary>
public sealed class PageNavigator : IPageNavigator
{
    private static readonly Dictionary<Type, PageKey> Pages = new()
    {
        [typeof(OverviewView)] = PageKey.Overview,
        [typeof(UpdatesView)] = PageKey.Updates,
        [typeof(HardwareView)] = PageKey.Hardware,
        [typeof(SystemView)] = PageKey.System,
        [typeof(NetworkView)] = PageKey.Network,
        [typeof(HistoryView)] = PageKey.History,
        [typeof(SettingsView)] = PageKey.Settings,
    };

    private NavigationView? _view;

    /// <inheritdoc />
    public event EventHandler<PageKey>? Navigated;

    /// <inheritdoc />
    public PageKey Current { get; private set; }

    /// <summary>Binds the navigator to the shell navigation view.</summary>
    /// <param name="view">The navigation view of the shell.</param>
    public void Attach(NavigationView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _view = view;
        view.Navigated += (_, args) =>
        {
            if (args.Page is not null && Pages.TryGetValue(args.Page.GetType(), out var page))
            {
                Current = page;
                Navigated?.Invoke(this, page);
            }
        };
    }

    /// <inheritdoc />
    public void Navigate(PageKey page) => _view?.Navigate(PageKeys.ToId(page));
}
