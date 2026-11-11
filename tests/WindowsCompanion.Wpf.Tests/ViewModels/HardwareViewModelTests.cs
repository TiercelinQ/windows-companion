using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;
using WindowsCompanion.Models;
using WindowsCompanion.Services;
using WindowsCompanion.Tests.Support;
using WindowsCompanion.ViewModels;

namespace WindowsCompanion.Tests.ViewModels;

public sealed class HardwareViewModelTests
{
    private readonly IHardwareService _hardware = Substitute.For<IHardwareService>();
    private readonly IFeedbackService _feedback = Substitute.For<IFeedbackService>();
    private readonly IMessenger _messenger = new WeakReferenceMessenger();
    private readonly HardwareViewModel _viewModel;

    public HardwareViewModelTests()
    {
        _hardware.GetSnapshotAsync(default).ReturnsForAnyArgs(Samples.Hardware(Samples.Volume("C:", 931, 612), Samples.Volume("E:", 3726, 3220, disk: 2)));
        _viewModel = new HardwareViewModel(_hardware, Substitute.For<IExportService>(), Substitute.For<IClipboardService>(), _feedback, _messenger, new ManualTimeProvider());
    }

    [Fact]
    public async Task Analyse_au_demarrage_sans_toast_et_publie_l_instantane()
    {
        HardwareSnapshot? published = null;
        _messenger.Register<HardwareSnapshotMessage>(this, (_, m) => published = m.Snapshot);

        await _viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(published);
        Assert.Equal("6 composants détectés", _viewModel.ComponentsText);
        _feedback.DidNotReceiveWithAnyArgs().Show(default!);
    }

    [Fact]
    public async Task Annonce_la_fin_d_une_nouvelle_analyse()
    {
        await _viewModel.AnalyzeCommand.ExecuteAsync(null);

        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Message == "Analyse terminée"), Arg.Any<ToastAction[]>());
    }

    [Fact]
    public async Task Signale_un_volume_presque_plein()
    {
        await _viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Volume E: presque plein", _viewModel.StorageChip);
        Assert.Equal(UsageLevel.Attention, _viewModel.StorageLevel);
        Assert.Equal("86 % utilisé · presque plein", _viewModel.Volumes[1].PercentText);
    }
}
