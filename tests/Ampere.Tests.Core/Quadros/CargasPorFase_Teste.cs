using Ampere.Core.Quadros;

namespace Ampere.Tests.Core.Quadros;

public class CargasPorFase_Teste
{
    private static readonly string[] Abc = ["A", "B", "C"];

    [Test]
    public async Task Carga_de_circuito_de_varias_fases_e_dividida_igualmente()
    {
        var balanco = CargasPorFase.Calcular(Abc, 127m,
        [
            new CircuitoNasFases("IL-01", 600m, 600m, ["A"]),
            new CircuitoNasFases("TUG-01", 1200m, 600m, ["B"]),
            new CircuitoNasFases("TUE-01", 4400m, 4400m, ["A", "C"])
        ])!;

        await Assert.That(Linhas(balanco)).IsEqualTo("A:2800/2800|B:1200/600|C:2200/2200");
        await Assert.That(balanco.PelaDemanda).IsTrue();
        await Assert.That(balanco.FaseMaisCarregada).IsEqualTo("A");
        await Assert.That(Math.Round(balanco.DesequilibrioPct, 4)).IsEqualTo(78.5714m); // (2800 − 600) / 2800
        await Assert.That(Math.Round(balanco.Fases[0].CorrenteA!.Value, 4)).IsEqualTo(22.0472m); // 2800 / 127
        await Assert.That(balanco.CircuitosSemFase).IsEmpty();
    }

    [Test]
    public async Task Sem_demanda_usa_a_potencia_instalada_e_fase_vazia_conta()
    {
        var balanco = CargasPorFase.Calcular(Abc, null,
        [
            new CircuitoNasFases("IL-01", 600m, null, ["A"]),
            new CircuitoNasFases("TUG-01", 300m, 150m, ["B"])
        ])!;

        await Assert.That(balanco.PelaDemanda).IsFalse();
        await Assert.That(balanco.Fases.All(fase => fase.DemandaVA is null && fase.CorrenteA is null)).IsTrue();
        await Assert.That(balanco.DesequilibrioPct).IsEqualTo(100m); // fase C sem carga
    }

    [Test]
    public async Task Circuito_sem_fase_ou_com_fase_fora_do_quadro_fica_de_fora_e_e_listado()
    {
        var balanco = CargasPorFase.Calcular(["A", "B"], 127m,
        [
            new CircuitoNasFases("IL-01", 600m, 600m, ["A"]),
            new CircuitoNasFases("TUG-01", 600m, 600m, null),
            new CircuitoNasFases("TUG-02", 600m, 600m, ["C"])
        ])!;

        await Assert.That(Linhas(balanco)).IsEqualTo("A:600/600|B:0/0");
        await Assert.That(balanco.CircuitosSemFase).IsEquivalentTo(["TUG-01", "TUG-02"]);
    }

    [Test]
    public async Task Sem_fases_do_quadro_usa_as_dos_circuitos_e_sem_nenhuma_nao_ha_balanco()
    {
        var dosCircuitos = CargasPorFase.Calcular(null, null, [new CircuitoNasFases("IL-01", 600m, 600m, ["R"]), new CircuitoNasFases("IL-02", 300m, 300m, ["S"])]);

        await Assert.That(string.Join(",", dosCircuitos!.Fases.Select(fase => fase.Fase))).IsEqualTo("R,S");
        await Assert.That(CargasPorFase.Calcular(null, null, [new CircuitoNasFases("IL-01", 600m, 600m, null)])).IsNull();
    }

    [Test]
    [Arguments("F+N", 127, 127)]
    [Arguments("2F+N", 240, 120)]
    public async Task Tensao_fase_neutro_da_alimentacao(string esquema, decimal tensao, decimal faseNeutro)
    {
        await Assert.That(CargasPorFase.FaseNeutro(esquema, tensao)).IsEqualTo(faseNeutro);
        await Assert.That(Math.Round(CargasPorFase.FaseNeutro("3F+N", 220m)!.Value, 3)).IsEqualTo(127.017m);
        await Assert.That(CargasPorFase.FaseNeutro("3F", 220m)).IsNull();
    }

    private static string Linhas(BalancoDasFases balanco) =>
        string.Join("|", balanco.Fases.Select(fase => $"{fase.Fase}:{fase.PotenciaInstaladaVA:0.##}/{fase.DemandaVA:0.##}"));
}
