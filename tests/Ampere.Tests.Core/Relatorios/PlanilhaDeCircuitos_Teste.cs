using Ampere.Core.Catalogos;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Normas;
using Ampere.Core.Relatorios;
using Ampere.Tests.Core.Catalogos;
using Ampere.Tests.Core.Normas;

namespace Ampere.Tests.Core.Relatorios;

/// <summary>Planilha CSV dos circuitos dimensionados (perfil e catálogos fictícios).</summary>
public class PlanilhaDeCircuitos_Teste
{
    private static readonly PerfilNormativo Ficticio = PerfilNormativo.Carregar(PerfilFicticio.Json);
    private static readonly CondicoesDoProjeto Condicoes = new(30m, 1, "Cobre", "B1", "PVC", CatalogosFicticios.TipoDeCondutor, CatalogosFicticios.TipoDeEletroduto);

    private static readonly CatalogosDeProduto Catalogos = new(
        CatalogoDeCondutores.Carregar(CatalogosFicticios.Condutores),
        CatalogoDeEletrodutos.Carregar(CatalogosFicticios.Eletrodutos));

    [Test]
    public async Task Cabecalho_e_uma_linha_por_circuito_com_virgula_decimal()
    {
        var linhas = Linhas(Circuito(1, "TUG-01", "TUG", "LOCAL-SECO"));

        await Assert.That(linhas.Length).IsEqualTo(2);
        await Assert.That(linhas[0]).IsEqualTo(
            "Quadro;Circuito;Tipo de carga;Situação;Potência (VA);IB (A);FCT;FCA;Seção (mm²);Neutro (mm²);PE (mm²);IZ (A);Disjuntor (A);Icn mín. (kA);k²S² (A²s);" +
            "IDR In (A);IDR IΔn (mA);" +
            "Queda de tensão (%);Eletroduto;Ocupação (%);Motivo;Avisos;Memória");
        var campos = linhas[1].Split(';');
        await Assert.That(campos[..16]).IsEquivalentTo(["QD1", "TUG-01", "TUG", "Dimensionado", "1270", "10", "1", "1", "2,5", "2,5", "2,5", "20", "10", "", "", ""]);
        await Assert.That(campos[17]).IsEqualTo("1,2598");
        await Assert.That(campos[22]).StartsWith("sha256:");
    }

    [Test]
    public async Task Sem_IDR_decidido_as_colunas_de_protecao_ficam_vazias_como_no_modelo()
    {
        var campos = Linhas(Circuito(1, "TUG-01", "TUG", null))[1].Split(';');

        await Assert.That(campos[3]).IsEqualTo("Interrompido antes da proteção");
        await Assert.That(campos[5]).IsEqualTo("10");
        await Assert.That(campos[8..20].All(campo => campo.Length == 0)).IsTrue();
        await Assert.That(campos[20]).Contains("sem local");
    }

    [Test]
    public async Task Circuito_sem_dados_sai_com_o_motivo_e_sem_memoria()
    {
        var semNumero = Circuito(1, "TUG-01", "TUG", "LOCAL-SECO") with { Numero = null };

        var campos = Linhas(semNumero)[1].Split(';');

        await Assert.That(campos[3]).IsEqualTo("Não calculado (dados faltando)");
        await Assert.That(campos[20]).Contains("sem AMP_NumeroCircuito");
        await Assert.That(campos[22]).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Campo_com_ponto_e_virgula_ou_aspas_vai_entre_aspas()
    {
        var circuito = Circuito(1, "TUG-01", "TUG", "LOCAL-SECO") with { Quadro = "QD \"A\"; térreo" };

        var linha = Linhas(circuito)[1];

        await Assert.That(linha).StartsWith("\"QD \"\"A\"\"; térreo\";TUG-01;");
    }

    private static string[] Linhas(params DadosDoCircuito[] circuitos)
    {
        var resultados = DimensionamentoDoProjeto.Executar(circuitos.Select(circuito => circuito.Id).ToList(), Condicoes, Ficticio, Catalogos,
            new DocumentoFalso(circuitos));
        var csv = PlanilhaDeCircuitos.Csv(resultados);
        return csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
    }

    private static DadosDoCircuito Circuito(long id, string numero, string tipo, string? local) =>
        new(id, numero, tipo, 10m, null, null, [new DadosDoPonto(id * 10, 1270m, 127m, "F+N", local, tipo)], Quadro: "QD1");

    private sealed class DocumentoFalso(IReadOnlyList<DadosDoCircuito> circuitos) : IDocumentoDeDimensionamento
    {
        public void EmUmaTransacao(string nome, Action acao) => acao();

        public IReadOnlyList<DadosDoCircuito> LerCircuitos(IReadOnlyCollection<long> ids) => circuitos;

        public void GravarResultados(IReadOnlyList<ResultadoDoCircuito> resultados)
        {
        }

        public void GravarCondicoes(CondicoesDoProjeto condicoes, IReadOnlyCollection<long> circuitos, bool doProjetoTodo)
        {
        }
    }
}
