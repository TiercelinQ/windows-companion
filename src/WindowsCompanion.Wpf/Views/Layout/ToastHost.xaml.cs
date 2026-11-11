using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsCompanion.ViewModels;

namespace WindowsCompanion.Views.Layout;

/// <summary>Toast layer (D-01). Code-behind limited to view plumbing: hover pause, rounded clip and screen reader announcement.</summary>
public partial class ToastHost
{
    /// <summary>Initializes a new instance of the <see cref="ToastHost"/> class.</summary>
    public ToastHost() => InitializeComponent();

    private static ToastViewModel? Toast(object sender) => (sender as FrameworkElement)?.DataContext as ToastViewModel;

    // The timer stops while the pointer is over the toast (TS-41).
    private void OnToastMouseEnter(object sender, MouseEventArgs e) => Toast(sender)?.Pause();

    private void OnToastMouseLeave(object sender, MouseEventArgs e) => Toast(sender)?.Resume();

    // Clips the toast to its own rounded outline: a Border does not clip its content, so the timer bar would square
    // the bottom corners. The radius is the one of the style (CornerRadiusMd); the size is only known at run time.
    private void OnToastSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Border toast)
        {
            var radius = toast.CornerRadius.BottomLeft;
            toast.Clip = new RectangleGeometry(new Rect(e.NewSize), radius, radius);
        }
    }

    // Announced without moving the focus: polite for success and information, alert otherwise (TS-60, TS-61).
    private void OnToastLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement element && UIElementAutomationPeer.CreatePeerForElement(element) is { } peer)
        {
            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }
}
