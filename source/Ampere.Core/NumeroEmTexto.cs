using System.Globalization;

namespace Ampere.Core;

/// <summary>
///     Números em textos para gente (memória de cálculo, relatório, mensagens): vírgula decimal, sem separador de milhar e
///     sem zeros à direita — igual em qualquer máquina, sem depender da cultura do Windows. JSON e chaves de tabela usam
///     ponto (<see cref="CultureInfo.InvariantCulture" />).
/// </summary>
public static class NumeroEmTexto
{
    private static readonly NumberFormatInfo Formato = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = string.Empty };

    /// <summary>O valor exato, com todas as casas.</summary>
    public static string Formatar(decimal valor) => valor == 0m ? "0" : valor.ToString("0.############################", Formato);

    /// <summary>
    ///     Para leitura: até 4 casas decimais e, abaixo de 1 em módulo, quatro algarismos significativos (0,01724 fica
    ///     inteiro; 17,320508… vira 17,3205). Só para textos e relatórios — o valor completo fica no JSON da memória.
    /// </summary>
    public static string FormatarParaLeitura(decimal valor) => Formatar(ArredondarParaLeitura(valor));

    private static decimal ArredondarParaLeitura(decimal valor)
    {
        var absoluto = Math.Abs(valor);
        if (absoluto == 0m || absoluto >= 1m) return Math.Round(valor, 4, MidpointRounding.AwayFromZero);

        var casas = 3;
        for (var escala = absoluto; escala < 1m; escala *= 10m) casas++;
        return Math.Round(valor, Math.Min(casas, 28), MidpointRounding.AwayFromZero);
    }
}
