using WindowsCompanion.Models;
using WindowsCompanion.Services;
using WindowsCompanion.Tests.Support;
using WindowsCompanion.ViewModels;

namespace WindowsCompanion.Tests.ViewModels;

public sealed class ToastHostViewModelTests
{
    private readonly ManualTimeProvider _time = new();
    private readonly FeedbackService _feedback = new();
    private readonly ToastHostViewModel _host;

    public ToastHostViewModelTests() => _host = new ToastHostViewModel(_feedback, _time);

    private void Show(FeedbackType type, string title) => _feedback.Show(new ResultError(type, title, "message"));

    [Fact]
    public void Garde_quatre_toasts_et_epargne_les_erreurs()
    {
        Show(FeedbackType.Danger, "erreur");
        Show(FeedbackType.Success, "a");
        Show(FeedbackType.Success, "b");
        Show(FeedbackType.Success, "c");
        Show(FeedbackType.Success, "d");

        Assert.Equal(["erreur", "b", "c", "d"], _host.Toasts.Select(t => t.Title));
    }

    [Fact]
    public void Remplace_un_message_repete_au_lieu_de_l_empiler()
    {
        Show(FeedbackType.Info, "même");
        _time.Advance(TimeSpan.FromSeconds(1));
        Show(FeedbackType.Info, "même");

        Assert.Single(_host.Toasts);
    }

    [Fact]
    public async Task Ferme_un_succes_apres_quatre_secondes_et_demie()
    {
        Show(FeedbackType.Success, "ok");

        _time.Advance(TimeSpan.FromMilliseconds(AppConfig.ToastSuccessMs));
        Assert.True(_host.Toasts[0].IsClosing);

        _time.Advance(TimeSpan.FromMilliseconds(AppConfig.ToastExitDelayMs));
        await _host.LastRemoval;
        Assert.Empty(_host.Toasts);
    }

    [Fact]
    public void Garde_une_erreur_jusqu_a_sa_fermeture()
    {
        Show(FeedbackType.Danger, "erreur");

        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.False(_host.Toasts[0].IsClosing);
        Assert.False(_host.Toasts[0].HasTimer);
    }

    [Fact]
    public void Suspend_le_minuteur_au_survol()
    {
        Show(FeedbackType.Warning, "attention");
        var toast = _host.Toasts[0];

        toast.Pause();
        _time.Advance(TimeSpan.FromMilliseconds(AppConfig.ToastWarningMs * 2));
        Assert.False(toast.IsClosing);

        toast.Resume();
        _time.Advance(TimeSpan.FromMilliseconds(AppConfig.ToastWarningMs));
        Assert.True(toast.IsClosing);
    }

    [Fact]
    public async Task Execute_l_action_puis_ferme_le_toast()
    {
        var executed = false;
        _feedback.Show(new ResultError(FeedbackType.Danger, "Échec", "message"), new ToastAction("Réessayer", true, () =>
        {
            executed = true;
            return Task.CompletedTask;
        }));
        var toast = _host.Toasts[0];

        await toast.InvokeActionCommand.ExecuteAsync(toast.Actions[0]);

        Assert.True(executed);
        Assert.True(toast.IsClosing);
    }
}
