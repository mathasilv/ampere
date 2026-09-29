using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ampere.Core.Parametros;

/// <summary>
///     Catálogo dos parâmetros compartilhados Ampere (AMP_*), lido do JSON versionado
///     <c>data/parametros/parametros_compartilhados_ampere.json</c>, embarcado neste assembly.
/// </summary>
/// <remarks>
///     A leitura é estrita: campo desconhecido, tipo ou categoria fora da lista, GUID inválido ou repetido invalidam o
///     catálogo inteiro. Um catálogo meio certo injetaria parâmetros errados no modelo sem ninguém perceber.
/// </remarks>
public sealed class CatalogoDeParametros
{
    private const string RecursoPadrao = "Ampere.Core.Parametros.parametros_compartilhados_ampere.json";
    private const string Prefixo = "AMP_";

    private static readonly Lazy<CatalogoDeParametros> CatalogoPadrao = new(CarregarPadrao);

    private static readonly JsonSerializerOptions OpcoesDeLeitura = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private static readonly Dictionary<string, TipoDeDadoDoParametro> TiposPorCodigo = new(StringComparer.Ordinal)
    {
        ["TEXT"] = TipoDeDadoDoParametro.Texto,
        ["NUMBER"] = TipoDeDadoDoParametro.Numero,
        ["ELECTRICAL_APPARENT_POWER"] = TipoDeDadoDoParametro.PotenciaAparente,
        ["CURRENT"] = TipoDeDadoDoParametro.Corrente,
        ["LENGTH"] = TipoDeDadoDoParametro.Comprimento
    };

    private CatalogoDeParametros(string grupoRevit, IReadOnlyList<DefinicaoDeParametro> parametros)
    {
        GrupoRevit = grupoRevit;
        Parametros = parametros;
    }

    /// <summary>Grupo do arquivo de parâmetros compartilhados em que as definições são criadas.</summary>
    public string GrupoRevit { get; }

    /// <summary>Definições, na ordem do arquivo.</summary>
    public IReadOnlyList<DefinicaoDeParametro> Parametros { get; }

    /// <summary>Catálogo oficial, embarcado no assembly.</summary>
    public static CatalogoDeParametros Padrao => CatalogoPadrao.Value;

    /// <summary>Lê e valida um catálogo em JSON.</summary>
    /// <exception cref="CatalogoDeParametrosInvalidoException">Com todos os problemas encontrados.</exception>
    public static CatalogoDeParametros Carregar(string json)
    {
        ArquivoJson? arquivo;
        try
        {
            arquivo = JsonSerializer.Deserialize<ArquivoJson>(json, OpcoesDeLeitura);
        }
        catch (JsonException excecao)
        {
            throw new CatalogoDeParametrosInvalidoException([$"JSON inválido: {excecao.Message}"]);
        }

        if (arquivo is null) throw new CatalogoDeParametrosInvalidoException(["JSON vazio"]);

        var problemas = new List<string>();
        if (arquivo.Meta is null) problemas.Add("$meta ausente (fonte, versão e data são obrigatórias)");
        if (string.IsNullOrWhiteSpace(arquivo.GrupoRevit)) problemas.Add("grupo_revit vazio");
        if (arquivo.Parametros is not { Count: > 0 }) problemas.Add("nenhum parâmetro declarado");

        var definicoes = (arquivo.Parametros ?? []).Select(parametro => Validar(parametro, problemas)).ToList();
        VerificarRepeticoes(definicoes, problemas);

        if (problemas.Count > 0) throw new CatalogoDeParametrosInvalidoException(problemas);
        return new CatalogoDeParametros(arquivo.GrupoRevit!, definicoes);
    }

    private static CatalogoDeParametros CarregarPadrao()
    {
        using var recurso = typeof(CatalogoDeParametros).Assembly.GetManifestResourceStream(RecursoPadrao)
                            ?? throw new InvalidOperationException($"Recurso embarcado '{RecursoPadrao}' não encontrado.");
        using var leitor = new StreamReader(recurso, Encoding.UTF8);
        return Carregar(leitor.ReadToEnd());
    }

    private static DefinicaoDeParametro Validar(ParametroJson parametro, List<string> problemas)
    {
        var nome = parametro.Nome ?? string.Empty;
        if (!nome.StartsWith(Prefixo, StringComparison.Ordinal))
            problemas.Add($"{nome}: nome deve começar com '{Prefixo}'");

        if (!Guid.TryParseExact(parametro.Guid, "D", out var guid) || guid == Guid.Empty)
            problemas.Add($"{nome}: GUID inválido '{parametro.Guid}'");

        if (!TiposPorCodigo.TryGetValue(parametro.Tipo ?? string.Empty, out var tipo))
            problemas.Add($"{nome}: tipo desconhecido '{parametro.Tipo}'");

        var categorias = new List<CategoriaEletrica>();
        foreach (var codigo in parametro.Categorias ?? [])
        {
            // TryParse aceitaria "2" ou " Luminarias"; só o nome exato do enum vale.
            if (!Enum.TryParse<CategoriaEletrica>(codigo, ignoreCase: false, out var categoria) || categoria.ToString() != codigo)
                problemas.Add($"{nome}: categoria desconhecida '{codigo}'");
            else if (categorias.Contains(categoria))
                problemas.Add($"{nome}: categoria repetida '{codigo}'");
            else
                categorias.Add(categoria);
        }

        if (parametro.Categorias is not { Count: > 0 })
            problemas.Add($"{nome}: sem categorias");

        return new DefinicaoDeParametro(guid, nome, tipo, parametro.Descricao ?? string.Empty, categorias);
    }

    private static void VerificarRepeticoes(IReadOnlyList<DefinicaoDeParametro> definicoes, List<string> problemas)
    {
        foreach (var repetidos in definicoes.Where(definicao => definicao.Guid != Guid.Empty).GroupBy(definicao => definicao.Guid))
        {
            if (repetidos.Count() > 1)
                problemas.Add($"GUID {repetidos.Key} repetido em {string.Join(" e ", repetidos.Select(definicao => definicao.Nome))}");
        }

        foreach (var repetidos in definicoes.GroupBy(definicao => definicao.Nome, StringComparer.OrdinalIgnoreCase))
        {
            var primeiro = repetidos.First();
            foreach (var repetido in repetidos.Skip(1))
                problemas.Add($"nome repetido: {repetido.Nome} (já declarado como {primeiro.Nome})");
        }
    }

    private sealed record ArquivoJson(
        [property: JsonPropertyName("$meta")] JsonElement? Meta,
        string? Produto,
        string? GrupoRevit,
        IReadOnlyList<ParametroJson>? Parametros);

    private sealed record ParametroJson(
        string? Guid,
        string? Nome,
        string? Tipo,
        string? Descricao,
        IReadOnlyList<string>? Categorias);
}
