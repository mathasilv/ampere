using Ampere.Core.Catalogos;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Normas;
using Ampere.Core.Relatorios;
using Ampere.Tests.Core.Catalogos;
using Ampere.Tests.Core.Normas;

namespace Ampere.Tests.Core.Relatorios;

/// <summary>Lista de materiais a partir da rodada de dimensionamento (perfil e catálogos fictícios).</summary>
public class ListaDeMateriais_Teste
{
    private static readonly PerfilNormativo Ficticio = PerfilNormativo.Carregar(PerfilFicticio.Json);
    private static readonly CondicoesDoProjeto Condicoes = new(30m, 1, "Cobre", "B1", "PVC", CatalogosFicticios.TipoDeCondutor, CatalogosFicticios.TipoDeEletroduto);

    private static readonly CatalogosDeProduto Catalogos = new(
        CatalogoDeCondutores.Carregar(CatalogosFicticios.Condutores),
        CatalogoDeEletrodutos.Carregar(CatalogosFicticios.Eletrodutos));

    // TUG-01: 2,5 mm², 10 A, sem IDR, 10 m. TUG-02: 2,5 mm², 10 A, IDR 25 A/30 mA, 20 m. TUE-01 (3F+N): 4 mm², 20 A, 15 m.
    private static readonly DadosDoCircuito[] TresCircuitos =
    [
        Circuito(1, "TUG-01", "TUG", 10m, "LOCAL-SECO", 1270m),
        Circuito(2, "TUG-02", "TUG", 20m, "LOCAL-MOLHADO", 1270m),
        Circuito(3, "TUE-01", "TUE", 15m, "LOCAL-SECO", 6600m, 220m, "3F+N")
    ];

    [Test]
    public async Task Condutores_em_metros_por_secao_e_funcao_e_protecoes_por_polos_e_corrente()
    {
        var lista = Montar(TresCircuitos);

        await Assert.That(string.Join("\n", lista.Itens.Select(item => $"{item.Grupo}|{item.Item}|{item.Quantidade:0.###}|{item.Unidade}|{string.Join(", ", item.Circuitos)}")))
            .IsEqualTo(string.Join("\n",
                "Condutores|FIO-TESTE (Cobre, PVC) 2,5 mm² — fase|30|m|QD1-TUG-01, QD1-TUG-02",
                "Condutores|FIO-TESTE (Cobre, PVC) 2,5 mm² — neutro|30|m|QD1-TUG-01, QD1-TUG-02",
                "Condutores|FIO-TESTE (Cobre, PVC) 2,5 mm² — proteção (PE)|30|m|QD1-TUG-01, QD1-TUG-02",
                "Condutores|FIO-TESTE (Cobre, PVC) 4 mm² — fase|45|m|QD1-TUE-01",
                "Condutores|FIO-TESTE (Cobre, PVC) 4 mm² — neutro|15|m|QD1-TUE-01",
                "Condutores|FIO-TESTE (Cobre, PVC) 4 mm² — proteção (PE)|15|m|QD1-TUE-01",
                "Disjuntores|Disjuntor 1P 10 A|2|un|QD1-TUG-01, QD1-TUG-02",
                "Disjuntores|Disjuntor 3P 20 A|1|un|QD1-TUE-01",
                "IDR|IDR 2P 25 A, IΔn 30 mA|1|un|QD1-TUG-02"));
        await Assert.That(lista.ForaDaLista).IsEmpty();
    }

    [Test]
    public async Task Neutro_e_protecao_com_as_secoes_da_memoria()
    {
        // 7620 VA / 127 V = 60 A: disjuntor 63 A, fase 16 mm²; no perfil fictício, o PE de 16 mm² é 10 mm².
        var lista = Montar(Circuito(1, "TUE-03", "TUE", 10m, "LOCAL-SECO", 7620m));

        await Assert.That(string.Join("|", lista.Itens.Where(item => item.Grupo == ListaDeMateriais.Condutores).Select(item => item.Item))).IsEqualTo(
            "FIO-TESTE (Cobre, PVC) 10 mm² — proteção (PE)|FIO-TESTE (Cobre, PVC) 16 mm² — fase|FIO-TESTE (Cobre, PVC) 16 mm² — neutro");
        await Assert.That(lista.Itens[0].Observacao).IsEqualTo("comprimento do circuito, sem sobras nem emendas; seção pela tabela do condutor de proteção");
    }

    [Test]
    public async Task Bifasico_sem_neutro_nao_leva_neutro()
    {
        var lista = Montar(Circuito(1, "TUE-02", "TUE", 10m, "LOCAL-SECO", 2200m, 220m, "2F"));

        await Assert.That(string.Join("|", lista.Itens.Select(item => item.Item))).IsEqualTo(
            "FIO-TESTE (Cobre, PVC) 2,5 mm² — fase|FIO-TESTE (Cobre, PVC) 2,5 mm² — proteção (PE)|Disjuntor 2P 10 A");
        await Assert.That(lista.Itens[0].Quantidade).IsEqualTo(20m);
    }

