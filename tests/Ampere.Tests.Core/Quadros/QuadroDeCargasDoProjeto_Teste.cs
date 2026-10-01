using Ampere.Core.Cargas;
using Ampere.Core.Normas;
using Ampere.Core.Quadros;
using Ampere.Tests.Core.Normas;
using TUnit.Assertions.Enums;

namespace Ampere.Tests.Core.Quadros;

/// <summary>
///     Caso de uso "Montar quadro de cargas" contra um documento falso: leitura dos circuitos, gravação dos resultados
///     (potência e fator nos circuitos, hash da memória no quadro) numa única transação.
/// </summary>
/// <remarks>Fatores do perfil fictício, não da NBR 5410 (GAP-005).</remarks>
[Property("Fonte", "TODO_NORMA")]
public class QuadroDeCargasDoProjeto_Teste
{
    private static readonly PerfilNormativo Ficticio = PerfilNormativo.Carregar(PerfilFicticio.Json);

    private static readonly QuadroLido Qd1 = new(10, "QD1",
    [
        new CircuitoLido(101, "IL-01", "Iluminação", 600m, "F+N", 127m),
        new CircuitoLido(102, "TUG-01", "TUG", 1000m, "F+N", 127m)
    ]);

    private static readonly QuadroLido Qd2 = new(20, "QD2", [new CircuitoLido(201, "TUE-01", "TUE", 2000m, "2F", 220m)]);

    [Test]
    public async Task Grava_potencia_e_fator_nos_circuitos_e_o_hash_no_quadro_numa_transacao()
    {
        var documento = new DocumentoDeQuadrosFalso([Qd1, Qd2]);
        var resultados = QuadroDeCargasDoProjeto.Executar(documento, Ficticio);

        var gravacao = QuadroDeCargasDoProjeto.Gravar(resultados, documento);

        await Assert.That(gravacao.CircuitosAtualizados).IsEqualTo(3);
        await Assert.That(gravacao.QuadrosSemMemoria).IsEmpty();
        await Assert.That(documento.Chamadas).IsEquivalentTo(
            ["transacao:" + QuadroDeCargasDoProjeto.NomeDaTransacao, "linhas:3", "memoria:10", "memoria:20", "apagar-outros:10,20"], CollectionOrdering.Matching);
        await Assert.That(documento.Linhas).IsEquivalentTo(
            [new LinhaParaGravar(101, "IL-01", 600m, 0.8m, "QD1"), new LinhaParaGravar(102, "TUG-01", 1000m, 0.5m, "QD1"), new LinhaParaGravar(201, "TUE-01", 2000m, 1m, "QD2")],
            CollectionOrdering.Matching);
        await Assert.That(documento.Memorias[10]).IsEqualTo(resultados[0].Quadro.Memoria!.Hash());
        await Assert.That(documento.Memorias[20]).IsEqualTo(resultados[1].Quadro.Memoria!.Hash());
        await Assert.That(documento.Memorias[10]).IsNotEqualTo(documento.Memorias[20]);
    }

    [Test]
    public async Task Quadro_sem_fator_grava_a_potencia_e_apaga_fator_e_hash_anteriores()
    {
        var documento = new DocumentoDeQuadrosFalso([Qd1]);
        var resultados = QuadroDeCargasDoProjeto.Executar(documento, PerfilNormativo.NBR5410_2004);

        QuadroDeCargasDoProjeto.Gravar(resultados, documento);

        await Assert.That(resultados[0].Quadro.Memoria).IsNull();
        await Assert.That(documento.Linhas.All(linha => linha.Fator is null)).IsTrue();
        await Assert.That(documento.Linhas.Select(linha => linha.PotenciaVA ?? -1m)).IsEquivalentTo([600m, 1000m], CollectionOrdering.Matching);
        await Assert.That(documento.Memorias.ContainsKey(10)).IsTrue();
        await Assert.That(documento.Memorias[10]).IsNull();
    }

