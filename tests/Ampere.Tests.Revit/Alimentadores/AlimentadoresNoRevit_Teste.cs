using Ampere.Core.Alimentadores;
using Ampere.Core.Cargas;
using Ampere.Core.Catalogos;
using Ampere.Core.Circuitos;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Normas;
using Ampere.Core.Parametros;
using Ampere.Core.Quadros;
using Ampere.Revit.Alimentadores;
using Ampere.Revit.Dimensionamento;
using Ampere.Revit.Quadros;
using Ampere.Tests.Revit.Circuitos;
using Ampere.Tests.Revit.Parametros;
using Autodesk.Revit.DB.Electrical;

namespace Ampere.Tests.Revit.Alimentadores;

/// <summary>
///     Alimentadores contra o Revit real: QD1 (tomadas F+N 120 V) ligado a um QGBT por um circuito do Revit. O perfil oficial
///     com os catálogos vazios para no diâmetro do condutor, depois da proteção — que é gravada no alimentador.
/// </summary>
[Property("Fonte", "TODO_NORMA")]
[DependsOn(typeof(DesempenhoDaInjecao_Teste), ProceedOnFailure = true)]
public sealed class AlimentadoresNoRevit_Teste : TesteComProjetoEletrico
{
    private const string DemaisLocais = "Demais locais internos";
    private static readonly CondicoesDoProjeto Condicoes = new(30m, 1, "Cobre", "B1", "PVC");
    private static readonly Dictionary<TipoDeCarga, decimal> Fatores = new() { [TipoDeCarga.TUG] = 0.5m };

    [Test]
    public async Task Le_o_alimentador_do_quadro_e_a_origem()
    {
        var (_, alimentador) = MontarQd1AlimentadoPeloQgbt();

        var quadros = new DocumentoDeAlimentadoresRevit(Cenario.Documento).LerAlimentadores();

        var qd1 = quadros.Single(quadro => quadro.Quadro == "QD1");
        await Assert.That(qd1.Alimentador!.Id).IsEqualTo(alimentador.Id.Value);
        await Assert.That(qd1.Origem).IsEqualTo("QGBT");
        await Assert.That(qd1.OrigemAlimentada).IsFalse();
        await Assert.That(qd1.AlimentaQuadros).IsFalse();
        await Assert.That(Math.Round(qd1.Alimentador.ComprimentoM!.Value, 6)).IsEqualTo(15m);
        var qgbt = quadros.Single(quadro => quadro.Quadro == "QGBT");
        await Assert.That(qgbt.Alimentador).IsNull();
        await Assert.That(qgbt.AlimentaQuadros).IsTrue();
    }

    [Test]
    public async Task Dimensiona_e_grava_o_alimentador_pela_demanda_do_quadro()
    {
        var (_, alimentador) = MontarQd1AlimentadoPeloQgbt();
        var porta = new DocumentoDeAlimentadoresRevit(Cenario.Documento);

        var resultados = DimensionamentoDeAlimentadores.Executar("ponto_de_entrega", PerfilNormativo.NBR5410_2004, CatalogosDeProduto.Padrao, porta,
            new DocumentoDeQuadrosRevit(Cenario.Documento));

        var qd1 = resultados.Single(resultado => resultado.Quadro == "QD1");
        await Assert.That(qd1.Problemas).IsEmpty();
        var calculo = qd1.Circuito!.Dimensionamento!;
        await Assert.That(calculo.IdrAvaliado).IsTrue();
        await Assert.That(calculo.DisjuntorA).IsNotNull();
        await Assert.That(Parametro(alimentador, "AMP_DisjuntorNominalA").AsDouble()).IsEqualTo((double)calculo.DisjuntorA!.Value);
        await Assert.That(Parametro(alimentador, "AMP_MemoriaCalculoId").AsString()).IsEqualTo(qd1.Circuito.Memoria!.Hash());
        await Assert.That(calculo.Memoria!.Passos[0].Observacao!).Contains("do QD1");
        await Assert.That(resultados.Single(resultado => resultado.Quadro == "QGBT").Problemas.Single()).StartsWith("sem circuito alimentador no Revit");
    }

