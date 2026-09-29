using Ampere.Core.Cargas;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Normas;
using Ampere.Tests.Core.Normas;

namespace Ampere.Tests.Core.Dimensionamento;

/// <summary>
///     Motor de dimensionamento contra o perfil FICTÍCIO (números redondos, não são da NBR 5410). Os resultados
///     esperados foram calculados à mão a partir desse perfil.
/// </summary>
public class DimensionamentoDeCircuito_Teste
{
    private static readonly PerfilNormativo Ficticio = PerfilNormativo.Carregar(PerfilFicticio.Json);

    [Test]
    [Arguments("F+N", 1270, 127, 10)]
    [Arguments("2F", 4400, 220, 20)]
    [Arguments("3F", 6600, 220, 17.320508)]
    [Arguments("3F+N", 6600, 220, 17.320508)]
    public async Task Corrente_de_projeto_por_configuracao(string fases, decimal potenciaVA, decimal tensaoV, decimal esperadaA)
    {
        var resultado = Dimensionar(Entrada(potenciaVA, fases, tensaoV));

        await Assert.That(Math.Round(resultado.CorrenteDeProjetoA!.Value, 6)).IsEqualTo(esperadaA);
    }

    [Test]
    public async Task Circuito_simples_dimensionado_de_ponta_a_ponta()
    {
        var resultado = Dimensionar(Entrada());

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Dimensionado);
        await Assert.That(resultado.CorrenteDeProjetoA).IsEqualTo(10m);
        await Assert.That(resultado.SecaoMm2).IsEqualTo(2.5m);
        await Assert.That(resultado.CapacidadeDeConducaoA).IsEqualTo(20m);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(10m);
        await Assert.That(Math.Round(resultado.QuedaDeTensaoPct!.Value, 4)).IsEqualTo(1.2598m);
        await Assert.That(resultado.PerfilNorma).IsEqualTo("FICTICIO-TESTE");
    }

    [Test]
    public async Task Fatores_de_temperatura_e_agrupamento_reduzem_a_capacidade()
    {
        var resultado = Dimensionar(Entrada(potenciaVA: 1905m, circuitosAgrupados: 3));

        await Assert.That(resultado.FCA).IsEqualTo(0.7m);
        await Assert.That(resultado.FCT).IsEqualTo(1m);
        await Assert.That(resultado.SecaoMm2).IsEqualTo(4m);
        await Assert.That(resultado.CapacidadeDeConducaoA).IsEqualTo(21m);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(16m);
    }

    [Test]
    public async Task Coordenacao_com_o_disjuntor_eleva_a_secao_e_explica()
    {
        var resultado = Dimensionar(Entrada(potenciaVA: 1524m, temperaturaC: 40m, circuitosAgrupados: 2));

        await Assert.That(resultado.SecaoMm2).IsEqualTo(4m);
        await Assert.That(resultado.CapacidadeDeConducaoA).IsEqualTo(19.2m);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(16m);
        await Assert.That(Observacoes(resultado)).Contains("seção elevada de 2.5 para 4 mm²: nenhum disjuntor entre IB = 12 A e IZ = 12.8 A");
    }

    [Test]
    public async Task Queda_de_tensao_eleva_a_secao_e_explica()
    {
        var resultado = Dimensionar(Entrada(comprimentoM: 60m));

        await Assert.That(resultado.SecaoMm2).IsEqualTo(4m);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(10m);
        await Assert.That(Math.Round(resultado.QuedaDeTensaoPct!.Value, 4)).IsEqualTo(4.7244m);
        await Assert.That(Observacoes(resultado)).Contains("seção elevada de 2.5 para 4 mm²: queda de tensão 7.5591% acima do limite de 5%");
    }

    [Test]
    public async Task Iluminacao_usa_a_secao_minima_de_iluminacao()
    {
        var resultado = Dimensionar(Entrada(potenciaVA: 127m, tipo: TipoDeCarga.Iluminacao));

        await Assert.That(resultado.SecaoMm2).IsEqualTo(1.5m);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(10m);
    }

    [Test]
    public async Task Toda_linha_da_memoria_cita_a_referencia_do_perfil_nunca_do_codigo()
    {
        var memoria = Dimensionar(Entrada(comprimentoM: 60m)).Memoria!;

        await Assert.That(memoria.Passos.Count).IsGreaterThan(5);
        await Assert.That(memoria.Passos.Select(passo => passo.Referencia).Where(referencia => !referencia.StartsWith("FICTÍCIO"))).IsEmpty();
    }

    [Test]
    public async Task Mesma_entrada_gera_a_mesma_memoria_e_o_mesmo_hash()
    {
        await Assert.That(Dimensionar(Entrada()).Memoria!.Hash()).IsEqualTo(Dimensionar(Entrada()).Memoria!.Hash());
    }

    [Test]
    public async Task Perfil_oficial_calcula_IB_e_para_na_primeira_tabela_sem_dados()
    {
        var resultado = DimensionamentoDeCircuito.Dimensionar(Entrada(), PerfilNormativo.NBR5410_2004);

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Interrompido);
        await Assert.That(resultado.CorrenteDeProjetoA).IsEqualTo(10m);
        await Assert.That(resultado.SecaoMm2).IsNull();

        var ultimo = resultado.Memoria!.Passos[^1];
        await Assert.That(ultimo.Resultado).IsNull();
        await Assert.That(ultimo.Referencia).IsEqualTo("TODO_NORMA");
        await Assert.That(ultimo.Observacao).Contains("condutores_carregados sem dados oficiais (TODO_NORMA)");
        await Assert.That(string.Join("\n", resultado.Problemas)).Contains("condutores_carregados sem dados oficiais (TODO_NORMA)");
    }

    [Test]
    public async Task Configuracao_2F_mais_N_para_com_explicacao()
    {
        var resultado = Dimensionar(Entrada(fases: "2F+N"));

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Interrompido);
        await Assert.That(resultado.CorrenteDeProjetoA).IsNull();
        await Assert.That(string.Join("\n", resultado.Problemas)).Contains("2F+N");
    }

    [Test]
    public async Task Corrente_maior_que_qualquer_secao_para_com_explicacao()
    {
        var resultado = Dimensionar(Entrada(potenciaVA: 25400m));

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Interrompido);
        await Assert.That(string.Join("\n", resultado.Problemas)).Contains("nenhuma seção do perfil atende IB = 200 A");
    }

    [Test]
    public async Task Entrada_invalida_nao_gera_memoria()
    {
        var resultado = Dimensionar(Entrada(potenciaVA: -1m, tensaoV: 0m, tipo: TipoDeCarga.Reserva, circuitosAgrupados: 0));

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.EntradaInvalida);
        await Assert.That(resultado.Memoria).IsNull();
        await Assert.That(resultado.Problemas.Count).IsEqualTo(4);
    }

    private static ResultadoDoDimensionamento Dimensionar(EntradaDeDimensionamento entrada) =>
        DimensionamentoDeCircuito.Dimensionar(entrada, Ficticio);

    private static EntradaDeDimensionamento Entrada(
        decimal potenciaVA = 1270m,
        string fases = "F+N",
        decimal tensaoV = 127m,
        decimal comprimentoM = 10m,
        TipoDeCarga tipo = TipoDeCarga.TUG,
        decimal temperaturaC = 30m,
        int circuitosAgrupados = 1) =>
        new("TUG-01", tipo, potenciaVA, fases, tensaoV, comprimentoM, "B1", "PVC", "Cobre", temperaturaC, circuitosAgrupados);

    private static string Observacoes(ResultadoDoDimensionamento resultado) =>
        string.Join("\n", resultado.Memoria!.Passos.Select(passo => passo.Observacao).OfType<string>());
}
