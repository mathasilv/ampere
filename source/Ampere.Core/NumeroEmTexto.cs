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

    public static string Formatar(decimal valor) => valor == 0m ? "0" : valor.ToString("0.############################", Formato);
}
