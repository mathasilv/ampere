using Ampere.Core.Alimentadores;
using Ampere.Core.Cargas;
using Ampere.Core.Catalogos;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Memoria;
using Ampere.Core.Normas;
using Ampere.Core.Quadros;
using Ampere.Tests.Core.Catalogos;
using Ampere.Tests.Core.Normas;

namespace Ampere.Tests.Core.Alimentadores;

/// <summary>
///     Alimentadores contra o perfil FICTÍCIO, com queda total de 7% no ponto de entrega. QD1 3F+N 220 V: IL-01 (600 VA,
///     fase A), TUG-01 (1270 VA, fase B) e TUE-01 (2200 VA, 2F nas fases B e C); demandas 480, 635 e 2200 VA → correntes
///     A 3,78, B 15 e C 10 A.
/// </summary>
public class DimensionamentoDeAlimentadores_Teste
{
    private const string Origem = CenarioDeAlimentador.Origem;
    private static readonly CatalogosDeProduto Catalogos = CenarioDeAlimentador.Catalogos;

    [Test]
    public async Task Alimentador_pela_fase_mais_carregada_e_pela_queda_que_sobra()
    {
        var cenario = new CenarioDeAlimentador();

        var resultado = Executar(cenario).Single();

        await Assert.That(resultado.Problemas).IsEmpty();
        var calculo = resultado.Circuito!.Dimensionamento!;
        await Assert.That(calculo.Situacao).IsEqualTo(SituacaoDoDimensionamento.Dimensionado);
        // Fase B: TUG-01 635 / 127 = 5 A mais o chuveiro 2F 2200 / 220 = 10 A (corrente de linha, não metade de S / V fase-neutro).
        await Assert.That(Math.Round(calculo.CorrenteDeProjetoA!.Value, 6)).IsEqualTo(15m);
        await Assert.That(calculo.SecaoMm2).IsEqualTo(2.5m);
        await Assert.That(calculo.DisjuntorA).IsEqualTo(16m);
        await Assert.That(calculo.IdrSensibilidadeMa).IsNull();
        await Assert.That(calculo.IdrAvaliado).IsTrue();

        var memoria = calculo.Memoria!;
        await Assert.That(memoria.Circuito).IsEqualTo("Alimentador QD1");
        await Assert.That(Passo(memoria, "Corrente de projeto").Observacao!).StartsWith(
            "S: √3 · V · I da fase de maior corrente do QD1 (B: 15 A, soma das correntes de linha dos circuitos pela demanda; demanda total 3315 VA)");
        var limite = Passo(memoria, "Limite de queda de tensão");
        await Assert.That(limite.Expressao).IsEqualTo("ΔV%máx = ΔV%total − ΔV%terminal");
        await Assert.That(Math.Round(limite.Resultado!.Value, 4)).IsEqualTo(5.7402m);
        await Assert.That(limite.Observacao).IsEqualTo(
            "total: instalação alimentada em baixa tensão pela distribuidora (a partir do ponto de entrega); terminal: a maior queda dos circuitos do QD1 (TUG-01)");
        await Assert.That(Passo(memoria, "Exigência de IDR").Observacao!).StartsWith("a tabela de IDR por local vale para os circuitos terminais");

        await Assert.That(string.Join("|", cenario.Documento.Chamadas)).IsEqualTo($"transacao:{DimensionamentoDeAlimentadores.NomeDaTransacao}|gravar:900|condicoes:900");
    }

    [Test]
    public async Task Sem_cargas_por_fase_completas_a_corrente_e_a_equilibrada()
    {
        var cenario = new CenarioDeAlimentador(semFaseNoTue: true);

        var calculo = Executar(cenario).Single().Circuito!.Dimensionamento!;

        await Assert.That(Math.Round(calculo.CorrenteDeProjetoA!.Value, 6)).IsEqualTo(Math.Round(3315m / (1.7320508075688772935274463415m * 220m), 6));
        await Assert.That(Passo(calculo.Memoria!, "Corrente de projeto").Observacao!).EndsWith("; corrente de cada fase desconhecida: corrente equilibrada");
    }

    [Test]
    public async Task Quadro_sem_alimentador_no_Revit_nao_grava_nada()
    {
        var cenario = new CenarioDeAlimentador { SemAlimentador = true };

        var resultado = Executar(cenario).Single();

        await Assert.That(resultado.Circuito).IsNull();
        await Assert.That(resultado.Problemas.Single()).StartsWith("sem circuito alimentador no Revit");
        await Assert.That(cenario.Documento.Chamadas).IsEmpty();
    }

