namespace Ampere.Core.Cargas;

/// <summary>
///     Classificação aplicada em lote aos pontos de carga selecionados. Campo nulo = não alterar.
/// </summary>
/// <param name="Tipo">Tipo de carga (AMP_TipoCarga).</param>
/// <param name="PotenciaVA">Potência instalada, em VA (AMP_PotenciaInstaladaVA).</param>
/// <param name="FatorDePotencia">Fator de potência, em (0; 1] (AMP_FatorPotencia).</param>
/// <param name="TensaoV">Tensão, em V (AMP_TensaoCircuitoV).</param>
/// <param name="Fases">Configuração de fases (AMP_Fases), uma de <see cref="FasesValidas" />.</param>
/// <param name="Local">Local do ponto (AMP_Local), no vocabulário da tabela de proteção diferencial do perfil.</param>
/// <param name="Aparelho">
///     Aparelho do ponto (AMP_Aparelho), só para TUE. Nos outros tipos o aparelho anterior é apagado: um chuveiro
///     reclassificado como TUG não pode continuar contando como chuveiro na demanda.
/// </param>
public sealed record ClassificacaoDeCarga(
    TipoDeCarga Tipo,
    decimal? PotenciaVA = null,
    decimal? FatorDePotencia = null,
    decimal? TensaoV = null,
    string? Fases = null,
    string? Local = null,
    Aparelho? Aparelho = null)
{
    /// <summary>Configurações de fases aceitas em AMP_Fases.</summary>
    public static readonly IReadOnlyList<string> FasesValidas = ["F+N", "2F", "2F+N", "3F", "3F+N"];

    /// <summary>Problemas que impedem aplicar a classificação; vazio se estiver válida.</summary>
    public IReadOnlyList<string> Validar()
    {
        var problemas = new List<string>();
        if (!Enum.IsDefined(Tipo)) problemas.Add($"tipo de carga inválido ({(int)Tipo})");
        else if (Tipo == TipoDeCarga.Reserva) problemas.Add("Reserva é tipo de circuito, não de ponto de carga");

        if (PotenciaVA < 0) problemas.Add("potência não pode ser negativa");
        if (FatorDePotencia is <= 0m or > 1m) problemas.Add("fator de potência deve estar em (0; 1]");
        if (TensaoV is <= 0m) problemas.Add("tensão deve ser positiva");
        if (Fases is not null && !FasesValidas.Contains(Fases))
            problemas.Add($"fases inválidas: '{Fases}' (use {string.Join(", ", FasesValidas)})");
        if (Aparelho is { } aparelho && !Enum.IsDefined(aparelho)) problemas.Add($"aparelho inválido ({(int)aparelho})");
        else if (Aparelho is { } definido && Tipo != TipoDeCarga.TUE) problemas.Add($"aparelho ({CodigosDeAparelho.Codigo(definido)}) só em ponto TUE");
        if (Local is not null && (Local.Length == 0 || Local.Trim() != Local))
            problemas.Add($"local inválido: '{Local}' (vazio ou com espaços nas pontas)");

        return problemas;
    }
}
