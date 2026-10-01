namespace Ampere.Core.Cargas;

/// <summary>
///     Aparelho de um ponto TUE (AMP_Aparelho), para a demanda pela norma da distribuidora: ela dá um fator para cada tipo
///     de aparelho, pela quantidade deles na instalação.
/// </summary>
public enum Aparelho
{
    Chuveiro,
    Torneira,
    LavaLoucas,
    AquecedorDePassagem,
    AquecedorDeAcumulacao,
    FornoOuFogao,
    SecadoraDeRoupa,
    MicroOndas,

    /// <summary>Máquina de solda a transformador.</summary>
    MaquinaDeSolda,

    /// <summary>Aparelho que a norma da distribuidora não lista: entra sem fator de demanda (100%).</summary>
    Outro
}

/// <summary>Texto gravado em AMP_Aparelho para cada aparelho (o da descrição do parâmetro no catálogo).</summary>
public static class CodigosDeAparelho
{
    private static readonly Dictionary<Aparelho, string> CodigoPorAparelho = new()
    {
        [Aparelho.Chuveiro] = "Chuveiro",
        [Aparelho.Torneira] = "Torneira",
        [Aparelho.LavaLoucas] = "Lava-louças",
        [Aparelho.AquecedorDePassagem] = "Aquecedor de passagem",
        [Aparelho.AquecedorDeAcumulacao] = "Aquecedor de acumulação",
        [Aparelho.FornoOuFogao] = "Forno ou fogão",
        [Aparelho.SecadoraDeRoupa] = "Secadora de roupa",
        [Aparelho.MicroOndas] = "Micro-ondas",
        [Aparelho.MaquinaDeSolda] = "Máquina de solda",
        [Aparelho.Outro] = "Outro"
    };

    // Aceita o código e o nome do enum, sem diferenciar maiúsculas (como AMP_TipoCarga).
    private static readonly Dictionary<string, Aparelho> AparelhoPorTexto = CodigoPorAparelho
        .SelectMany(par => new[] { (Texto: par.Value, Aparelho: par.Key), (Texto: par.Key.ToString(), Aparelho: par.Key) })
        .DistinctBy(par => par.Texto, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(par => par.Texto, par => par.Aparelho, StringComparer.OrdinalIgnoreCase);

    /// <summary>Os códigos, na ordem do enum (para o diálogo).</summary>
    public static IReadOnlyList<string> Todos { get; } = Enum.GetValues<Aparelho>().Select(Codigo).ToList();

    public static string Codigo(Aparelho aparelho) =>
        CodigoPorAparelho.TryGetValue(aparelho, out var codigo)
            ? codigo
            : throw new ArgumentOutOfRangeException(nameof(aparelho), aparelho, "Aparelho sem código.");

    /// <summary>Lê o texto de AMP_Aparelho; vazio ou desconhecido = aparelho não informado.</summary>
    public static bool TryLer(string? texto, out Aparelho aparelho)
    {
        aparelho = default;
        return texto is not null && AparelhoPorTexto.TryGetValue(texto.Trim(), out aparelho);
    }
}
