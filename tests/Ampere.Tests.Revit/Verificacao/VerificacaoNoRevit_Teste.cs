using Ampere.Core.Cargas;
using Ampere.Core.Catalogos;
using Ampere.Core.Circuitos;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Normas;
using Ampere.Core.Parametros;
using Ampere.Core.Verificacao;
using Ampere.Revit.Dimensionamento;
using Ampere.Revit.Verificacao;
using Ampere.Tests.Revit.Circuitos;
using Ampere.Tests.Revit.Parametros;
using Autodesk.Revit.DB.Electrical;

namespace Ampere.Tests.Revit.Verificacao;

/// <summary>
///     Verificação contra o Revit real (adapter <see cref="DocumentoDeVerificacaoRevit" />): leitura dos pontos e
///     circuitos e a conferência das memórias gravadas pelo "Dimensionar".
/// </summary>
[DependsOn(typeof(DesempenhoDaInjecao_Teste), ProceedOnFailure = true)]
public sealed class VerificacaoNoRevit_Teste : TesteComProjetoEletrico
{
    private const string DemaisLocais = "Demais locais internos";

    private static readonly CondicoesDoProjeto Condicoes = new(30m, 1, "Cobre", "B1", "PVC");

    private static readonly IReadOnlyDictionary<TipoDeCarga, RegraDeAgrupamento> Regras = new Dictionary<TipoDeCarga, RegraDeAgrupamento>
    {
        [TipoDeCarga.TUG] = new(MaximoDePontos: 4)
    };

    [Test]
    public async Task Le_pontos_e_circuitos_e_aponta_o_que_falta()
    {
        var tomadas = Cenario.ColocarTomadas(3);
        var semClassificacao = Cenario.ColocarTomadas(1);
        var foraDeCircuito = Cenario.ColocarTomadas(1);
        var avulsa = Cenario.ColocarTomadas(1);
        Classificar([.. tomadas, .. foraDeCircuito, .. avulsa]);
        CriacaoDeCircuitos.Executar(tomadas, Cenario.Quadro.Id.Value, Regras, ConfiguracaoDeNumeracao.Padrao, Porta);
        Transacionar(() => ElectricalSystem.Create(Cenario.Documento, [new ElementId(avulsa[0])], ElectricalSystemType.PowerCircuit));
        var porta = new DocumentoDeVerificacaoRevit(Cenario.Documento);

        var pontos = porta.LerPontos();
        var circuitos = porta.LerCircuitosDeForca();
        var relatorio = VerificacaoDoProjeto.Executar(porta, PerfilNormativo.NBR5410_2004, CatalogosDeProduto.Padrao);

        await Assert.That(pontos.Count).IsEqualTo(6);
        await Assert.That(pontos.Single(ponto => ponto.Id == semClassificacao[0]).TipoDeCarga).IsNull();
        await Assert.That(pontos.Single(ponto => ponto.Id == foraDeCircuito[0]).Circuito).IsNull();
        await Assert.That(pontos.Where(ponto => tomadas.Contains(ponto.Id)).All(ponto => ponto.Circuito is not null && ponto.Local == DemaisLocais)).IsTrue();
        await Assert.That(circuitos.Count).IsEqualTo(2);
        await Assert.That(circuitos.Count(circuito => circuito.Numero is null)).IsEqualTo(1);
        await Assert.That(circuitos.Single(circuito => circuito.Numero is null).Nome).EndsWith("do Revit");

        await Assert.That(Elementos(relatorio, VerificacaoDoProjeto.PontosSemClassificacao)).IsEqualTo($"{semClassificacao[0]}");
        await Assert.That(Elementos(relatorio, VerificacaoDoProjeto.PontosForaDeCircuito)).IsEqualTo($"{foraDeCircuito[0]}");
        await Assert.That(Elementos(relatorio, VerificacaoDoProjeto.CircuitosForaDoAmpere)).IsEqualTo(
            $"{circuitos.Single(circuito => circuito.Numero is null).Id}");
        // Nunca dimensionado: sem memória e sem condições, é "não dimensionado" (nada a conferir).
        await Assert.That(relatorio.MemoriasConferidas).IsTrue();
        await Assert.That(Elementos(relatorio, VerificacaoDoProjeto.CircuitosNaoDimensionados)).IsEqualTo(
            $"{circuitos.Single(circuito => circuito.Numero is not null).Id}");
    }

    [Test]
    public async Task Memoria_gravada_pelo_Dimensionar_confere_ate_o_modelo_mudar()
    {
        var tomadas = Cenario.ColocarTomadas(3);
        Classificar(tomadas);
        CriacaoDeCircuitos.Executar(tomadas, Cenario.Quadro.Id.Value, Regras, ConfiguracaoDeNumeracao.Padrao, Porta);
        var dimensionamento = new DocumentoDeDimensionamentoRevit(Cenario.Documento);
        DimensionamentoDoProjeto.Executar(dimensionamento.ListarCircuitos(), Condicoes, PerfilNormativo.NBR5410_2004, CatalogosDeProduto.Padrao, dimensionamento);
        var porta = new DocumentoDeVerificacaoRevit(Cenario.Documento);

        var emDia = VerificacaoDoProjeto.Executar(porta, PerfilNormativo.NBR5410_2004, CatalogosDeProduto.Padrao);
        Transacionar(() => Parametro(Cenario.Documento.GetElement(new ElementId(tomadas[0])), "AMP_PotenciaInstaladaVA")
            .Set(UnitUtils.ConvertToInternalUnits(600, UnitTypeId.VoltAmperes)));
        var depois = VerificacaoDoProjeto.Executar(porta, PerfilNormativo.NBR5410_2004, CatalogosDeProduto.Padrao);

        await Assert.That(emDia.MemoriasConferidas).IsTrue();
        await Assert.That(emDia.Pendencias.Where(pendencia => pendencia.Gravidade != GravidadeDaPendencia.Informacao)).IsEmpty();
        await Assert.That(Elementos(depois, VerificacaoDoProjeto.MemoriasDesatualizadas)).IsEqualTo($"{dimensionamento.ListarCircuitos().Single()}");
    }

    private static string Elementos(RelatorioDeVerificacao relatorio, string grupo) =>
        string.Join(";", relatorio.Pendencias.Where(pendencia => pendencia.Grupo == grupo).SelectMany(pendencia => pendencia.Elementos));

    private void Classificar(IReadOnlyCollection<long> ids)
    {
        var resultado = ClassificacaoEmLote.Executar(ids, new ClassificacaoDeCarga(TipoDeCarga.TUG, PotenciaVA: 180m, TensaoV: 127m, Fases: "F+N", Local: DemaisLocais), Porta);
        if (resultado.Classificados != ids.Count) throw new InvalidOperationException("Classificação do cenário incompleta.");
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
