using System.Diagnostics;
using Ampere.Core.Parametros;
using Ampere.Revit.Parametros;

namespace Ampere.Tests.Revit.Parametros;

/// <summary>
///     Orçamento de desempenho da injeção (especificação §8: menos de 2 s em projeto limpo).
/// </summary>
/// <remarks>
///     Mede a primeira injeção da sessão — o primeiro clique do usuário, com leitura do catálogo, JIT e primeiras
///     chamadas ao Revit incluídos. Para isso, os demais testes de integração declaram
///     <c>[DependsOn(typeof(DesempenhoDaInjecao_Teste))]</c> e só rodam depois deste.
/// </remarks>
public sealed class DesempenhoDaInjecao_Teste : TesteComProjetoNovo
{
    [Test]
    public async Task Primeira_injecao_da_sessao_em_projeto_limpo_leva_menos_de_2_segundos()
    {
        var cronometro = Stopwatch.StartNew();
        InjecaoDeParametros.Executar(CatalogoDeParametros.Padrao, new ParametrosDoDocumentoRevit(Documento));
        cronometro.Stop();

        Console.WriteLine($"Primeira injeção da sessão: {cronometro.Elapsed.TotalMilliseconds:0} ms");
        await Assert.That(cronometro.Elapsed).IsLessThan(TimeSpan.FromSeconds(2));
    }
}