    [Test]
    public async Task Sem_quadros_ainda_apaga_o_que_montagens_anteriores_deixaram()
    {
        var documento = new DocumentoDeQuadrosFalso([]);

        var gravacao = QuadroDeCargasDoProjeto.Gravar(QuadroDeCargasDoProjeto.Executar(documento, Ficticio), documento);

        await Assert.That(gravacao.CircuitosAtualizados).IsEqualTo(0);
        await Assert.That(documento.Chamadas).IsEquivalentTo(["transacao:" + QuadroDeCargasDoProjeto.NomeDaTransacao, "apagar-outros:"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Circuito_sem_quadro_perde_fator_e_quadro_da_montagem_anterior()
    {
        var documento = new DocumentoDeQuadrosFalso([Qd1]) { SemQuadro = { new CircuitoLido(301, "TUG-07", "TUG", 360m, "F+N", 127m) } };

        var gravacao = QuadroDeCargasDoProjeto.Gravar(QuadroDeCargasDoProjeto.Executar(documento, Ficticio), documento);

        await Assert.That(gravacao.CircuitosAtualizados).IsEqualTo(3);
        await Assert.That(documento.Linhas[^1]).IsEqualTo(new LinhaParaGravar(301, "TUG-07", 360m, null, null));
    }

    [Test]
    public async Task Quadro_sem_circuitos_que_nao_aceita_edicao_e_informado()
    {
        var documento = new DocumentoDeQuadrosFalso([Qd1]) { OutrosSomenteLeitura = { "QD-ANTIGO" } };

        var gravacao = QuadroDeCargasDoProjeto.Gravar(QuadroDeCargasDoProjeto.Executar(documento, Ficticio), documento);

        await Assert.That(gravacao.QuadrosSemMemoria).IsEquivalentTo(["QD-ANTIGO"]);
    }

    [Test]
    public async Task Circuito_sem_tipo_ou_sem_potencia_fica_fora_com_o_motivo()
    {
        var quadro = new QuadroLido(10, "QD1",
        [
            new CircuitoLido(101, "IL-01", "Iluminação", 600m, "F+N", 127m),
            new CircuitoLido(102, "X-01", null, 100m, "F+N", 127m),
            new CircuitoLido(103, "TUG-09", "TUG", null, "F+N", 127m)
        ]);

        var resultado = QuadroDeCargasDoProjeto.Executar(new DocumentoDeQuadrosFalso([quadro]), Ficticio)[0].Quadro;

        await Assert.That(resultado.Linhas.Select(linha => linha.Numero)).IsEquivalentTo(["IL-01"]);
        var problemas = string.Join("\n", resultado.Problemas);
        await Assert.That(problemas).Contains("circuito X-01: sem AMP_TipoCarga reconhecido");
        await Assert.That(problemas).Contains("circuito TUG-09: sem potência instalada");
    }

    [Test]
    public async Task Circuito_fora_do_quadro_perde_o_fator_e_fica_so_com_a_potencia_lida()
    {
        var quadro = new QuadroLido(10, "QD1",
        [
            new CircuitoLido(101, "IL-01", "Iluminação", 600m, "F+N", 127m),
            new CircuitoLido(102, "X-01", null, 100m, "F+N", 127m),
            new CircuitoLido(103, "TUG-09", "TUG", null, "F+N", 127m)
        ]);
        var documento = new DocumentoDeQuadrosFalso([quadro]);

        var gravacao = QuadroDeCargasDoProjeto.Gravar(QuadroDeCargasDoProjeto.Executar(documento, Ficticio), documento);

        await Assert.That(gravacao.CircuitosAtualizados).IsEqualTo(3);
        await Assert.That(documento.Linhas).IsEquivalentTo(
            [new LinhaParaGravar(101, "IL-01", 600m, 0.8m, "QD1"), new LinhaParaGravar(102, "X-01", 100m, null, "QD1"), new LinhaParaGravar(103, "TUG-09", null, null, "QD1")],
            CollectionOrdering.Matching);
    }

    [Test]
    public async Task Quadro_que_nao_aceita_edicao_fica_sem_hash_e_o_resto_e_gravado()
    {
        var documento = new DocumentoDeQuadrosFalso([Qd1, Qd2]) { QuadrosSomenteLeitura = { 20 } };

        var gravacao = QuadroDeCargasDoProjeto.Gravar(QuadroDeCargasDoProjeto.Executar(documento, Ficticio), documento);

        await Assert.That(gravacao.QuadrosSemMemoria).IsEquivalentTo(["QD2"]);
        await Assert.That(gravacao.CircuitosAtualizados).IsEqualTo(3);
        await Assert.That(documento.Memorias.ContainsKey(10)).IsTrue();
        await Assert.That(documento.Memorias.ContainsKey(20)).IsFalse();
    }

    [Test]
    public async Task Esquemas_diferentes_deixam_a_corrente_do_quadro_sem_calculo()
    {
        var quadro = new QuadroLido(10, "QD1",
        [
            new CircuitoLido(101, "IL-01", "Iluminação", 600m, "F+N", 127m),
            new CircuitoLido(102, "TUE-01", "TUE", 2000m, "2F", 220m)
        ]);

        var resultado = QuadroDeCargasDoProjeto.Executar(new DocumentoDeQuadrosFalso([quadro]), Ficticio)[0].Quadro;

        await Assert.That(resultado.CorrenteDeDemandaA).IsNull();
        await Assert.That(string.Join("\n", resultado.Problemas)).Contains("circuitos com esquemas/tensões diferentes (F+N 127 V, 2F 220 V) e quadro sem sistema de distribuição no Revit");
    }

    [Test]
    public async Task Alimentacao_do_quadro_pelo_sistema_de_distribuicao_calcula_a_corrente_com_circuitos_mistos()
    {
        var quadro = new QuadroLido(10, "QD1",
        [
            new CircuitoLido(101, "IL-01", "Iluminação", 600m, "F+N", 127m),
            new CircuitoLido(102, "TUE-01", "TUE", 2000m, "2F", 220m)
        ], new AlimentacaoDoQuadro("3F+N", 220m, "sistema de distribuição '220/127 Y' do quadro"));

        var resultado = QuadroDeCargasDoProjeto.Executar(new DocumentoDeQuadrosFalso([quadro]), Ficticio)[0].Quadro;

        await Assert.That(resultado.Problemas).IsEmpty();
        await Assert.That(resultado.Esquema).IsEqualTo("3F+N");
        await Assert.That(Math.Round(resultado.CorrenteDeDemandaA!.Value, 6)).IsEqualTo(Math.Round(resultado.DemandaVA!.Value / (1.7320508075688772935274463415m * 220m), 6));
        var passo = resultado.Memoria!.Passos[^1];
        await Assert.That(passo.Expressao).IsEqualTo("I = D_total / (√3 × V)");
        await Assert.That(passo.Observacao).IsEqualTo("alimentação 3F+N 220 V: sistema de distribuição '220/127 Y' do quadro");
    }

    [Test]
    [Arguments("3F+N", 220, "F+N", 220, false)]
    [Arguments("3F+N", 220, "F+N", 127, true)]
    [Arguments("3F+N", 380, "F+N", 220, true)]
    [Arguments("3F+N", 220, "3F", 220, true)]
    [Arguments("3F", 220, "F+N", 127, false)]
    [Arguments("F+N", 127, "2F", 220, false)]
    [Arguments("F+N", 127, "F+N", 127, true)]
    [Arguments("2F", 220, "3F", 220, false)]
    public async Task Circuito_que_a_alimentacao_nao_fornece_vira_problema(string esquema, decimal tensao, string fases, decimal tensaoDoCircuito, bool compativel)
    {
        var quadro = new QuadroLido(10, "QD1", [new CircuitoLido(101, "TUE-01", "TUE", 1000m, fases, tensaoDoCircuito)],
            new AlimentacaoDoQuadro(esquema, tensao, "teste"));

        var problemas = QuadroDeCargasDoProjeto.Executar(new DocumentoDeQuadrosFalso([quadro]), Ficticio)[0].Quadro.Problemas;

        await Assert.That(problemas.Any(problema => problema.Contains("incompatível com a alimentação do quadro"))).IsEqualTo(!compativel);
    }

    [Test]
    public async Task Alimentacao_sem_calculo_de_corrente_diz_o_esquema()
    {
        var quadro = new QuadroLido(10, "QD1", [new CircuitoLido(101, "IL-01", "Iluminação", 600m, "F+N", 120m)],
            new AlimentacaoDoQuadro("2F+N", 240m, "sistema de distribuição '120/240 monofásico' do quadro"));

        var resultado = QuadroDeCargasDoProjeto.Executar(new DocumentoDeQuadrosFalso([quadro]), Ficticio)[0].Quadro;

        await Assert.That(resultado.CorrenteDeDemandaA).IsNull();
        await Assert.That(string.Join("\n", resultado.Problemas)).Contains("esquema desconhecido '2F+N'");
    }

    [Test]
    public async Task Cargas_por_fase_acompanham_o_quadro_com_as_fases_dos_circuitos()
    {
        var quadro = new QuadroLido(10, "QD1",
        [
            new CircuitoLido(101, "IL-01", "Iluminação", 600m, "F+N", 127m, ["A"]),
            new CircuitoLido(102, "TUE-01", "TUE", 2000m, "2F", 220m, ["B", "C"]),
            new CircuitoLido(103, "X-01", null, 100m, "F+N", 127m, ["A"])
        ], new AlimentacaoDoQuadro("3F+N", 220m, "teste", ["A", "B", "C"]));

        var resultado = QuadroDeCargasDoProjeto.Executar(new DocumentoDeQuadrosFalso([quadro]), Ficticio)[0];

        // Fictício: Iluminação 0,8 e TUE 1 → A = 480 VA; B = C = 1000 VA. X-01 está fora do quadro (sem tipo).
        var fases = resultado.Fases!;
        await Assert.That(string.Join("|", fases.Fases.Select(fase => $"{fase.Fase}:{fase.DemandaVA:0.##}"))).IsEqualTo("A:480|B:1000|C:1000");
        await Assert.That(fases.FaseMaisCarregada).IsEqualTo("B");
        await Assert.That(fases.DesequilibrioPct).IsEqualTo(52m);
    }

    [Test]
    public async Task Fatores_da_montagem_ficam_no_quadro_e_refazem_a_mesma_memoria()
    {
        var fatores = new Dictionary<TipoDeCarga, decimal> { [TipoDeCarga.Iluminacao] = 1m };
        var documento = new DocumentoDeQuadrosFalso([Qd1]);

        var montados = QuadroDeCargasDoProjeto.Executar(documento, Ficticio, fatores);
        QuadroDeCargasDoProjeto.Gravar(montados, documento, fatores);
        var refeito = QuadroDeCargasDoProjeto.Montar(Qd1, Ficticio, documento.LerFatoresDoQuadro(Qd1.Id));

        await Assert.That(documento.LerFatoresDoQuadro(Qd1.Id)).IsSameReferenceAs(fatores);
        await Assert.That(refeito.Quadro.Memoria!.Hash()).IsEqualTo(documento.LerMemoriaDoQuadro(Qd1.Id));
    }

    private sealed class DocumentoDeQuadrosFalso(IReadOnlyList<QuadroLido> quadros) : IDocumentoDeQuadros
    {
        public List<string> Chamadas { get; } = [];

        public List<LinhaParaGravar> Linhas { get; } = [];

        public Dictionary<long, string?> Memorias { get; } = [];

        public void EmUmaTransacao(string nome, Action acao)
        {
            Chamadas.Add($"transacao:{nome}");
            acao();
        }

        public IReadOnlyList<QuadroLido> LerQuadrosComCircuitos() => quadros;

        public void GravarLinhas(IReadOnlyList<LinhaParaGravar> linhas)
        {
            Chamadas.Add($"linhas:{linhas.Count}");
            Linhas.AddRange(linhas);
        }

        public HashSet<long> QuadrosSomenteLeitura { get; } = [];

        public Dictionary<long, IReadOnlyDictionary<TipoDeCarga, decimal>?> Fatores { get; } = [];

        public bool GravarMemoriaDoQuadro(long quadroId, string? hashDaMemoria, IReadOnlyDictionary<TipoDeCarga, decimal>? fatoresInformados)
        {
            Chamadas.Add($"memoria:{quadroId}");
            if (QuadrosSomenteLeitura.Contains(quadroId)) return false;

            Memorias[quadroId] = hashDaMemoria;
            Fatores[quadroId] = fatoresInformados;
            return true;
        }

        public string? LerMemoriaDoQuadro(long quadroId) => Memorias.GetValueOrDefault(quadroId);

        public IReadOnlyDictionary<TipoDeCarga, decimal>? LerFatoresDoQuadro(long quadroId) => Fatores.GetValueOrDefault(quadroId);

        public List<CircuitoLido> SemQuadro { get; } = [];

        public List<string> OutrosSomenteLeitura { get; } = [];

        public IReadOnlyList<string> ApagarMemoriaDosOutrosQuadros(IReadOnlyCollection<long> montados)
        {
            Chamadas.Add($"apagar-outros:{string.Join(",", montados)}");
            return OutrosSomenteLeitura;
        }

        public IReadOnlyList<CircuitoLido> LerCircuitosSemQuadro() => SemQuadro;

        public string CriarTabelaDoQuadro(string nomeDoQuadro) => $"{nomeDoQuadro} (tabela)";
    }
}
