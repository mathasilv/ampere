using System.Globalization;
using Ampere.Core.Dimensionamento;

namespace Ampere.Core.Entradas;

/// <summary>
///     Converte os campos do diálogo "Dimensionar circuitos" em <see cref="CondicoesDoProjeto" />. Temperatura, circuitos
///     agrupados e material são obrigatórios — o Ampere não presume a condição de instalação; os padrões de método,
///     isolação e tipos são opcionais (vazio = cada circuito usa o seu AMP_*).
/// </summary>
public static class EntradaDeCondicoes
{
    public static CondicoesDigitadas Interpretar(
        string? temperaturaC, string? circuitosAgrupados, string? material, string? metodoPadrao, string? isolacaoPadrao,
        string? tipoDeCondutorPadrao, string? tipoDeEletroduto, CultureInfo cultura)
    {
        var problemas = new List<string>();

        var temperatura = NumeroDigitado.Interpretar(temperaturaC, cultura);
        if (temperatura.Problema is not null) problemas.Add($"temperatura ambiente: {temperatura.Problema}");
        else if (temperatura.Valor is null) problemas.Add("informe a temperatura ambiente (°C)");

        int? agrupados = null;
        var numeroDeCircuitos = NumeroDigitado.Interpretar(circuitosAgrupados, cultura);
        if (numeroDeCircuitos.Problema is not null) problemas.Add($"circuitos agrupados: {numeroDeCircuitos.Problema}");
        else if (numeroDeCircuitos.Valor is not { } valor) problemas.Add("informe os circuitos agrupados (1 = circuito sozinho)");
        else if (valor != decimal.Truncate(valor) || valor < 1 || valor > int.MaxValue) problemas.Add("circuitos agrupados deve ser um número inteiro de 1 em diante");
        else agrupados = (int)valor;

        var materialEscolhido = Opcional(material);
        if (materialEscolhido is null) problemas.Add("escolha o material do condutor");
        if (problemas.Count > 0) return new CondicoesDigitadas(null, problemas);

        return new CondicoesDigitadas(new CondicoesDoProjeto(
            temperatura.Valor!.Value,
            agrupados!.Value,
            materialEscolhido!,
            Opcional(metodoPadrao),
            Opcional(isolacaoPadrao),
            Opcional(tipoDeCondutorPadrao),
            Opcional(tipoDeEletroduto)), []);
    }

    private static string? Opcional(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}

/// <summary>Condições prontas para o dimensionamento (nulas se houver problema) e os problemas encontrados.</summary>
public sealed record CondicoesDigitadas(CondicoesDoProjeto? Condicoes, IReadOnlyList<string> Problemas);
