using Ampere.Core.Quadros;

namespace Ampere.Tests.Core.Quadros;

public class CargasPorFase_Teste
{
    private static readonly string[] Abc = ["A", "B", "C"];

    [Test]
    public async Task Potencia_dividida_entre_as_fases_e_corrente_pela_soma_das_correntes_de_linha()
    {
        var balanco = CargasPorFase.Calcular(Abc, 127m,
        [
            new CircuitoNasFases(1, "IL-01", 600m, 600m, ["A"], "F+N", 127m),
            new CircuitoNasFases(2, "TUG-01", 1200m, 600m, ["B"], "F+N", 127m),
            new CircuitoNasFases(3, "TUE-01", 4400m, 4400m, ["A", "C"], "2F", 220m)
        ])!;

        await Assert.That(Linhas(balanco)).IsEqualTo("A:2800/2800|B:1200/600|C:2200/2200");
        await Assert.That(balanco.PelaDemanda).IsTrue();
        await Assert.That(balanco.FaseMaisCarregada).IsEqualTo("A");
        await Assert.That(Math.Round(balanco.DesequilibrioPct, 4)).IsEqualTo(78.5714m); // (2800 − 600) / 2800
        // A: 600 / 127 + 4400 / 220 (o 2F leva S / V fase-fase em cada linha, não metade de S / V fase-neutro).
        await Assert.That(Math.Round(balanco.Fases[0].CorrenteA!.Value, 4)).IsEqualTo(24.7244m);
        await Assert.That(balanco.Fases[2].CorrenteA).IsEqualTo(20m);
        await Assert.That(balanco.MaiorCorrente!.Fase).IsEqualTo("A");
        await Assert.That(balanco.CircuitosSemFase).IsEmpty();
    }

    [Test]
    public async Task Circuito_trifasico_leva_S_sobre_raiz_de_3_V_em_cada_fase()
    {
        var balanco = CargasPorFase.Calcular(Abc, null, [new CircuitoNasFases(1, "TUE-01", 6600m, 6600m, ["A", "B", "C"], "3F", 220m)])!;

        await Assert.That(balanco.Fases.Select(fase => Math.Round(fase.CorrenteA!.Value, 4)).Distinct().Single()).IsEqualTo(17.3205m);
        await Assert.That(balanco.DesequilibrioPct).IsEqualTo(0m);
    }

    [Test]
    public async Task Sem_demanda_usa_a_potencia_instalada()
    {
        var balanco = CargasPorFase.Calcular(Abc, 127m,
        [
            new CircuitoNasFases(1, "IL-01", 600m, null, ["A"], "F+N", 127m),
            new CircuitoNasFases(2, "TUG-01", 300m, 150m, ["B"], "F+N", 127m)
        ])!;

        await Assert.That(balanco.PelaDemanda).IsFalse();
        await Assert.That(balanco.Fases.All(fase => fase.DemandaVA is null)).IsTrue();
        await Assert.That(Math.Round(balanco.Fases[0].CorrenteA!.Value, 4)).IsEqualTo(4.7244m); // 600 / 127
        await Assert.That(balanco.Fases[2].CorrenteA).IsEqualTo(0m);
        await Assert.That(balanco.DesequilibrioPct).IsEqualTo(100m); // fase C sem carga
    }

    [Test]
    public async Task Circuito_2F_mais_N_usa_a_tensao_fase_neutro_da_alimentacao()
    {
        CircuitoNasFases[] circuitos = [new CircuitoNasFases(1, "TUE-01", 2400m, 2400m, ["A", "B"], "2F+N", 240m)];

        var comTensao = CargasPorFase.Calcular(["A", "B"], 120m, circuitos)!;
        var semTensao = CargasPorFase.Calcular(["A", "B"], null, circuitos)!;

        await Assert.That(comTensao.Fases[0].CorrenteA).IsEqualTo(10m); // 2400 / (2 · 120)
        await Assert.That(semTensao.Fases[0].CorrenteA).IsNull();
        await Assert.That(semTensao.MaiorCorrente).IsNull();
    }

    [Test]
    public async Task Circuito_sem_fase_com_fase_fora_do_quadro_ou_polos_errados_fica_de_fora_pelo_id()
    {
        var balanco = CargasPorFase.Calcular(["A", "B"], 127m,
        [
            new CircuitoNasFases(1, "TUG-01", 600m, 600m, ["A"], "F+N", 127m),
            new CircuitoNasFases(2, "TUG-01", 600m, 600m, null, "F+N", 127m),
            new CircuitoNasFases(3, "TUG-02", 600m, 600m, ["C"], "F+N", 127m),
            new CircuitoNasFases(4, "TUG-03", 600m, 600m, ["A", "B"], "F+N", 127m)
        ])!;

        await Assert.That(Linhas(balanco)).IsEqualTo("A:600/600|B:0/0");
        await Assert.That(balanco.CircuitosSemFase).IsEquivalentTo(["TUG-01", "TUG-02", "TUG-03"]);
    }

    [Test]
    public async Task Sem_fases_do_quadro_usa_as_dos_circuitos_e_sem_o_que_distribuir_nao_ha_balanco()
    {
        var dosCircuitos = CargasPorFase.Calcular(null, null,
            [new CircuitoNasFases(1, "IL-01", 600m, 600m, ["R"], "F+N", 127m), new CircuitoNasFases(2, "IL-02", 300m, 300m, ["S"], "F+N", 127m)]);

        await Assert.That(string.Join(",", dosCircuitos!.Fases.Select(fase => fase.Fase))).IsEqualTo("R,S");
        await Assert.That(CargasPorFase.Calcular(null, null, [new CircuitoNasFases(1, "IL-01", 600m, 600m, null)])).IsNull();
        await Assert.That(CargasPorFase.Calcular(Abc, 127m, [new CircuitoNasFases(1, "IL-01", 600m, 600m, null)])).IsNull();
        await Assert.That(CargasPorFase.Calcular(["A"], 127m, [new CircuitoNasFases(1, "IL-01", 600m, 600m, ["A"], "F+N", 127m)])).IsNull();
    }

    [Test]
    public async Task Tensao_fase_neutro_derivada_so_quando_da_para_saber()
    {
        await Assert.That(CargasPorFase.FaseNeutro("F+N", 127m)).IsEqualTo(127m);
        await Assert.That(Math.Round(CargasPorFase.FaseNeutro("3F+N", 220m)!.Value, 3)).IsEqualTo(127.017m);
        await Assert.That(CargasPorFase.FaseNeutro("2F+N", 240m)).IsNull();
        await Assert.That(CargasPorFase.FaseNeutro("3F", 220m)).IsNull();
    }

    private static string Linhas(BalancoDasFases balanco) =>
        string.Join("|", balanco.Fases.Select(fase => $"{fase.Fase}:{fase.PotenciaInstaladaVA:0.##}/{fase.DemandaVA:0.##}"));
}