    [Test]
    public async Task Quadro_de_cargas_desatualizado_impede_e_apaga_o_alimentador()
    {
        var (tomadas, alimentador) = MontarQd1AlimentadoPeloQgbt();
        var porta = new DocumentoDeAlimentadoresRevit(Cenario.Documento);
        var quadros = new DocumentoDeQuadrosRevit(Cenario.Documento);
        DimensionamentoDeAlimentadores.Executar("ponto_de_entrega", PerfilNormativo.NBR5410_2004, CatalogosDeProduto.Padrao, porta, quadros);
        Transacionar(() => Parametro(Cenario.Documento.GetElement(new ElementId(tomadas[0])), "AMP_PotenciaInstaladaVA")
            .Set(UnitUtils.ConvertToInternalUnits(600, UnitTypeId.VoltAmperes)));

        var resultados = DimensionamentoDeAlimentadores.Executar("ponto_de_entrega", PerfilNormativo.NBR5410_2004, CatalogosDeProduto.Padrao, porta, quadros);

        await Assert.That(resultados.Single(resultado => resultado.Quadro == "QD1").Problemas.Single()).StartsWith("quadro de cargas desatualizado");
        await Assert.That(Parametro(alimentador, "AMP_DisjuntorNominalA").AsDouble()).IsEqualTo(0d);
        await Assert.That(Parametro(alimentador, "AMP_MemoriaCalculoId").AsString() ?? string.Empty).IsEqualTo(string.Empty);
    }

    /// <summary>
    ///     QD1 com TUG-01 (4 × 180 VA, F+N 120 V) dimensionado e com o quadro de cargas montado (TUG 0,5); QGBT ao lado, de onde
    ///     sai o alimentador do QD1 (15 m informados).
    /// </summary>
    private (List<long> Tomadas, ElectricalSystem Alimentador) MontarQd1AlimentadoPeloQgbt()
    {
        var tomadas = Cenario.ColocarTomadas(4);
        var classificacao = ClassificacaoEmLote.Executar(tomadas,
            new ClassificacaoDeCarga(TipoDeCarga.TUG, PotenciaVA: 180m, TensaoV: 120m, Fases: "F+N", Local: DemaisLocais), Porta);
        if (classificacao.Classificados != tomadas.Count) throw new InvalidOperationException("Classificação do cenário incompleta.");
        CriacaoDeCircuitos.Executar(tomadas, Cenario.Quadro.Id.Value, new Dictionary<TipoDeCarga, RegraDeAgrupamento> { [TipoDeCarga.TUG] = new(MaximoDePontos: 4) },
            ConfiguracaoDeNumeracao.Padrao, Porta);

        var dimensionamento = new DocumentoDeDimensionamentoRevit(Cenario.Documento);
        DimensionamentoDoProjeto.Executar(dimensionamento.ListarCircuitos(), Condicoes, PerfilNormativo.NBR5410_2004, CatalogosDeProduto.Padrao, dimensionamento);
        var quadros = new DocumentoDeQuadrosRevit(Cenario.Documento);
        QuadroDeCargasDoProjeto.Gravar(QuadroDeCargasDoProjeto.Executar(quadros, PerfilNormativo.NBR5410_2004, Fatores), quadros, Fatores);

        var qgbt = Cenario.ColocarQuadro("QGBT", 20);
        ElectricalSystem alimentador = null!;
        Transacionar(() =>
        {
            alimentador = ElectricalSystem.Create(Cenario.Documento, [Cenario.Quadro.Id], ElectricalSystemType.PowerCircuit);
            alimentador.SelectPanel(qgbt);
            Parametro(alimentador, "AMP_ComprimentoRotaM").Set(UnitUtils.ConvertToInternalUnits(15, UnitTypeId.Meters));
        });
        return (tomadas, alimentador);
    }

    private void Transacionar(Action acao)
    {
        using var transacao = new Transaction(Cenario.Documento, "Preparação do teste");
        transacao.Start();
        acao();
        transacao.Commit();
    }

    private static Parameter Parametro(Element elemento, string nome) =>
        elemento.get_Parameter(CatalogoDeParametros.Padrao.Parametros.Single(parametro => parametro.Nome == nome).Guid);
}
