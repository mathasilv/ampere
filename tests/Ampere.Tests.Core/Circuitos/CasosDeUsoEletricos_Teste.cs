using Ampere.Core.Cargas;
using Ampere.Core.Circuitos;
using TUnit.Assertions.Enums;

namespace Ampere.Tests.Core.Circuitos;

public class ClassificacaoEmLote_Teste
{
    private const string Transacao = "transacao:" + ClassificacaoEmLote.NomeDaTransacao;

    [Test]
    public async Task Classificacao_invalida_nao_le_nem_escreve_nada()
    {
        var documento = new DocumentoEletricoFalso();

        var resultado = ClassificacaoEmLote.Executar([1, 2], new ClassificacaoDeCarga(TipoDeCarga.Reserva), documento);

        await Assert.That(resultado.Problemas).IsNotEmpty();
        await Assert.That(documento.Chamadas).IsEmpty();
    }

    [Test]
    public async Task Classifica_os_aceitos_numa_unica_transacao_e_relata_os_recusados()
    {
        var documento = new DocumentoEletricoFalso { Recusados = { [3] = "categoria sem AMP_TipoCarga" } };

        var resultado = ClassificacaoEmLote.Executar([1, 2, 3], new ClassificacaoDeCarga(TipoDeCarga.TUG, PotenciaVA: 100m), documento);

        await Assert.That(documento.Chamadas).IsEquivalentTo(["filtrar:1,2,3", Transacao, "classificar:1,2@TUG/100"], CollectionOrdering.Matching);
        await Assert.That(resultado.Classificados).IsEqualTo(2);
        await Assert.That(resultado.Recusados.Select(recusado => $"{recusado.Id}: {recusado.Motivo}")).IsEquivalentTo(["3: categoria sem AMP_TipoCarga"]);
    }

    [Test]
    public async Task Sem_elemento_aceito_nao_abre_transacao()
    {
        var documento = new DocumentoEletricoFalso { Recusados = { [1] = "parede" } };

        var resultado = ClassificacaoEmLote.Executar([1], new ClassificacaoDeCarga(TipoDeCarga.TUG), documento);

        await Assert.That(resultado.Classificados).IsEqualTo(0);
        await Assert.That(documento.Chamadas).IsEquivalentTo(["filtrar:1"], CollectionOrdering.Matching);
    }
}

public class CriacaoDeCircuitos_Teste
{
    private const string Transacao = "transacao:" + CriacaoDeCircuitos.NomeDaTransacao;
    private const long Quadro = 10;

    private static readonly IReadOnlyDictionary<TipoDeCarga, RegraDeAgrupamento> SemRegras =
        new Dictionary<TipoDeCarga, RegraDeAgrupamento>();

