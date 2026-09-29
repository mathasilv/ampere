using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ampere.Core.Normas;

// Formato em disco do perfil normativo (data/normas/<norma>/<ano>/perfil.json). Tudo anulável: a validação do
// PerfilNormativo relata o que faltar, em vez de o desserializador falhar no primeiro problema.

internal sealed record ArquivoDePerfil(
    [property: JsonPropertyName("$meta")] MetaDoPerfil? Meta,
    string? Perfil,
    Dictionary<string, string>? Regras,
    TabelasDoPerfil? Tabelas);

internal sealed record MetaDoPerfil(string? Fonte, string? Versao, string? Data, bool? Ficticio, string? Situacao);

internal sealed record TabelasDoPerfil(
    TabelaJson<List<decimal>>? SecoesNominaisMm2,
    TabelaJson<List<decimal>>? CorrentesNominaisDisjuntorA,
    TabelaJson<Dictionary<string, int>>? CondutoresCarregados,
    TabelaJson<Dictionary<string, decimal>>? SecaoMinimaMm2,
    TabelaJson<List<LinhaDeCapacidadeJson>>? CapacidadeDeConducaoA,
    TabelaJson<List<LinhaDeTemperaturaJson>>? FatorDeTemperatura,
    TabelaJson<Dictionary<string, decimal>>? FatorDeAgrupamento,
    TabelaJson<Dictionary<string, decimal>>? QuedaDeTensaoMaximaPct,
    TabelaJson<Dictionary<string, decimal>>? ResistividadeOhmMm2PorM,
    TabelaJson<Dictionary<string, decimal>>? OcupacaoMaximaEletrodutoPct);

internal sealed record TabelaJson<T>(string? Ref, T? Valores);

internal sealed record LinhaDeCapacidadeJson(
    string? Metodo,
    string? Isolacao,
    string? Material,
    int? CondutoresCarregados,
    Dictionary<string, decimal>? PorSecaoMm2);

internal sealed record LinhaDeTemperaturaJson(string? Isolacao, Dictionary<string, decimal>? PorTemperaturaC);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ArquivoDePerfil))]
internal sealed partial class PerfilJsonContexto : JsonSerializerContext;
