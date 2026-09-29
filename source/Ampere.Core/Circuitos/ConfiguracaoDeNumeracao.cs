using System.Globalization;
using Ampere.Core.Cargas;

namespace Ampere.Core.Circuitos;

/// <summary>
///     Formato do número do circuito (AMP_NumeroCircuito): [quadro][sep]prefixo[sep]NN — ex.: "TUG-03", "QD1-IL-12".
/// </summary>
/// <param name="Prefixos">Prefixo de cada tipo de carga.</param>
/// <param name="Separador">Separador entre as partes.</param>
/// <param name="Digitos">Mínimo de dígitos do número (preenchido com zeros).</param>
/// <param name="PrefixoDoQuadro">Nome do quadro no início do número; <c>null</c> = sem prefixo de quadro.</param>
public sealed record ConfiguracaoDeNumeracao(
    IReadOnlyDictionary<TipoDeCarga, string> Prefixos,
    string Separador = "-",
    int Digitos = 2,
    string? PrefixoDoQuadro = null)
{
    /// <summary>Prefixos no estilo da especificação (IL-02, TUG-01).</summary>
    public static ConfiguracaoDeNumeracao Padrao { get; } = new(new Dictionary<TipoDeCarga, string>
    {
        [TipoDeCarga.Iluminacao] = "IL",
        [TipoDeCarga.TUG] = "TUG",
        [TipoDeCarga.TUE] = "TUE",
        [TipoDeCarga.ArCondicionado] = "AC",
        [TipoDeCarga.Motor] = "MOT",
        [TipoDeCarga.Reserva] = "RES"
    });

    /// <summary>
    ///     Problemas que impedem numerar; vazio se estiver válida. Prefixo repetido é erro porque dois tipos gerariam o
    ///     mesmo número (ex.: TUG e TUE com "T" dariam dois "T-01").
    /// </summary>
    public IReadOnlyList<string> Validar()
    {
        var problemas = new List<string>();
        foreach (var tipo in Enum.GetValues<TipoDeCarga>())
        {
            if (!Prefixos.TryGetValue(tipo, out var prefixo)) problemas.Add($"sem prefixo para {CodigosDeTipoDeCarga.Codigo(tipo)}");
            else if (string.IsNullOrWhiteSpace(prefixo)) problemas.Add($"prefixo de {CodigosDeTipoDeCarga.Codigo(tipo)} vazio");
        }

        var repetidos = Prefixos
            .Where(par => !string.IsNullOrWhiteSpace(par.Value))
            .GroupBy(par => par.Value.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(grupo => grupo.Count() > 1);
        foreach (var grupo in repetidos)
        {
            var ordenados = grupo.OrderBy(par => par.Key).ToList();
            problemas.Add($"prefixo '{ordenados[^1].Value}' repetido em {string.Join(" e ", ordenados.Select(par => CodigosDeTipoDeCarga.Codigo(par.Key)))}");
        }

        if (Digitos is < 1 or > 6) problemas.Add("dígitos devem estar entre 1 e 6");
        return problemas;
    }

    /// <summary>Tudo o que vem antes do número, ex.: "QD1-TUG-".</summary>
    public string PrefixoCompleto(TipoDeCarga tipo) =>
        (string.IsNullOrEmpty(PrefixoDoQuadro) ? string.Empty : PrefixoDoQuadro + Separador) + Prefixos[tipo] + Separador;

    public string Formatar(TipoDeCarga tipo, int numero) =>
        PrefixoCompleto(tipo) + numero.ToString("D" + Digitos.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    /// <summary>Maior número já usado com o prefixo do tipo; 0 se nenhum. Textos fora do formato são ignorados.</summary>
    public int MaiorNumeroExistente(TipoDeCarga tipo, IEnumerable<string> numerosExistentes)
    {
        var prefixo = PrefixoCompleto(tipo);
        var maior = 0;
        foreach (var numero in numerosExistentes)
        {
            if (!numero.StartsWith(prefixo, StringComparison.Ordinal)) continue;
            var resto = numero[prefixo.Length..];
            if (resto.Length > 0 && resto.All(char.IsAsciiDigit) &&
                int.TryParse(resto, NumberStyles.None, CultureInfo.InvariantCulture, out var valor))
                maior = Math.Max(maior, valor);
        }

        return maior;
    }
}
