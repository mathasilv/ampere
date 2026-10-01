using System.Runtime.CompilerServices;
using System.Text;
using Ampere.Core.Catalogos;
using Ampere.Core.Diagramas;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Normas;
using Ampere.Tests.Core.Catalogos;
using Ampere.Tests.Core.Normas;

namespace Ampere.Tests.Core.Diagramas;

/// <summary>
///     Diagrama unifilar: geometria do desenho e golden files em SVG (Relatorios/Referencias, LF forçado), regenerados com
///     AMPERE_ATUALIZAR_REFERENCIAS=1 como os da memória.
/// </summary>
public class DiagramaUnifilar_Teste
{
    private const string VariavelDeAtualizacao = "AMPERE_ATUALIZAR_REFERENCIAS";
    private static readonly PerfilNormativo Ficticio = PerfilNormativo.Carregar(PerfilFicticio.Json);
    private static readonly CatalogosDeProduto Catalogos = new(
        CatalogoDeCondutores.Carregar(CatalogosFicticios.Condutores), CatalogoDeEletrodutos.Carregar(CatalogosFicticios.Eletrodutos));

    private static readonly QuadroDoUnifilar Qd1 = new("QD1",
    [
        new CircuitoDoUnifilar("IL-01", "Iluminação sala e quartos", "Iluminação", "F+N", 127m, 600m, 10m, 1.5m, null, null, 0.8123m),
        new CircuitoDoUnifilar("TUG-01", "Tomadas cozinha", "TUG", "F+N", 127m, 1800m, 20m, 2.5m, 25m, 30m, 2.2134m),
        new CircuitoDoUnifilar("TUE-01", "Chuveiro", "TUE", "2F", 220m, 5978.26m, 32m, 6m, 40m, 30m, 1.9m),
        new CircuitoDoUnifilar("TUE-02", "Ar & <split>", "TUE", "2F", 220m, null, null, null, null, null, null)
    ]);

    [Test]
    public async Task Uma_derivacao_por_circuito_com_disjuntor_IDR_secao_e_identificacao()
    {
        var textos = Textos(DiagramaUnifilar.Montar(Qd1));

        await Assert.That(textos).Contains("QD1 — diagrama unifilar");
        await Assert.That(textos).Contains("4 circuitos · potência instalada incompleta (circuito sem potência)");
        await Assert.That(textos).Contains("20 A");
        await Assert.That(textos).Contains("IDR 25 A");
        await Assert.That(textos).Contains("30 mA");
        await Assert.That(textos).Contains("2,5 mm²");
        await Assert.That(textos).Contains("ΔV 2,21%");
        await Assert.That(textos).Contains("TUG-01 — Tomadas cozinha — TUG — F+N 127 V — 1800 VA");
    }

    [Test]
    public async Task Alimentador_e_fases_aparecem_quando_o_modelo_os_tem()
    {
        var quadro = Qd1 with
        {
            Circuitos = [Qd1.Circuitos[0] with { FasesNoQuadro = ["A"] }, Qd1.Circuitos[2] with { FasesNoQuadro = ["B", "C"] }],
            Alimentacao = "3F+N 220/127 V",
            Alimentador = new AlimentadorDoUnifilar("QGBT", 40m, 10m, null, null, 1.23456m)
        };

        var textos = Textos(DiagramaUnifilar.Montar(quadro));

        await Assert.That(textos).Contains("alimentador (de QGBT) — 40 A — 10 mm² — ΔV 1,23% — 3F+N 220/127 V");
        await Assert.That(textos).Contains("IL-01 — Iluminação sala e quartos — Iluminação — F+N 127 V — 600 VA — fase A");
        await Assert.That(textos).Contains("TUE-01 — Chuveiro — TUE — 2F 220 V — 5978,26 VA — fase B-C");
    }

