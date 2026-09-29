namespace Ampere.Core.Dimensionamento;

/// <summary>
///     Corrente de projeto I<sub>B</sub>.
/// </summary>
/// <remarks>
///     Fonte normativa: TODO_NORMA — o item da NBR 5410 que define I<sub>B</sub> ainda não foi conferido no texto
///     oficial (data/DATA_GAPS.md, GAP-001). A fórmula em si é a relação elétrica P = V · I · FP.
/// </remarks>
public static class CalculoDeCorrente
{
    /// <summary>
    ///     I<sub>B</sub> = P / (V · FP) para circuito monofásico.
    /// </summary>
    /// <param name="potenciaAtivaW">Potência ativa P, em W (≥ 0).</param>
    /// <param name="tensaoV">Tensão V do circuito, em V (&gt; 0).</param>
    /// <param name="fatorDePotencia">Fator de potência FP, no intervalo (0, 1].</param>
    /// <returns>Corrente de projeto I<sub>B</sub>, em A.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Entrada fisicamente impossível: rejeitada, nunca calculada em silêncio.</exception>
    public static decimal CorrenteDeProjetoMonofasica(decimal potenciaAtivaW, decimal tensaoV, decimal fatorDePotencia)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(potenciaAtivaW);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tensaoV);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fatorDePotencia);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fatorDePotencia, 1m);

        return potenciaAtivaW / (tensaoV * fatorDePotencia);
    }
}
