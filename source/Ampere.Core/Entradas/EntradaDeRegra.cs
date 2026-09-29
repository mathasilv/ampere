using System.Globalization;
using Ampere.Core.Circuitos;

namespace Ampere.Core.Entradas;

/// <summary>
///     Converte os campos de uma linha de regras do diálogo de circuitos em <see cref="RegraDeAgrupamento" />. Campos
///     vazios = sem limite.
/// </summary>
public static class EntradaDeRegra
{
    public static RegraDigitada Interpretar(bool circuitoExclusivo, string? maximoDePontos, string? maximaPotenciaVA, CultureInfo cultura)
    {
        var problemas = new List<string>();
        int? pontos = null;
        var numeroDePontos = NumeroDigitado.Interpretar(maximoDePontos, cultura);
        if (numeroDePontos.Problema is not null)
        {
            problemas.Add($"máximo de pontos: {numeroDePontos.Problema}");
        }
        else if (numeroDePontos.Valor is { } valor)
        {
            if (valor != decimal.Truncate(valor) || valor < int.MinValue || valor > int.MaxValue)
                problemas.Add("máximo de pontos deve ser um número inteiro");
            else
                pontos = (int)valor;
        }

        var potencia = NumeroDigitado.Interpretar(maximaPotenciaVA, cultura);
        if (potencia.Problema is not null) problemas.Add($"potência máxima: {potencia.Problema}");
        if (problemas.Count > 0) return new RegraDigitada(null, problemas);

        var regra = new RegraDeAgrupamento(circuitoExclusivo, pontos, potencia.Valor);
        var deDominio = regra.Validar();
        return deDominio.Count > 0 ? new RegraDigitada(null, deDominio) : new RegraDigitada(regra, []);
    }
}

/// <summary>Regra pronta para usar (nula se houver problema) e os problemas encontrados.</summary>
public sealed record RegraDigitada(RegraDeAgrupamento? Regra, IReadOnlyList<string> Problemas);