    [Test]
    public async Task Quadro_de_cargas_desatualizado_apaga_o_alimentador_com_o_motivo()
    {
        var cenario = new CenarioDeAlimentador { MemoriaDoQuadro = "sha256:de-uma-montagem-anterior" };

        var resultado = Executar(cenario).Single();

        await Assert.That(resultado.Problemas.Single()).StartsWith("quadro de cargas desatualizado");
        await Assert.That(resultado.Circuito!.Memoria).IsNull();
        await Assert.That(string.Join("|", cenario.Documento.Chamadas)).IsEqualTo($"transacao:{DimensionamentoDeAlimentadores.NomeDaTransacao}|gravar:900");
    }

    [Test]
    public async Task Cascata_e_quadro_que_alimenta_quadros_ficam_de_fora()
    {
        var cenario = new CenarioDeAlimentador { OrigemAlimentada = true, AlimentaQuadros = true };

        var problemas = Executar(cenario).Single().Problemas;

        await Assert.That(problemas.Count).IsEqualTo(2);
        await Assert.That(problemas[0]).StartsWith("alimentação em cascata (a origem QGBT também é alimentada");
        await Assert.That(problemas[1]).StartsWith("o quadro alimenta outros quadros");
    }

    [Test]
    public async Task Circuito_do_quadro_sem_queda_calculada_impede_o_alimentador()
    {
        var cenario = new CenarioDeAlimentador();
        cenario.Terminais[1] = cenario.Terminais[1] with { ComprimentoM = null };

        var problemas = Executar(cenario).Single().Problemas;

        await Assert.That(problemas.Single()).IsEqualTo(
            "circuitos do quadro sem queda de tensão calculada: TUG-01 (sem AMP_ComprimentoRotaM e sem comprimento do circuito no Revit)");
    }

    [Test]
    public async Task Sem_queda_que_sobre_o_alimentador_para_com_explicacao()
    {
        var cenario = new CenarioDeAlimentador();

        var problemas = DimensionamentoDeAlimentadores.Executar(Origem, CenarioDeAlimentador.ComQuedaTotal(1), Catalogos, cenario.Documento, cenario.Quadros).Single().Problemas;

        await Assert.That(problemas.Single()).StartsWith("os circuitos do quadro já usam 1,2598% (TUG-01) dos 1% de queda total");
    }

    [Test]
    public async Task Sem_condicoes_do_projeto_pede_o_Dimensionar_antes()
    {
        var cenario = new CenarioDeAlimentador();
        cenario.Documento.Condicoes = null;

        var problemas = Executar(cenario).Single().Problemas;

        await Assert.That(problemas.Single()).StartsWith("o modelo não tem as condições do projeto");
    }

    private static IReadOnlyList<ResultadoDoAlimentador> Executar(CenarioDeAlimentador cenario) => cenario.Executar();

    private static PassoDeCalculo Passo(MemoriaDeCalculo memoria, string descricao) => memoria.Passos.Single(passo => passo.Descricao == descricao);
}

/// <summary>Cenário dos alimentadores (também usado pelo golden da memória do alimentador).</summary>
internal sealed class CenarioDeAlimentador
{
    public const string Origem = "ponto_de_entrega";
    public static readonly PerfilNormativo Perfil = ComQuedaTotal(7);
    public static readonly CondicoesDoProjeto Condicoes = new(30m, 1, "Cobre", "B1", "PVC", CatalogosFicticios.TipoDeCondutor, CatalogosFicticios.TipoDeEletroduto);

    public static readonly CatalogosDeProduto Catalogos = new(
        CatalogoDeCondutores.Carregar(CatalogosFicticios.Condutores),
        CatalogoDeEletrodutos.Carregar(CatalogosFicticios.Eletrodutos));

    public static readonly Dictionary<TipoDeCarga, decimal> Fatores = new() { [TipoDeCarga.TUG] = 0.5m };

    private readonly bool _semFaseNoTue;

    public CenarioDeAlimentador(bool semFaseNoTue = false)
    {
        _semFaseNoTue = semFaseNoTue;
        Terminais =
        [
            Terminal(101, "IL-01", "Iluminação", 600m),
            Terminal(102, "TUG-01", "TUG", 1270m),
            Terminal(103, "TUE-01", "TUE", 2200m, 220m, "2F")
        ];
    }

    public List<DadosDoCircuito> Terminais { get; }

    public bool SemAlimentador { get; init; }

    public bool OrigemAlimentada { get; init; }

    public bool AlimentaQuadros { get; init; }

    public string? MemoriaDoQuadro { get; init; }