    [Test]
    public async Task Cria_todos_os_circuitos_planejados_numa_unica_transacao()
    {
        var documento = new DocumentoEletricoFalso
        {
            Pontos = [Ponto(1, TipoDeCarga.Iluminacao), Ponto(2, TipoDeCarga.Iluminacao), Ponto(3, TipoDeCarga.TUG)]
        };

        var plano = CriacaoDeCircuitos.Executar([1, 2, 3], Quadro, SemRegras, ConfiguracaoDeNumeracao.Padrao, documento);

        await Assert.That(plano.Circuitos.Count).IsEqualTo(2);
        await Assert.That(documento.Chamadas).IsEquivalentTo(
            ["ler-pontos:1,2,3", "ler-quadro:10", Transacao, "criar:10:IL-01[1,2];TUG-01[3]"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Numeracao_continua_a_do_quadro()
    {
        var documento = new DocumentoEletricoFalso { Pontos = [Ponto(1, TipoDeCarga.TUG)], NumerosDoQuadro = ["TUG-01"] };

        var plano = CriacaoDeCircuitos.Executar([1], Quadro, SemRegras, ConfiguracaoDeNumeracao.Padrao, documento);

        await Assert.That(plano.Circuitos[0].Numero).IsEqualTo("TUG-02");
    }

    [Test]
    public async Task Sem_circuito_a_criar_nao_abre_transacao()
    {
        var documento = new DocumentoEletricoFalso { Pontos = [Ponto(1, tipo: null)] };

        var plano = CriacaoDeCircuitos.Executar([1], Quadro, SemRegras, ConfiguracaoDeNumeracao.Padrao, documento);

        await Assert.That(plano.Circuitos).IsEmpty();
        await Assert.That(documento.Chamadas).IsEquivalentTo(["ler-pontos:1", "ler-quadro:10"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Recusados_na_leitura_aparecem_com_os_ignorados_do_plano_em_ordem_de_id()
    {
        var documento = new DocumentoEletricoFalso
        {
            Pontos = [Ponto(2, tipo: null), Ponto(4, TipoDeCarga.TUG)],
            Recusados = { [3] = "sem conector elétrico de força" }
        };

        var plano = CriacaoDeCircuitos.Executar([2, 3, 4], Quadro, SemRegras, ConfiguracaoDeNumeracao.Padrao, documento);

        await Assert.That(plano.Ignorados.Select(ignorado => $"{ignorado.Id}: {ignorado.Motivo}")).IsEquivalentTo(
            ["2: não classificado (AMP_TipoCarga vazio ou desconhecido)", "3: sem conector elétrico de força"], CollectionOrdering.Matching);
    }

    private static PontoDeCarga Ponto(long id, TipoDeCarga? tipo) => new(id, tipo, 100m, "120 V · 1 polo");
}

/// <summary>Registra as chamadas em ordem e denuncia escrita fora de transação.</summary>
internal sealed class DocumentoEletricoFalso : IDocumentoEletrico
{
    private bool _emTransacao;

    public List<string> Chamadas { get; } = [];

    public Dictionary<long, string> Recusados { get; } = [];

    public List<PontoDeCarga> Pontos { get; init; } = [];

    public List<string> NumerosDoQuadro { get; init; } = [];

    public void EmUmaTransacao(string nome, Action acao)
    {
        Chamadas.Add($"transacao:{nome}");
        _emTransacao = true;
        try
        {
            acao();
        }
        finally
        {
            _emTransacao = false;
        }
    }

    public ElementosFiltrados FiltrarClassificaveis(IReadOnlyCollection<long> ids)
    {
        Chamadas.Add($"filtrar:{string.Join(",", ids)}");
        return new ElementosFiltrados(
            ids.Where(id => !Recusados.ContainsKey(id)).ToList(),
            ids.Where(Recusados.ContainsKey).Select(id => new PontoIgnorado(id, Recusados[id])).ToList());
    }

    public void Classificar(IReadOnlyCollection<long> ids, ClassificacaoDeCarga classificacao) =>
        Chamadas.Add($"classificar:{string.Join(",", ids)}@{classificacao.Tipo}/{classificacao.PotenciaVA}{ForaDaTransacao()}");

    public LeituraDePontos LerPontos(IReadOnlyCollection<long> ids)
    {
        Chamadas.Add($"ler-pontos:{string.Join(",", ids)}");
        return new LeituraDePontos(Pontos, Recusados.Select(recusado => new PontoIgnorado(recusado.Key, recusado.Value)).ToList());
    }

    public QuadroEletrico LerQuadro(long quadroId)
    {
        Chamadas.Add($"ler-quadro:{quadroId}");
        return new QuadroEletrico(quadroId, "QD1", NumerosDoQuadro);
    }

    public void CriarCircuitos(long quadroId, IReadOnlyList<CircuitoPlanejado> circuitos) =>
        Chamadas.Add($"criar:{quadroId}:{string.Join(";", circuitos.Select(circuito => $"{circuito.Numero}[{string.Join(",", circuito.Pontos)}]"))}{ForaDaTransacao()}");

    private string ForaDaTransacao() => _emTransacao ? string.Empty : " FORA DA TRANSACAO";
}