    [Test]
    public async Task Alimentador_nao_dimensionado_aparece_sem_valor_presumido()
    {
        var textos = Textos(DiagramaUnifilar.Montar(Qd1 with { Alimentador = new AlimentadorDoUnifilar(null, null, null, null, null, null) }));

        await Assert.That(textos).Contains("alimentador — não dimensionado");
    }

    [Test]
    public async Task Circuito_sem_dimensionamento_aparece_como_nao_dimensionado_sem_valor_presumido()
    {
        var textos = Textos(DiagramaUnifilar.Montar(Qd1));

        await Assert.That(textos).Contains("não dimensionado");
        await Assert.That(textos).Contains("—");
        await Assert.That(textos).Contains("TUE-02 — Ar & <split> — TUE — 2F 220 V");
    }

    [Test]
    public async Task Barramento_vai_da_alimentacao_ate_abaixo_da_ultima_derivacao()
    {
        var desenho = DiagramaUnifilar.Montar(Qd1);

        var barramento = desenho.Elementos.OfType<Segmento>().Single(segmento => segmento.Grosso);
        var derivacoes = desenho.Elementos.OfType<Segmento>().Where(segmento => segmento.X1 == 20m && !segmento.Grosso && segmento.Y1 == segmento.Y2).ToList();
        await Assert.That(derivacoes.Count).IsEqualTo(4);
        await Assert.That(barramento.Y2).IsLessThan(derivacoes.Min(segmento => segmento.Y1));
        await Assert.That(desenho.Altura).IsGreaterThan(-barramento.Y2);
    }

    [Test]
    public async Task Mesmo_quadro_da_o_mesmo_desenho()
    {
        await Assert.That(DiagramaUnifilar.Svg(DiagramaUnifilar.Montar(Qd1))).IsEqualTo(DiagramaUnifilar.Svg(DiagramaUnifilar.Montar(Qd1)));
    }

    [Test]
    public async Task Texto_do_SVG_escapa_caracteres_de_XML()
    {
        var svg = DiagramaUnifilar.Svg(DiagramaUnifilar.Montar(Qd1));

        await Assert.That(svg).Contains("Ar &amp; &lt;split&gt;");
        await Assert.That(svg).DoesNotContain("<split>");
    }

    [Test]
    [Arguments("unifilar-qd1")]
    [Arguments("unifilar-vazio")]
    public async Task SVG_igual_ao_de_referencia(string cenario)
    {
        var quadro = cenario == "unifilar-vazio" ? new QuadroDoUnifilar("QD2", []) : Qd1;

        await Conferir($"{cenario}.svg", DiagramaUnifilar.Svg(DiagramaUnifilar.Montar(quadro)));
    }

    [Test]
    public async Task Um_diagrama_por_quadro_numa_transacao()
    {
        var documento = new DocumentoFalso([Qd1, new QuadroDoUnifilar("QD2", [])]);

        var desenhados = DiagramasDoProjeto.Desenhar(documento, Ficticio, Catalogos);

        await Assert.That(desenhados.Select(desenhado => desenhado.Vista)).IsEquivalentTo(["QD1 (vista)", "QD2 (vista)"]);
        await Assert.That(documento.Chamadas).IsEquivalentTo(["transacao", "QD1", "QD2"]);
    }

    [Test]
    public async Task Sem_quadros_nada_e_desenhado()
    {
        var documento = new DocumentoFalso([]);

        await Assert.That(DiagramasDoProjeto.Desenhar(documento, Ficticio, Catalogos)).IsEmpty();
        await Assert.That(documento.Chamadas).IsEmpty();
    }

