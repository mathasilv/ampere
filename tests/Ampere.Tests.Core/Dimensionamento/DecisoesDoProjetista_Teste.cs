using Ampere.Core.Catalogos;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Normas;
using Ampere.Tests.Core.Catalogos;
using Ampere.Tests.Core.Normas;

namespace Ampere.Tests.Core.Dimensionamento;

/// <summary>
///     Decisões do projetista lidas do circuito (AMP_* de entrada) até o motor: 0 = sem decisão; decisão incoerente é
///     problema de dados, nunca interpretada.
/// </summary>
public class DecisoesDoProjetista_Teste
{
    private static readonly PerfilNormativo Ficticio = PerfilNormativo.Carregar(PerfilFicticio.Json);
    private static readonly CondicoesDoProjeto Condicoes = new(30m, 1, "Cobre", "B1", "PVC", CatalogosFicticios.TipoDeCondutor, CatalogosFicticios.TipoDeEletroduto);

    private static readonly CatalogosDeProduto Catalogos = new(
        CatalogoDeCondutores.Carregar(CatalogosFicticios.Condutores),
        CatalogoDeEletrodutos.Carregar(CatalogosFicticios.Eletrodutos));

    [Test]
    public async Task Zeros_e_vazios_sao_sem_decisao_e_nao_mudam_a_memoria()
    {
        var zerado = Montar(new DecisoesDoProjetista(0m, 0m, " ", 0m, " ", 0m, 0m));
        var semDecisoes = Montar(null);

        await Assert.That(zerado.Problemas).IsEmpty();
        await Assert.That(zerado.Entrada! with { LocaisDosPontos = semDecisoes.Entrada!.LocaisDosPontos }).IsEqualTo(semDecisoes.Entrada);
        await Assert.That(Memoria(zerado).Hash()).IsEqualTo(Memoria(semDecisoes).Hash());
    }

    [Test]
    public async Task Temperatura_e_agrupamento_do_circuito_substituem_os_do_projeto()
    {
        var montada = Montar(new DecisoesDoProjetista(TemperaturaAmbienteC: 40m, CircuitosAgrupados: 2m, Justificativa: "  forro sem ventilação "));

        var entrada = montada.Entrada!;
        await Assert.That(entrada.TemperaturaAmbienteC).IsEqualTo(40m);
        await Assert.That(entrada.CircuitosAgrupados).IsEqualTo(2);
        await Assert.That(entrada.Justificativa).IsEqualTo("forro sem ventilação");
        await Assert.That(entrada.OrigemDaTemperatura).IsEqualTo("AMP_TemperaturaAmbienteC do circuito (o projeto usa 30 °C; justificativa: forro sem ventilação)");
        await Assert.That(entrada.OrigemDoAgrupamento).IsEqualTo("AMP_CircuitosAgrupados do circuito (o projeto usa 1; justificativa: forro sem ventilação)");

        var resultado = DimensionamentoDeCircuito.Dimensionar(entrada, Ficticio, Catalogos);
        await Assert.That(resultado.FCT).IsEqualTo(0.8m);
        await Assert.That(resultado.FCA).IsEqualTo(0.8m);
    }

    [Test]
    [Property("Fonte", "NBR 5410:2004, Tabela 40 (solo)")]
    public async Task Linha_enterrada_usa_a_temperatura_do_solo_do_projeto_ou_a_do_circuito()
    {
        var oficial = PerfilNormativo.NBR5410_2004;
        var dados = new DadosDoCircuito(1, "TUG-01", "TUG", 10m, "D", "PVC", [new DadosDoPonto(11, 1270m, 127m, "F+N", "Demais locais internos", "TUG")]);

        var doProjeto = EntradaDoCircuito.Montar(dados, Condicoes with { TemperaturaDoSoloC = 25m }, oficial).Entrada!;
        var doCircuito = EntradaDoCircuito.Montar(dados with { Decisoes = new DecisoesDoProjetista(TemperaturaAmbienteC: 35m, Justificativa: "solo exposto") },
            Condicoes with { TemperaturaDoSoloC = 25m }, oficial).Entrada!;
        var semSolo = EntradaDoCircuito.Montar(dados, Condicoes, oficial);

        await Assert.That(doProjeto.TemperaturaAmbienteC).IsEqualTo(25m);
        await Assert.That(doProjeto.OrigemDaTemperatura).IsEqualTo("temperatura do solo das condições do projeto (linha enterrada)");
        await Assert.That(doCircuito.TemperaturaAmbienteC).IsEqualTo(35m);
        await Assert.That(doCircuito.OrigemDaTemperatura).IsEqualTo(
            "AMP_TemperaturaAmbienteC do circuito, a do solo na linha enterrada (o projeto usa 25 °C no solo; justificativa: solo exposto)");
        await Assert.That(semSolo.Entrada).IsNull();
        await Assert.That(semSolo.Problemas).IsEquivalentTo(
            ["linha enterrada sem temperatura do solo: informe-a nas condições do projeto ou em AMP_TemperaturaAmbienteC do circuito"]);
        // Fora do solo, a temperatura do solo do projeto não entra.
        await Assert.That(EntradaDoCircuito.Montar(dados with { MetodoDeInstalacao = "B1" }, Condicoes with { TemperaturaDoSoloC = 25m }, oficial).Entrada!.TemperaturaAmbienteC)
            .IsEqualTo(30m);
    }

