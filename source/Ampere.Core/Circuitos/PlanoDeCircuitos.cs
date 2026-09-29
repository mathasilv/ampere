using Ampere.Core.Cargas;

namespace Ampere.Core.Circuitos;

/// <summary>
///     Circuitos a criar, pontos que ficaram de fora (com o motivo) e avisos para o projetista.
/// </summary>
public sealed record PlanoDeCircuitos(
    IReadOnlyList<CircuitoPlanejado> Circuitos,
    IReadOnlyList<PontoIgnorado> Ignorados,
    IReadOnlyList<string> Avisos);

/// <summary>
///     Circuito a criar no quadro.
/// </summary>
/// <param name="Numero">Número do circuito (AMP_NumeroCircuito), ex.: "TUG-03".</param>
/// <param name="Tipo">Tipo de carga do circuito.</param>
/// <param name="Alimentacao">Chave de compatibilidade elétrica comum a todos os pontos.</param>
/// <param name="Pontos">Identificadores dos pontos, em ordem crescente.</param>
/// <param name="PotenciaTotalVA">Soma das potências; <c>null</c> se algum ponto não tiver potência.</param>
public sealed record CircuitoPlanejado(
    string Numero,
    TipoDeCarga Tipo,
    string Alimentacao,
    IReadOnlyList<long> Pontos,
    decimal? PotenciaTotalVA);

/// <summary>
///     Ponto selecionado que não entrou em circuito, e por quê.
/// </summary>
public sealed record PontoIgnorado(long Id, string Motivo);
