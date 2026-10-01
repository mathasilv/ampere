using Ampere.Core.Surtos;
using Ampere.Revit.Surtos;
using Ampere.Tests.Revit.Circuitos;
using Ampere.Tests.Revit.Parametros;

namespace Ampere.Tests.Revit.Quadros;

/// <summary>Os quadros para a seleção dos DPS, com as tensões do sistema de distribuição do Revit.</summary>
[DependsOn(typeof(DesempenhoDaInjecao_Teste), ProceedOnFailure = true)]
public sealed class DpsNoRevit_Teste : TesteComProjetoEletrico
{
    [Test]
    public async Task Le_o_quadro_com_Uo_e_U_do_sistema_de_distribuicao()
    {
        var quadro = new DocumentoDeDpsRevit(Cenario.Documento).LerQuadros().Single(lido => lido.Nome == "QD1");

        // QD1 do cenário: 120/208 Wye.
        await Assert.That(quadro.Esquema).IsEqualTo("3F+N");
        await Assert.That(quadro.TensaoFaseNeutroV).IsEqualTo(120m);
        await Assert.That(quadro.TensaoEntreFasesV).IsEqualTo(208m);
        var resultado = SelecaoDeDps.Selecionar(quadro, new EscolhaDoDps(EsquemasDeAterramento.TnCS, FinalidadeDoDps.LinhaExterna), NormaDeDps.NBR5410_2004);
        await Assert.That(resultado.Problemas).IsEmpty();
        await Assert.That(resultado.Ligacoes.Single().UcMinimoV).IsEqualTo(132m);
        await Assert.That(resultado.UpMaximoKv).IsEqualTo(1.5m);
    }
}
