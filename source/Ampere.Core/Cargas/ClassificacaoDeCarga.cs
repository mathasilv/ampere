namespace Ampere.Core.Cargas;

/// <summary>
///     Classificação aplicada em lote aos pontos de carga selecionados. Campo nulo = não alterar.
/// </summary>
/// <param name="Tipo">Tipo de carga (AMP_TipoCarga).</param>
/// <param name="PotenciaVA">Potência instalada, em VA (AMP_PotenciaInstaladaVA).</param>
/// <param name="FatorDePotencia">Fator de potência, em (0; 1] (AMP_FatorPotencia).</param>
/// <param name="TensaoV">Tensão, em V (AMP_TensaoCircuitoV).</param>
/// <param name="Fases">Configuração de fases (AMP_Fases), uma de <see cref="FasesValidas" />.</param>
public sealed record ClassificacaoDeCarga(
    TipoDeCarga Tipo,
    decimal? PotenciaVA = null,
    decimal? FatorDePotencia = null,
    decimal? TensaoV = null,
    string? Fases = null)
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

        return problemas;
    }
}
