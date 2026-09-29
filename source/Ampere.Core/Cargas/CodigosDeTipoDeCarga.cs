namespace Ampere.Core.Cargas;

/// <summary>
///     Texto gravado em AMP_TipoCarga para cada tipo de carga: os códigos da descrição do parâmetro no catálogo
///     ("Iluminação/TUG/TUE/ArCondicionado/Motor/Reserva").
/// </summary>
public static class CodigosDeTipoDeCarga
{
    private static readonly Dictionary<TipoDeCarga, string> CodigoPorTipo = new()
    {
        [TipoDeCarga.Iluminacao] = "Iluminação",
        [TipoDeCarga.TUG] = "TUG",
        [TipoDeCarga.TUE] = "TUE",
        [TipoDeCarga.ArCondicionado] = "ArCondicionado",
        [TipoDeCarga.Motor] = "Motor",
        [TipoDeCarga.Reserva] = "Reserva"
    };

    // Aceita o código e o nome do enum, sem diferenciar maiúsculas: "tug" digitado à mão é TUG, sem ambiguidade.
    private static readonly Dictionary<string, TipoDeCarga> TipoPorTexto = CodigoPorTipo
        .SelectMany(par => new[] { (Texto: par.Value, Tipo: par.Key), (Texto: par.Key.ToString(), Tipo: par.Key) })
        .DistinctBy(par => par.Texto, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(par => par.Texto, par => par.Tipo, StringComparer.OrdinalIgnoreCase);

    public static string Codigo(TipoDeCarga tipo) =>
        CodigoPorTipo.TryGetValue(tipo, out var codigo)
            ? codigo
            : throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de carga sem código.");

    /// <summary>Lê o texto de AMP_TipoCarga; texto vazio ou desconhecido significa ponto não classificado.</summary>
    public static bool TryLer(string? texto, out TipoDeCarga tipo)
    {
        tipo = default;
        return texto is not null && TipoPorTexto.TryGetValue(texto.Trim(), out tipo);
    }
}
