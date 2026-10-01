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
    TabelaJson<Dictionary<string, decimal>>? OcupacaoMaximaEletrodutoPct,
    TabelaJson<List<decimal>>? CorrentesNominaisIdrA,
    TabelaJson<List<decimal>>? SensibilidadesNominaisIdrMa,
    TabelaJson<List<LinhaDeProtecaoDiferencialJson>>? ProtecaoDiferencialPorLocal,
    TabelaJson<Dictionary<string, decimal>>? FatorDeDemandaPorTipo,
    TabelaJson<Dictionary<string, decimal>>? SecaoDoCondutorDeProtecaoMm2,
    TabelaJson<List<string>>? MetodosComEletroduto = null,
    TabelaDeAgrupamentoJson? FatorDeAgrupamentoEnterrado = null,
    TabelaJson<List<LinhaDeConstrucaoJson>>? ConstrucoesPorMetodo = null,
    TabelaJson<Dictionary<string, decimal>>? SecaoMinimaDoPeForaDoCaboMm2 = null);

internal sealed record TabelaJson<T>(string? Ref, T? Valores);

/// <summary>Tabela de agrupamento própria de alguns métodos (ex.: Tabela 44, linhas enterradas): chave = número exato de circuitos.</summary>
internal sealed record TabelaDeAgrupamentoJson(string? Ref, List<string>? Metodos, Dictionary<string, decimal>? Valores);

/// <param name="Ref">Referência só desta linha (ex.: a coluna da Tabela 38), no lugar da ref da tabela.</param>
internal sealed record LinhaDeCapacidadeJson(
    string? Metodo,
    string? Isolacao,
    string? Material,
    int? CondutoresCarregados,
    Dictionary<string, decimal>? PorSecaoMm2,
    string? Ref = null);

/// <param name="Metodos">Métodos de instalação a que a linha se aplica (ex.: a Tabela 40 do ar não vale para o D, enterrado); ausente = todos.</param>
/// <param name="Ref">Referência só desta linha (ex.: a coluna do solo da Tabela 40), no lugar da ref da tabela.</param>
internal sealed record LinhaDeTemperaturaJson(string? Isolacao, Dictionary<string, decimal>? PorTemperaturaC, List<string>? Metodos = null, string? Ref = null);

/// <param name="Condicionais">Construção admitida só sob uma condição (o texto vai para o aviso).</param>
internal sealed record LinhaDeConstrucaoJson(string? Metodo, List<string>? Construcoes, Dictionary<string, string>? Condicionais);

internal sealed record LinhaDeProtecaoDiferencialJson(string? Local, List<string>? TiposDeCarga, decimal? SensibilidadeMaximaMa);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ArquivoDePerfil))]
internal sealed partial class PerfilJsonContexto : JsonSerializerContext;
