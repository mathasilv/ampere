using System.Globalization;

namespace Ampere.Core.Entradas;

/// <summary>
///     Número digitado pelo projetista num campo da interface, na cultura dela.
/// </summary>
/// <remarks>
///     Só vale o separador decimal da cultura; separador de milhar é recusado com mensagem. Em pt-BR, "0.92" seria lido
///     como 92 e "1.200" é ambíguo — pedir de novo é melhor que gravar um valor errado em silêncio.
/// </remarks>
public static class NumeroDigitado
{
    private const NumberStyles Estilos = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    /// <summary>Texto vazio = não informado (sem valor e sem problema).</summary>
    public static NumeroInterpretado Interpretar(string? texto, CultureInfo cultura)
    {
        if (string.IsNullOrWhiteSpace(texto)) return new NumeroInterpretado(null, null);

        var limpo = texto.Trim();
        var formato = cultura.NumberFormat;
        if (limpo.Contains(formato.NumberGroupSeparator, StringComparison.Ordinal))
            return new NumeroInterpretado(null, $"número sem separador de milhar: use só '{formato.NumberDecimalSeparator}' para decimais ('{limpo}')");

        return decimal.TryParse(limpo, Estilos, formato, out var valor)
            ? new NumeroInterpretado(valor, null)
            : new NumeroInterpretado(null, $"número inválido: '{limpo}'");
    }
}

/// <summary>Valor lido (nulo se vazio ou inválido) e o problema, se houver.</summary>
public readonly record struct NumeroInterpretado(decimal? Valor, string? Problema);
