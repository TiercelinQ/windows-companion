using WindowsCompanion.Models;

namespace WindowsCompanion.Tests.Models;

public sealed class InstallBatchTests
{
    [Fact]
    public void Avance_d_un_cran_par_element_termine()
    {
        var batch = new InstallBatch();
        batch.Add(4);
        batch.MarkCompleted();

        // 1 element termine sur 4 = 25 %, l'element en cours ne compte pas
        Assert.Equal(25, batch.GlobalPercent(), 2);
        Assert.Equal(2, batch.CurrentPosition);
    }

    [Fact]
    public void Reste_a_zero_tant_qu_aucun_element_n_est_termine()
    {
        var batch = new InstallBatch();
        batch.Add(2);

        Assert.Equal(0, batch.GlobalPercent(), 2);
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
        Assert.Equal(100, batch.GlobalPercent());
    }

    [Fact]
    public void Repart_de_zero_apres_reinitialisation()
    {
        var batch = new InstallBatch();
        batch.Add(2);
        batch.MarkCompleted();
        batch.Reset();

        Assert.Equal(0, batch.Total);
        Assert.Equal(0, batch.GlobalPercent());
    }
}
