using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WindowsCompanion.Models;
using WindowsCompanion.Services;
using WindowsCompanion.Tests.Support;

namespace WindowsCompanion.Tests.Services;

public sealed class UpdateCheckServiceTests
{
    private readonly IPreferencesService _preferences = Substitute.For<IPreferencesService>();
    private readonly IProcessRunner _runner = Substitute.For<IProcessRunner>();
    private readonly ManualTimeProvider _time = new();

    public UpdateCheckServiceTests() => _preferences.Current.Returns(new AppSettings());

    [Theory]
    [InlineData("v1.2.0", "1.2.0")]
    [InlineData("1.2", "1.2.0")]
    [InlineData("V2.0.1-beta.1", "2.0.1")]
    [InlineData("1.0.2.0", "1.0.2")]
    [InlineData("1.0.2+build5", "1.0.2")]
    public void Lit_un_tag_de_release(string tag, string expected)
    {
        Assert.True(UpdateCheckService.TryParseTag(tag, out var version));
        Assert.Equal(expected, version.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("1")]
    public void Refuse_un_tag_illisible(string? tag) => Assert.False(UpdateCheckService.TryParseTag(tag, out _));

    [Fact]
    public async Task Signale_une_version_plus_recente_et_note_l_heure_de_la_verification()
    {
        var service = Create(FakeHttpMessageHandler.Release("v99.0.0"));

        var result = await service.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        Assert.True(result.Ok);
        Assert.Equal(UpdateCheckStatus.Available, result.Data!.Status);
        Assert.Equal("99.0.0", result.Data.LatestVersion);
        await _preferences.ReceivedWithAnyArgs(1).UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Reste_a_jour_quand_le_tag_est_celui_installe()
    {
        var service = Create(FakeHttpMessageHandler.Release("v" + AppConfig.AppVersion));

        var result = await service.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCheckStatus.UpToDate, result.Data!.Status);
    }

    [Fact]
    public async Task Reste_a_jour_quand_le_tag_est_plus_ancien()
    {
        var service = Create(FakeHttpMessageHandler.Release("v0.0.1"));

        var result = await service.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCheckStatus.UpToDate, result.Data!.Status);
    }

    [Fact]
    public async Task Envoie_un_user_agent_generique_a_l_adresse_de_l_api()
    {
        var handler = FakeHttpMessageHandler.Release("v99.0.0");
        var service = Create(handler);

        await service.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        Assert.Equal(AppConfig.ReleasesApiUrl, handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal($"{AppConfig.AppName}/{AppConfig.AppVersion}", handler.LastRequest.Headers.UserAgent.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "{}")]
    [InlineData(HttpStatusCode.OK, "pas du json")]
    [InlineData(HttpStatusCode.OK, "{}")]
    [InlineData(HttpStatusCode.OK, """{ "tag_name": "latest" }""")]
    public async Task Renvoie_un_echec_sans_noter_l_heure_quand_la_reponse_est_inutilisable(HttpStatusCode status, string body)
    {
        var service = Create(FakeHttpMessageHandler.Json(body, status));

        var result = await service.CheckAsync(manual: true, TestContext.Current.CancellationToken);

        Assert.False(result.Ok);
        Assert.Equal(FeedbackType.Warning, result.Error!.Type);
        await _preferences.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Renvoie_un_echec_quand_le_delai_est_depasse()
    {
        var service = Create(new FakeHttpMessageHandler((_, _) => throw new TaskCanceledException()));

        var result = await service.CheckAsync(manual: true, TestContext.Current.CancellationToken);

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task Propage_l_annulation_demandee_par_l_appelant()
    {
        var service = Create(FakeHttpMessageHandler.Release("v99.0.0"));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CheckAsync(manual: true, cancelled.Token));
    }

    [Fact]
    public async Task N_appelle_pas_github_quand_la_recherche_automatique_est_desactivee()
    {
        _preferences.Current.Returns(new AppSettings { CheckAppUpdates = false });
        var handler = FakeHttpMessageHandler.Release("v99.0.0");
        var service = Create(handler);

        var result = await service.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCheckStatus.Skipped, result.Data!.Status);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Attend_24_heures_entre_deux_verifications_automatiques()
    {
        _preferences.Current.Returns(new AppSettings { LastAppUpdateCheckAt = _time.GetUtcNow().AddHours(-23) });
        var handler = FakeHttpMessageHandler.Release("v99.0.0");
        var service = Create(handler);

        var tooSoon = await service.CheckAsync(manual: false, TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromHours(1));
        var due = await service.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCheckStatus.Skipped, tooSoon.Data!.Status);
        Assert.Equal(UpdateCheckStatus.Available, due.Data!.Status);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Verifie_a_nouveau_quand_la_derniere_verification_est_dans_le_futur()
    {
        _preferences.Current.Returns(new AppSettings { LastAppUpdateCheckAt = _time.GetUtcNow().AddDays(3) });
        var handler = FakeHttpMessageHandler.Release("v99.0.0");
        var service = Create(handler);

        await service.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task La_demande_manuelle_ignore_le_reglage_et_le_delai()
    {
        _preferences.Current.Returns(new AppSettings { CheckAppUpdates = false, LastAppUpdateCheckAt = _time.GetUtcNow() });
        var handler = FakeHttpMessageHandler.Release("v99.0.0");
        var service = Create(handler);

        var result = await service.CheckAsync(manual: true, TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCheckStatus.Available, result.Data!.Status);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void Ouvre_la_page_des_releases_par_le_runner_sans_shell()
    {
        var service = Create(FakeHttpMessageHandler.Release("v99.0.0"));

        service.OpenReleasesPage();

        _runner.Received(1).Start(
            Arg.Is<string>(path => path.EndsWith("explorer.exe", StringComparison.OrdinalIgnoreCase)),
            Arg.Is<IReadOnlyList<string>>(args => args.Count == 1 && args[0] == AppConfig.ReleasesPageUrl));
    }

    private UpdateCheckService Create(HttpMessageHandler handler) =>
        new(new HttpClient(handler), _preferences, _runner, _time, NullLogger<UpdateCheckService>.Instance);
}
