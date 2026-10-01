using Ampere.Core.Cargas;
using Ampere.Core.Demanda;
using Ampere.Revit.Demanda;
using Ampere.Tests.Revit.Circuitos;
using Ampere.Tests.Revit.Parametros;

namespace Ampere.Tests.Revit.Demanda;

/// <summary>Demanda contra o Revit real (adapter <see cref="DocumentoDeDemandaRevit" />): leitura dos pontos com AMP_Aparelho.</summary>
[Property("Fonte", "CELG CT 04/18")]
[DependsOn(typeof(DesempenhoDaInjecao_Teste), ProceedOnFailure = true)]
public sealed class DemandaNoRevit_Teste : TesteComProjetoEletrico
{
    [Test]
    public async Task Le_tipo_aparelho_e_potencia_dos_pontos_e_calcula_a_demanda()
    {
        var tomadas = Cenario.ColocarTomadas(2);
        var chuveiro = Cenario.ColocarTomadas(1);
        var semClassificacao = Cenario.ColocarTomadas(1);
        ClassificacaoEmLote.Executar(tomadas, new ClassificacaoDeCarga(TipoDeCarga.TUG, PotenciaVA: 100m, TensaoV: 120m, Fases: "F+N"), Porta);
        ClassificacaoEmLote.Executar(chuveiro, new ClassificacaoDeCarga(TipoDeCarga.TUE, PotenciaVA: 5400m, TensaoV: 208m, Fases: "2F", Aparelho: Aparelho.Chuveiro), Porta);

        var pontos = new DocumentoDeDemandaRevit(Cenario.Documento).LerPontos();
        var comProblema = DemandaDaEntrada.Calcular(pontos, new OpcoesDaDemanda("Residências"), PerfilDeDemanda.CelgCt04_18);
        var calculada = DemandaDaEntrada.Calcular(pontos.Where(ponto => ponto.Id != semClassificacao[0]).ToList(), new OpcoesDaDemanda("Residências"),
            PerfilDeDemanda.CelgCt04_18);

        await Assert.That(pontos.Count).IsEqualTo(4);
        var lido = pontos.Single(ponto => ponto.Id == chuveiro[0]);
        await Assert.That(lido.Aparelho).IsEqualTo("Chuveiro");
        await Assert.That(Math.Round(lido.PotenciaVA!.Value, 3)).IsEqualTo(5400m);
        await Assert.That(string.Join(",", comProblema.PontosComProblema)).IsEqualTo($"{semClassificacao[0]}");
        // a = 0,2 kVA · 0,86 (P ≤ 1 kW); b1 = 5,4 kVA · 1 (um chuveiro).
        await Assert.That(Math.Round(calculada.DemandaVA!.Value, 3)).IsEqualTo(5572m);
    }
}