    [Test]
    public async Task Neutro_e_protecao_so_quando_a_memoria_de_hoje_confere_com_a_gravada()
    {
        // TUG-01 de 7620 VA em 127 V: fase de 16 mm² e, no perfil fictício, PE de 10 mm².
        var dados = new DadosDoCircuito(7, "TUG-01", "TUG", 10m, "B1", "PVC", [new DadosDoPonto(70, 7620m, 127m, "F+N", "LOCAL-SECO", "TUG")], Quadro: "QD1");
        var condicoes = new CondicoesDoProjeto(30m, 1, "Cobre", TipoDeCondutorPadrao: CatalogosFicticios.TipoDeCondutor, TipoDeEletroduto: CatalogosFicticios.TipoDeEletroduto);
        var gravada = DimensionamentoDoProjeto.Calcular(dados, condicoes, Ficticio, Catalogos).Memoria!.Hash();
        CircuitoDoUnifilar Circuito(string? memoria) =>
            new("TUG-01", null, "TUG", "F+N", 127m, 7620m, 63m, 16m, null, null, 1.2m, Id: 7, MemoriaGravada: memoria);

        var confere = new DocumentoFalso([new QuadroDoUnifilar("QD1", [Circuito(gravada)])], [dados], condicoes);
        var mudou = new DocumentoFalso([new QuadroDoUnifilar("QD1", [Circuito("sha256:outra")])], [dados], condicoes);

        await Assert.That(Textos(DiagramasDoProjeto.Desenhar(confere, Ficticio, Catalogos)[0].Desenho)).Contains("16 mm² (N 16 · PE 10)");
        await Assert.That(Textos(DiagramasDoProjeto.Desenhar(mudou, Ficticio, Catalogos)[0].Desenho)).Contains("16 mm²");
    }

    private sealed class DocumentoFalso(
        IReadOnlyList<QuadroDoUnifilar> quadros, IReadOnlyList<DadosDoCircuito>? circuitos = null, CondicoesDoProjeto? condicoes = null) : IDocumentoDeDiagramas
    {
        public List<string> Chamadas { get; } = [];

        public IReadOnlyList<DadosDoCircuito> LerCircuitos(IReadOnlyCollection<long> ids) => (circuitos ?? []).Where(circuito => ids.Contains(circuito.Id)).ToList();

        public CondicoesDoProjeto? LerCondicoes() => condicoes;

        public IReadOnlyDictionary<long, CondicoesDoProjeto> LerCondicoesDosCircuitos(IReadOnlyCollection<long> ids) => new Dictionary<long, CondicoesDoProjeto>();

        public void EmUmaTransacao(string nome, Action acao)
        {
            Chamadas.Add("transacao");
            acao();
        }

        public IReadOnlyList<QuadroDoUnifilar> LerQuadros() => quadros;

        public string DesenharUnifilar(string nomeDoQuadro, DesenhoDoUnifilar desenho)
        {
            Chamadas.Add(nomeDoQuadro);
            return $"{nomeDoQuadro} (vista)";
        }
    }

    private static List<string> Textos(DesenhoDoUnifilar desenho) => desenho.Elementos.OfType<Texto>().Select(texto => texto.Conteudo).ToList();

    private static async Task Conferir(string arquivo, string atual)
    {
        var caminho = Path.Combine(PastaDasReferencias(), arquivo);
        if (Environment.GetEnvironmentVariable(VariavelDeAtualizacao) == "1")
        {
            await File.WriteAllTextAsync(caminho, atual, new UTF8Encoding(false));
            return;
        }

        await Assert.That(File.Exists(caminho)).IsTrue()
            .Because($"referência {arquivo} ausente: rode os testes com {VariavelDeAtualizacao}=1 e revise o arquivo gerado");
        await Assert.That(atual).IsEqualTo(await File.ReadAllTextAsync(caminho, new UTF8Encoding(false)))
            .Because($"{arquivo} mudou: se a mudança tem justificativa, regenere com {VariavelDeAtualizacao}=1 e revise o diff");
    }

    private static string PastaDasReferencias([CallerFilePath] string arquivoDoTeste = "")
    {
        var pasta = Path.Combine(Path.GetDirectoryName(arquivoDoTeste)!, "..", "Relatorios", "Referencias");
        if (!Directory.Exists(pasta)) throw new DirectoryNotFoundException($"Pasta de referências não encontrada: {pasta}");
        return Path.GetFullPath(pasta);
    }
}
