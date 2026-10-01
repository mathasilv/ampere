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
        await Assert.That(string.Join("\n", resultado.Problemas)).Contains("circuitos com esquemas/tensões diferentes (F+N 127 V, 2F 220 V)");
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

        public bool GravarMemoriaDoQuadro(long quadroId, string? hashDaMemoria)
        {
            Chamadas.Add($"memoria:{quadroId}");
            if (QuadrosSomenteLeitura.Contains(quadroId)) return false;

            Memorias[quadroId] = hashDaMemoria;
            return true;
        }

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
