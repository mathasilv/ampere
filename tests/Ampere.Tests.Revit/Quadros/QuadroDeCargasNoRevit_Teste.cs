using Ampere.Core.Cargas;
using Ampere.Core.Circuitos;
using Ampere.Core.Normas;
using Ampere.Core.Quadros;
using Ampere.Revit.Quadros;
using Ampere.Tests.Revit.Circuitos;

namespace Ampere.Tests.Revit.Quadros;

/// <summary>
///     Quadro de cargas contra o Revit real (F1.4, adapter): leitura dos circuitos do painel — número, tipo, potência
///     somada dos membros, esquema e tensão — e a montagem completa com fatores informados.
/// </summary>
/// <remarks>
///     Fatores do teste são de projetista, não da norma (GAP-005). Sem fatores, o perfil oficial está com a tabela
///     TODO_NORMA e o quadro precisa ficar incompleto com o motivo.
/// </remarks>
[Property("Fonte", "TODO_NORMA")]
public sealed class QuadroDeCargasNoRevit_Teste : TesteComProjetoEletrico
{
    private static readonly IReadOnlyDictionary<TipoDeCarga, RegraDeAgrupamento> RegrasDoCenario = new Dictionary<TipoDeCarga, RegraDeAgrupamento>
    {
        [TipoDeCarga.Iluminacao] = new(MaximoDePontos: 3),
        [TipoDeCarga.TUG] = new(MaximoDePontos: 4),
        [TipoDeCarga.TUE] = new(CircuitoExclusivo: true)
    };

    [Test]
    public async Task Le_os_circuitos_do_quadro_com_potencia_esquema_e_tensao()
    {
        MontarQuadroComDoisCircuitos();

        var quadros = new DocumentoDeQuadrosRevit(Cenario.Documento).LerQuadrosComCircuitos();

        await Assert.That(quadros.Select(quadro => quadro.Nome)).IsEquivalentTo(["QD1"]);
        var leituras = quadros[0].Circuitos.OrderBy(circuito => circuito.Numero, StringComparer.Ordinal).ToList();
        await Assert.That(string.Join(",", leituras.Select(circuito => circuito.Numero))).IsEqualTo("IL-01,TUG-01");
        await Assert.That(string.Join(",", leituras.Select(circuito => circuito.Tipo))).IsEqualTo(
            CodigosDeTipoDeCarga.Codigo(TipoDeCarga.Iluminacao) + "," + CodigosDeTipoDeCarga.Codigo(TipoDeCarga.TUG));
        await Assert.That(leituras[0].PotenciaVA!.Value).IsEqualTo(186m);
        await Assert.That(leituras[1].PotenciaVA!.Value).IsEqualTo(720m);
        await Assert.That(leituras.All(circuito => circuito.Fases == "F+N" && circuito.TensaoV == 127m)).IsTrue();
    }

    [Test]
    public async Task Monta_quadro_completo_com_fatores_informados()
    {
        MontarQuadroComDoisCircuitos();
        var porta = new DocumentoDeQuadrosRevit(Cenario.Documento);

        var resultados = QuadroDeCargasDoProjeto.Executar(porta, PerfilNormativo.NBR5410_2004,
            new Dictionary<TipoDeCarga, decimal> { [TipoDeCarga.Iluminacao] = 1m, [TipoDeCarga.TUG] = 0.5m });

        await Assert.That(resultados.Count).IsEqualTo(1);
        var quadro = resultados[0].Quadro;
        await Assert.That(quadro.Problemas).IsEmpty();
        await Assert.That(quadro.Linhas.Count).IsEqualTo(2);
        await Assert.That(quadro.PotenciaInstaladaVA).IsEqualTo(906m);
        await Assert.That(quadro.DemandaVA).IsEqualTo(546m);
        await Assert.That(quadro.CorrenteDeDemandaA).IsEqualTo(546m / 127m);
        await Assert.That(quadro.Memoria).IsNotNull();
        await Assert.That(quadro.Memoria!.Circuito).IsEqualTo("QD1");
        await Assert.That(resultados[0].Nome).IsEqualTo("QD1");
    }

    [Test]
    public async Task Sem_fatores_o_quadro_fica_incompleto_com_o_motivo_da_tabela()
    {
        MontarQuadroComDoisCircuitos();
        var porta = new DocumentoDeQuadrosRevit(Cenario.Documento);

        var resultados = QuadroDeCargasDoProjeto.Executar(porta, PerfilNormativo.NBR5410_2004);

        var quadro = resultados[0].Quadro;
        await Assert.That(quadro.DemandaVA).IsNull();
        await Assert.That(quadro.Memoria).IsNull();
        await Assert.That(quadro.PotenciaInstaladaVA).IsEqualTo(906m);
        await Assert.That(quadro.Problemas.Any(problema => problema.Contains("TODO_NORMA"))).IsTrue();
    }

    /// <summary>QD1 com IL-01 (3 × 62 VA) e TUG-01 (4 × 180 VA), classificados com F+N 127 V.</summary>
    private void MontarQuadroComDoisCircuitos()
    {
        var luminarias = Cenario.ColocarLuminarias(3);
        var tomadas = Cenario.ColocarTomadas(4);
        Classificar(luminarias, TipoDeCarga.Iluminacao, 62m);
        Classificar(tomadas, TipoDeCarga.TUG, 180m);
        CriacaoDeCircuitos.Executar([.. luminarias, .. tomadas], Cenario.Quadro.Id.Value, RegrasDoCenario, ConfiguracaoDeNumeracao.Padrao, Porta);
    }

    private void Classificar(IReadOnlyCollection<long> ids, TipoDeCarga tipo, decimal potenciaVA)
    {
        var resultado = ClassificacaoEmLote.Executar(ids, new ClassificacaoDeCarga(tipo, PotenciaVA: potenciaVA, TensaoV: 127m, Fases: "F+N"), Porta);
        if (resultado.Classificados != ids.Count) throw new InvalidOperationException($"Classificação do cenário incompleta ({tipo}).");
    }
}