    [Test]
    public async Task Secao_e_disjuntor_do_projetista_chegam_ao_motor()
    {
        var montada = Montar(new DecisoesDoProjetista(SecaoMinimaMm2: 4m, DisjuntorA: 20m, Justificativa: "padrão da obra"));

        var resultado = DimensionamentoDeCircuito.Dimensionar(montada.Entrada!, Ficticio, Catalogos);

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Dimensionado);
        await Assert.That(resultado.SecaoMm2).IsEqualTo(4m);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(20m);
    }

    [Test]
    [Arguments("Exigir", 30, true)]
    [Arguments(" exigir ", 30, true)]
    [Arguments("Dispensar", 0, false)]
    [Arguments("DISPENSAR", null, false)]
    public async Task Decisao_de_IDR_reconhecida_leva_a_justificativa(string decisao, int? sensibilidadeMa, bool exigir)
    {
        var montada = Montar(new DecisoesDoProjetista(Idr: decisao, IdrSensibilidadeMa: sensibilidadeMa, Justificativa: "motivo"));

        await Assert.That(montada.Problemas).IsEmpty();
        await Assert.That(montada.Entrada!.IdrDoProjetista).IsEqualTo(exigir ? DecisaoDeIdr.Exigido(30m, "motivo") : DecisaoDeIdr.Dispensado("motivo"));
    }

    [Test]
    [Arguments("Exigir", null, "AMP_IDR_DecisaoProjetista = Exigir sem AMP_IDR_SensibilidadeProjetistaMa: informe a IΔn, em mA")]
    [Arguments("Exigir", -30, "AMP_IDR_SensibilidadeProjetistaMa negativa ('-30'; 0 = sem decisão)")]
    [Arguments("Dispensar", 30, "AMP_IDR_DecisaoProjetista = Dispensar com AMP_IDR_SensibilidadeProjetistaMa preenchida: zere a sensibilidade ou exija o IDR")]
    [Arguments("Talvez", null, "AMP_IDR_DecisaoProjetista 'Talvez' desconhecido: use Exigir ou Dispensar (vazio = tabela por local)")]
    [Arguments(null, 30, "AMP_IDR_SensibilidadeProjetistaMa preenchida sem AMP_IDR_DecisaoProjetista = Exigir")]
    public async Task Decisao_de_IDR_incoerente_e_problema_de_dados(string? decisao, int? sensibilidadeMa, string problema)
    {
        var montada = Montar(new DecisoesDoProjetista(Idr: decisao, IdrSensibilidadeMa: sensibilidadeMa));

        await Assert.That(montada.Entrada).IsNull();
        await Assert.That(montada.Problemas).IsEquivalentTo([problema]);
    }

    [Test]
    public async Task Decisoes_numericas_invalidas_sao_todas_relatadas()
    {
        var montada = Montar(new DecisoesDoProjetista(SecaoMinimaMm2: -4m, DisjuntorA: -20m, CircuitosAgrupados: 2.5m));

        await Assert.That(montada.Entrada).IsNull();
        await Assert.That(string.Join("\n", montada.Problemas)).IsEqualTo(string.Join("\n",
            "AMP_SecaoMinimaProjetistaMm2 negativo ('-4'; 0 = sem decisão)",
            "AMP_DisjuntorProjetistaA negativo ('-20'; 0 = sem decisão)",
            "AMP_CircuitosAgrupados deve ser um número inteiro de circuitos, pelo menos 1 ('2,5'; 0 = o do projeto)"));
    }

    [Test]
    public async Task Agrupamento_negativo_e_problema_de_dados()
    {
        var montada = Montar(new DecisoesDoProjetista(CircuitosAgrupados: -1m));

        await Assert.That(montada.Problemas).IsEquivalentTo(
            ["AMP_CircuitosAgrupados deve ser um número inteiro de circuitos, pelo menos 1 ('-1'; 0 = o do projeto)"]);
    }

    [Test]
    public async Task Problema_de_leitura_do_adapter_e_problema_de_dados_do_circuito()
    {
        var dados = new DadosDoCircuito(1, "TUG-01", "TUG", 10m, "B1", "PVC", [new DadosDoPonto(11, 1270m, 127m, "F+N", "LOCAL-SECO", "TUG")],
            ProblemasDeLeitura: ["AMP_DisjuntorProjetistaA fora da faixa (1E+300): corrija o valor"]);

        var montada = EntradaDoCircuito.Montar(dados, Condicoes, Ficticio);

        await Assert.That(montada.Entrada).IsNull();
        await Assert.That(montada.Problemas).IsEquivalentTo(["AMP_DisjuntorProjetistaA fora da faixa (1E+300): corrija o valor"]);
    }

    private static EntradaMontada Montar(DecisoesDoProjetista? decisoes) =>
        EntradaDoCircuito.Montar(
            new DadosDoCircuito(1, "TUG-01", "TUG", 10m, "B1", "PVC", [new DadosDoPonto(11, 1270m, 127m, "F+N", "LOCAL-SECO", "TUG")], Decisoes: decisoes),
            Condicoes, Ficticio);

    private static Ampere.Core.Memoria.MemoriaDeCalculo Memoria(EntradaMontada montada) =>
        DimensionamentoDeCircuito.Dimensionar(montada.Entrada!, Ficticio, Catalogos).Memoria!;
}
