using WindowsCompanion.Models;
using WindowsCompanion.Services;

namespace WindowsCompanion.Tests.Services;

public sealed class FeedbackServiceTests
{
    [Fact]
    public void Transmet_le_toast_avec_deux_actions_au_plus()
    {
        var service = new FeedbackService();
        ToastRequest? received = null;
        service.ToastRequested += (_, request) => received = request;
        var action = new ToastAction("Réessayer", true, () => Task.CompletedTask);

        service.Show(new ResultError(FeedbackType.Danger, "Échec : Docker Desktop", "Code de sortie 1603."), action, action, action);

        Assert.NotNull(received);
        Assert.Equal(FeedbackType.Danger, received.Type);
        Assert.Equal("Échec : Docker Desktop", received.Title);
        Assert.Equal("Code de sortie 1603.", received.Message);
        Assert.Equal(2, received.Actions.Count);
    }
}
