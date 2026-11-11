using WindowsCompanion.Models;

namespace WindowsCompanion.Tests.Models;

public sealed class InstallBatchTests
{
    [Fact]
    public void Calcule_le_pourcentage_global_pendant_le_telechargement()
    {
        var batch = new InstallBatch();
        batch.Add(4);
        batch.MarkCompleted();

        // (1 + 0.7 * 50 / 100) / 4 = 33.75 %
        Assert.Equal(33.75, batch.GlobalPercent(new InstallProgress(InstallPhase.Downloading, 50)), 2);
        Assert.Equal(2, batch.CurrentPosition);
    }

    [Fact]
    public void Garde_70_pour_cent_pendant_l_installation()
    {
        var batch = new InstallBatch();
        batch.Add(2);

        Assert.Equal(35, batch.GlobalPercent(new InstallProgress(InstallPhase.Installing, 100)), 2);
    }

    [Fact]
    public void Ajuste_le_total_quand_on_ajoute_ou_retire_pendant_un_lot()
    {
        var batch = new InstallBatch();
        batch.Add(3);
        batch.Add(1);
        batch.RemoveWaiting();

        Assert.Equal(3, batch.Total);
    }

    [Fact]
    public void Ne_descend_jamais_sous_les_elements_deja_traites()
    {
        var batch = new InstallBatch();
        batch.Add(1);
        batch.MarkCompleted();
        batch.RemoveWaiting();

        Assert.Equal(1, batch.Total);
        Assert.Equal(100, batch.GlobalPercent(null));
    }

    [Fact]
    public void Repart_de_zero_apres_reinitialisation()
    {
        var batch = new InstallBatch();
        batch.Add(2);
        batch.MarkCompleted();
        batch.Reset();

        Assert.Equal(0, batch.Total);
        Assert.Equal(0, batch.GlobalPercent(null));
    }
}
