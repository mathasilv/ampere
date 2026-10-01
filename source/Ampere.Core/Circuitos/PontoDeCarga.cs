using Ampere.Core.Cargas;

namespace Ampere.Core.Circuitos;

/// <summary>
///     Ponto de carga (luminária, tomada, equipamento) como o adapter o lê do documento.
/// </summary>
/// <param name="Id">Identificador do elemento no documento.</param>
/// <param name="Tipo">Tipo de carga (AMP_TipoCarga); <c>null</c> se não classificado.</param>
/// <param name="PotenciaVA">Potência instalada (AMP_PotenciaInstaladaVA); <c>null</c> se não informada.</param>
/// <param name="Alimentacao">
///     Chave de compatibilidade elétrica nativa do ponto (ex.: "120 V · 1 polo"). Pontos com chaves diferentes nunca
///     dividem circuito — o Revit recusaria.
/// </param>
/// <param name="CircuitoAtual">Circuito a que o ponto já pertence; <c>null</c> se livre.</param>
/// <param name="TensaoV">AMP_TensaoCircuitoV (nulo = vazio), para a corrente do ponto na divisão da instalação.</param>
/// <param name="Fases">AMP_Fases (nulo = vazio), idem.</param>
public sealed record PontoDeCarga(
    long Id,
    TipoDeCarga? Tipo,
    decimal? PotenciaVA,
    string Alimentacao,
    string? CircuitoAtual = null,
    decimal? TensaoV = null,
    string? Fases = null);
