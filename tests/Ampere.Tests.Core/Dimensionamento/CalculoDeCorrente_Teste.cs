using Ampere.Core.Dimensionamento;

namespace Ampere.Tests.Core.Dimensionamento;

/// <summary>
///     Teste-exemplo da F0: corrente de projeto I<sub>B</sub> de circuito monofásico, IB = P / (V · FP).
/// </summary>
/// <remarks>
///     Valores de brinquedo, não extraídos da norma. Fonte normativa: TODO_NORMA (data/DATA_GAPS.md, GAP-001).
/// </remarks>
[Property("Fonte", "TODO_NORMA")]
public class CalculoDeCorrente_Teste
{
    [Test]
    [Arguments(1270, 127, 1.0, 10.0)]
    [Arguments(2200, 220, 0.8, 12.5)]
    public async Task Monofasico_IB_e_P_sobre_V_vezes_FP(decimal potenciaAtivaW, decimal tensaoV, decimal fatorDePotencia, decimal ibEsperadaA)
    {
        var ib = CalculoDeCorrente.CorrenteDeProjetoMonofasica(potenciaAtivaW, tensaoV, fatorDePotencia);

        await Assert.That(ib).IsEqualTo(ibEsperadaA);
    }

    [Test]
    [Arguments(-1, 127, 1.0)]
    [Arguments(1000, 0, 1.0)]
    [Arguments(1000, -127, 1.0)]
    [Arguments(1000, 127, 0)]
    [Arguments(1000, 127, 1.01)]
    public async Task Entrada_fisicamente_invalida_e_rejeitada(decimal potenciaAtivaW, decimal tensaoV, decimal fatorDePotencia)
    {
        await Assert.That(() => CalculoDeCorrente.CorrenteDeProjetoMonofasica(potenciaAtivaW, tensaoV, fatorDePotencia))
            .Throws<ArgumentOutOfRangeException>();
    }
}