    [Test]
    public async Task Circuito_sem_protecao_decidida_ou_sem_dados_fica_fora_com_o_motivo()
    {
        var semLocal = Circuito(1, "TUG-01", "TUG", 10m, null, 1270m);
        var semNumero = Circuito(2, "TUG-02", "TUG", 10m, "LOCAL-SECO", 1270m) with { Numero = null };

        var lista = Montar(semLocal, semNumero);

        await Assert.That(lista.Itens).IsEmpty();
        await Assert.That(lista.ForaDaLista.Select(fora => fora.Circuito)).IsEquivalentTo(["QD1-TUG-01", "QD1-circuito 2"]);
        await Assert.That(lista.ForaDaLista[0].Motivo).StartsWith("proteção não decidida: ").And.Contains("sem local");
        await Assert.That(lista.ForaDaLista[1].Motivo).StartsWith("dados faltando: ").And.Contains("sem AMP_NumeroCircuito");
    }

    [Test]
    public async Task Entrada_invalida_fica_fora_com_o_motivo_dela()
    {
        var lista = Montar(Circuito(1, "TUG-01", "TUG", -5m, "LOCAL-SECO", 1270m));

        await Assert.That(lista.ForaDaLista.Single().Motivo).IsEqualTo("entrada inválida: comprimento deve ser positivo");
    }

    [Test]
    public async Task Isolacao_diferente_e_outro_item()
    {
        var comEpr = PerfilNormativo.Carregar(PerfilFicticio.Json
            .Replace("{ \"isolacao\": \"PVC\", \"por_temperatura_c\": { \"30\": 1, \"40\": 0.8 } }",
                "{ \"isolacao\": \"PVC\", \"por_temperatura_c\": { \"30\": 1, \"40\": 0.8 } }, { \"isolacao\": \"EPR\", \"por_temperatura_c\": { \"30\": 1 } }")
            .Replace("{ \"metodo\": \"B1\", \"isolacao\": \"PVC\", \"material\": \"Cobre\", \"condutores_carregados\": 2,",
                "{ \"metodo\": \"B1\", \"isolacao\": \"EPR\", \"material\": \"Cobre\", \"condutores_carregados\": 2, \"por_secao_mm2\": { \"2.5\": 20 } }, " +
                "{ \"metodo\": \"B1\", \"isolacao\": \"PVC\", \"material\": \"Cobre\", \"condutores_carregados\": 2,"));
        DadosDoCircuito[] circuitos = [Circuito(1, "TUG-01", "TUG", 10m, "LOCAL-SECO", 1270m), Circuito(2, "TUG-02", "TUG", 10m, "LOCAL-SECO", 1270m) with { Isolacao = "EPR" }];

        var lista = ListaDeMateriais.Montar(DimensionamentoDoProjeto.Executar([1, 2], Condicoes, comEpr, Catalogos, new DocumentoFalso(circuitos)));

        await Assert.That(string.Join("|", lista.Itens.Where(item => item.Grupo == ListaDeMateriais.Condutores && item.Item.EndsWith("fase")).Select(item => item.Item)))
            .IsEqualTo("FIO-TESTE (Cobre, EPR) 2,5 mm² — fase|FIO-TESTE (Cobre, PVC) 2,5 mm² — fase");
    }

    [Test]
    public async Task Csv_com_cabecalho_itens_e_circuitos_fora_da_lista_no_fim()
    {
        var semLocal = Circuito(4, "TUG-03", "TUG", 10m, null, 1270m);

        var linhas = Montar([.. TresCircuitos, semLocal]).Csv().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        await Assert.That(linhas[0]).IsEqualTo("Grupo;Item;Quantidade;Unidade;Circuitos;Observação");
        await Assert.That(linhas[1]).IsEqualTo("Condutores;FIO-TESTE (Cobre, PVC) 2,5 mm² — fase;30;m;QD1-TUG-01 | QD1-TUG-02;comprimento do circuito × fases, sem sobras nem emendas");
        await Assert.That(linhas[7]).IsEqualTo("Disjuntores;Disjuntor 1P 10 A;2;un;QD1-TUG-01 | QD1-TUG-02;curva e capacidade de interrupção a definir");
        await Assert.That(linhas[^1]).StartsWith("Fora da lista;QD1-TUG-03;;;;proteção não decidida: ");
        await Assert.That(linhas.Length).IsEqualTo(11);
    }

    [Test]
    public async Task Comprimentos_somados_como_entraram_no_calculo()
    {
        var lista = Montar(Circuito(1, "TUG-01", "TUG", 10.123m, "LOCAL-SECO", 1270m), Circuito(2, "TUG-02", "TUG", 5.0006m, "LOCAL-SECO", 1270m));

        await Assert.That(lista.Itens[0].Quantidade).IsEqualTo(15.124m); // 5,0006 m entra no cálculo arredondado ao milímetro
        await Assert.That(lista.Csv()).Contains(";15,124;m;");
    }

    private static ListaDeMateriais Montar(params DadosDoCircuito[] circuitos) =>
        ListaDeMateriais.Montar(DimensionamentoDoProjeto.Executar(circuitos.Select(circuito => circuito.Id).ToList(), Condicoes, Ficticio, Catalogos,
            new DocumentoFalso(circuitos)));

    private static DadosDoCircuito Circuito(long id, string numero, string tipo, decimal comprimentoM, string? local, decimal potenciaVA,
        decimal tensaoV = 127m, string fases = "F+N") =>
        new(id, numero, tipo, comprimentoM, null, null, [new DadosDoPonto(id * 10, potenciaVA, tensaoV, fases, local, tipo)], Quadro: "QD1");

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
