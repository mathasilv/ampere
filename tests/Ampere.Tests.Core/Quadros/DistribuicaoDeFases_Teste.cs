using Ampere.Core.Quadros;

namespace Ampere.Tests.Core.Quadros;

/// <summary>Correntes pela demanda: circuito F+N 127 V com demanda 127 · I VA tem I A em cada fase.</summary>
public class DistribuicaoDeFases_Teste
{
    private static readonly string[] Abc = ["A", "B", "C"];

    [Test]
    public async Task Move_o_circuito_que_mais_baixa_a_fase_de_maior_corrente()
    {
        // A: IL-01 3 A + chuveiro 10 A; B: IL-02 2 A + chuveiro; C: TUG-01 4 A.
        var sugestao = DistribuicaoDeFases.Sugerir(Abc, 127m,
        [
            FaseNeutro(1, "IL-01", 3m, "A"),
            FaseNeutro(2, "IL-02", 2m, "B"),
            FaseNeutro(3, "TUG-01", 4m, "C"),
            new CircuitoNasFases(4, "TUE-01", 2200m, 2200m, ["A", "B"], "2F", 220m)
        ])!;

        // IL-01 para C (A cai para 10 A), depois IL-02 para C (B cai para 10 A): 10 A é o mínimo com o chuveiro em duas fases.
        await Assert.That(Mudancas(sugestao)).IsEqualTo("IL-01 A→C; IL-02 B→C");
        await Assert.That(string.Join("|", sugestao.CorrentesAntesA)).IsEqualTo("13|12|4");
        await Assert.That(string.Join("|", sugestao.CorrentesDepoisA)).IsEqualTo("10|10|9");
        await Assert.That(sugestao.PelaDemanda).IsTrue();
    }

    [Test]
    public async Task Troca_dois_circuitos_quando_nenhuma_mudanca_sozinha_ajuda()
    {
        // A: 6 + 4 = 10; B: 5 + 3 = 8; C: 8. Mover qualquer um deixa outra fase com 12 ou mais; trocar 6 e 5 dá 9, 9 e 8.
        var sugestao = DistribuicaoDeFases.Sugerir(Abc, 127m,
        [
            FaseNeutro(1, "TUG-01", 6m, "A"),
            FaseNeutro(2, "TUG-02", 4m, "A"),
            FaseNeutro(3, "TUG-03", 5m, "B"),
            FaseNeutro(4, "TUG-04", 3m, "B"),
            FaseNeutro(5, "TUG-05", 8m, "C")
        ])!;

        await Assert.That(Mudancas(sugestao)).IsEqualTo("TUG-01 A→B; TUG-03 B→A");
        await Assert.That(sugestao.CorrentesDepoisA.Max()).IsEqualTo(9m);
    }

    [Test]
    public async Task Quadro_equilibrado_nao_muda_nada()
    {
        var sugestao = DistribuicaoDeFases.Sugerir(Abc, 127m, [FaseNeutro(1, "TUG-01", 5m, "A"), FaseNeutro(2, "TUG-02", 5m, "B"), FaseNeutro(3, "TUG-03", 5m, "C")])!;

        await Assert.That(sugestao.Mudancas).IsEmpty();
    }

    [Test]
    public async Task Ganho_pequeno_demais_nao_vale_a_mudanca()
    {
        // Mover IL-01 (0,05 A) baixaria a fase A em 0,05 A: abaixo de 0,1 A, e a soma dos quadrados cai menos de 1%.
        var sugestao = DistribuicaoDeFases.Sugerir(Abc, 127m,
            [FaseNeutro(1, "IL-01", 0.05m, "A"), FaseNeutro(2, "TUG-01", 5m, "A"), FaseNeutro(3, "TUG-02", 5m, "B"), FaseNeutro(4, "TUG-03", 5m, "C")])!;

        await Assert.That(sugestao.Mudancas).IsEmpty();
    }

    [Test]
    public async Task Circuito_sem_fase_vai_para_onde_a_maior_corrente_fica_menor()
    {
        var sugestao = DistribuicaoDeFases.Sugerir(Abc, 127m, [FaseNeutro(1, "TUG-01", 5m, "A"), FaseNeutro(2, "TUG-02", 5m, "B"), FaseNeutro(3, "TUG-03", 2m, null)])!;

        await Assert.That(Mudancas(sugestao)).IsEqualTo("TUG-03 sem fase→C");
        await Assert.That(string.Join("|", sugestao.CorrentesAntesA)).IsEqualTo("5|5|0");
    }

    [Test]
    public async Task Circuito_trifasico_fica_onde_esta()
    {
        var sugestao = DistribuicaoDeFases.Sugerir(Abc, 127m,
            [new CircuitoNasFases(1, "MOT-01", 6600m, 6600m, ["A", "B", "C"], "3F", 220m), FaseNeutro(2, "TUG-01", 5m, "A")])!;

        await Assert.That(sugestao.Mudancas).IsEmpty();
    }

    [Test]
    public async Task Sem_o_que_distribuir_nao_ha_sugestao()
    {
        await Assert.That(DistribuicaoDeFases.Sugerir(["A"], 127m, [FaseNeutro(1, "TUG-01", 5m, "A")])).IsNull();
        await Assert.That(DistribuicaoDeFases.Sugerir(Abc, 127m, [new CircuitoNasFases(1, "TUG-01", 600m, 600m, ["A"])])).IsNull();
        await Assert.That(DistribuicaoDeFases.Sugerir(["A", "B"], 127m, [new CircuitoNasFases(1, "MOT-01", 6600m, 6600m, null, "3F", 220m)])).IsNull();
    }

    private static CircuitoNasFases FaseNeutro(long id, string numero, decimal correnteA, string? fase) =>
        new(id, numero, 127m * correnteA, 127m * correnteA, fase is null ? null : [fase], "F+N", 127m);

    private static string Mudancas(SugestaoDeFases sugestao) =>
        string.Join("; ", sugestao.Mudancas.Select(mudanca =>
            $"{mudanca.Numero} {(mudanca.Atuais is { } atuais ? string.Join(",", atuais) : "sem fase")}→{string.Join(",", mudanca.Sugeridas)}"));
}
