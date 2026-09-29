namespace Ampere.Core.Circuitos;

/// <summary>
///     Como agrupar os pontos de um tipo de carga em circuitos. Todos os limites são definidos pelo projetista.
/// </summary>
/// <remarks>
///     Fonte normativa: TODO_NORMA — os limites da NBR 5410 por tipo de carga e por local ainda não estão no perfil
///     (data/DATA_GAPS.md, GAP-002). Por isso o Ampere não sugere nenhum valor: sem regra, o tipo não tem limite.
/// </remarks>
/// <param name="CircuitoExclusivo">Cada ponto no seu próprio circuito.</param>
/// <param name="MaximoDePontos">Máximo de pontos por circuito (≥ 1); <c>null</c> = sem limite.</param>
/// <param name="MaximaPotenciaVA">Potência máxima por circuito, em VA (&gt; 0); <c>null</c> = sem limite.</param>
public sealed record RegraDeAgrupamento(
    bool CircuitoExclusivo = false,
    int? MaximoDePontos = null,
    decimal? MaximaPotenciaVA = null)
{
    /// <summary>Problemas que impedem usar a regra; vazio se estiver válida.</summary>
    public IReadOnlyList<string> Validar()
    {
        var problemas = new List<string>();
        if (MaximoDePontos is < 1) problemas.Add("máximo de pontos por circuito deve ser pelo menos 1");
        if (MaximaPotenciaVA is <= 0m) problemas.Add("potência máxima por circuito deve ser positiva");
        return problemas;
    }
}