    public QuadroLido Quadro => new(1, "QD1",
    [
        new CircuitoLido(101, "IL-01", "Iluminação", 600m, "F+N", 127m, ["A"]),
        new CircuitoLido(102, "TUG-01", "TUG", 1270m, "F+N", 127m, ["B"]),
        new CircuitoLido(103, "TUE-01", "TUE", 2200m, "2F", 220m, _semFaseNoTue ? null : ["B", "C"])
    ], new AlimentacaoDoQuadro("3F+N", 220m, "teste", ["A", "B", "C"]));

    public static PerfilNormativo ComQuedaTotal(decimal total) =>
        PerfilNormativo.Carregar(PerfilFicticio.Json.Replace("\"valores\": { \"circuito_terminal\": 5 }",
            $"\"valores\": {{ \"circuito_terminal\": 5, \"ponto_de_entrega\": {total} }}"));

    public IReadOnlyList<ResultadoDoAlimentador> Executar() => DimensionamentoDeAlimentadores.Executar(Origem, Perfil, Catalogos, Documento, Quadros);

    public DocumentoFalso Documento => _documento ??= new DocumentoFalso(this);

    public QuadrosFalsos Quadros => _quadros ??= new QuadrosFalsos(this);

    private DocumentoFalso? _documento;
    private QuadrosFalsos? _quadros;

    private static DadosDoCircuito Terminal(long id, string numero, string tipo, decimal potenciaVA, decimal tensaoV = 127m, string fases = "F+N") =>
        new(id, numero, tipo, 10m, null, null, [new DadosDoPonto(id * 10, potenciaVA, tensaoV, fases, "LOCAL-SECO", tipo)], Quadro: "QD1");

    public sealed class DocumentoFalso(CenarioDeAlimentador cenario) : IDocumentoDeAlimentadores
    {
        public List<string> Chamadas { get; } = [];

        public CondicoesDoProjeto? Condicoes { get; set; } = CenarioDeAlimentador.Condicoes;

        public void EmUmaTransacao(string nome, Action acao)
        {
            Chamadas.Add($"transacao:{nome}");
            acao();
        }

        public IReadOnlyList<QuadroComAlimentador> LerAlimentadores() =>
        [
            new(1, "QD1",
                cenario.SemAlimentador ? null : new DadosDoAlimentador(900, 30m, null, null, null),
                "QGBT", cenario.OrigemAlimentada, cenario.AlimentaQuadros)
        ];

        public CondicoesDoProjeto? LerCondicoes() => Condicoes;

        public IReadOnlyDictionary<long, CondicoesDoProjeto> LerCondicoesDosCircuitos(IReadOnlyCollection<long> ids) => new Dictionary<long, CondicoesDoProjeto>();

        public IReadOnlyList<DadosDoCircuito> LerCircuitos(IReadOnlyCollection<long> ids) => cenario.Terminais.Where(circuito => ids.Contains(circuito.Id)).ToList();

        public void GravarResultados(IReadOnlyList<ResultadoDoCircuito> resultados) =>
            Chamadas.Add($"gravar:{string.Join(",", resultados.Select(resultado => resultado.Id))}");

        public void GravarCondicoes(CondicoesDoProjeto condicoes, IReadOnlyCollection<long> circuitos, bool doProjetoTodo)
        {
            if (doProjetoTodo) throw new InvalidOperationException("alimentador não troca as condições do projeto");
            Chamadas.Add($"condicoes:{string.Join(",", circuitos)}");
        }
    }

    public sealed class QuadrosFalsos(CenarioDeAlimentador cenario) : IDocumentoDeQuadros
    {
        public void EmUmaTransacao(string nome, Action acao) => acao();

        public IReadOnlyList<QuadroLido> LerQuadrosComCircuitos() => [cenario.Quadro];

        public string? LerMemoriaDoQuadro(long quadroId) =>
            cenario.MemoriaDoQuadro ?? QuadroDeCargasDoProjeto.Montar(cenario.Quadro, Perfil, Fatores).Quadro.Memoria!.Hash();

        public IReadOnlyDictionary<TipoDeCarga, decimal>? LerFatoresDoQuadro(long quadroId) => Fatores;

        public void GravarLinhas(IReadOnlyList<LinhaParaGravar> linhas) => throw new NotSupportedException();

        public bool GravarMemoriaDoQuadro(long quadroId, string? hashDaMemoria, IReadOnlyDictionary<TipoDeCarga, decimal>? fatoresInformados) =>
            throw new NotSupportedException();

        public IReadOnlyList<string> ApagarMemoriaDosOutrosQuadros(IReadOnlyCollection<long> montados) => throw new NotSupportedException();

        public IReadOnlyList<CircuitoLido> LerCircuitosSemQuadro() => throw new NotSupportedException();

        public string CriarTabelaDoQuadro(string nomeDoQuadro) => throw new NotSupportedException();
    }
}
