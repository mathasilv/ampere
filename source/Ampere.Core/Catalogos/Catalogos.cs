using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ampere.Core.Normas;

namespace Ampere.Core.Catalogos;

/// <summary>
///     Catálogo de condutores: diâmetro externo por tipo de cabo e seção (para a ocupação de eletrodutos).
/// </summary>
/// <remarks>
///     Dado de fabricante, com a mesma disciplina do perfil normativo: catálogo com ref TODO_CATALOGO não pode ter
///     valores, e valores exigem a ref (fabricante e edição). O oficial está vazio até a escolha do fabricante.
/// </remarks>
public sealed class CatalogoDeCondutores
{
    private static readonly Lazy<CatalogoDeCondutores> Oficial =
        new(() => Carregar(RegrasDeCatalogo.LerRecurso("Ampere.Core.Catalogos.condutores.json")));

    private readonly string _referencia;
    private readonly bool _pendente;
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<decimal, decimal>> _diametros;

    private CatalogoDeCondutores(bool ficticio, string referencia, bool pendente, IReadOnlyDictionary<string, IReadOnlyDictionary<decimal, decimal>> diametros)
    {
        Ficticio = ficticio;
        _referencia = referencia;
        _pendente = pendente;
        _diametros = diametros;
    }

    /// <summary>Catálogo só para testes.</summary>
    public bool Ficticio { get; }

    /// <summary>Catálogo oficial, embarcado no assembly.</summary>
    public static CatalogoDeCondutores Padrao => Oficial.Value;

    /// <exception cref="CatalogoDeProdutoInvalidoException">Com todos os problemas encontrados.</exception>
    public static CatalogoDeCondutores Carregar(string json)
    {
        var arquivo = RegrasDeCatalogo.Desserializar(json, CatalogoDeProdutoJsonContexto.Default.ArquivoDeCatalogoDeCondutores);
        var problemas = new List<string>();
        var (ficticio, referencia, pendente) = RegrasDeCatalogo.Validar(arquivo.Meta, arquivo.Catalogo, "condutores", arquivo.Ref, arquivo.Tipos?.Count ?? 0, problemas);

        var diametros = new Dictionary<string, IReadOnlyDictionary<decimal, decimal>>(StringComparer.Ordinal);
        foreach (var tipo in arquivo.Tipos ?? [])
        {
            if (string.IsNullOrWhiteSpace(tipo.Tipo))
            {
                problemas.Add("tipo de condutor sem nome");
                continue;
            }

            if (diametros.ContainsKey(tipo.Tipo)) problemas.Add($"tipo de condutor '{tipo.Tipo}' repetido");
            diametros[tipo.Tipo] = RegrasDeCatalogo.PorSecao(tipo.Tipo, tipo.DiametroExternoMmPorSecaoMm2, problemas);
        }

        if (problemas.Count > 0) throw new CatalogoDeProdutoInvalidoException(problemas);
        return new CatalogoDeCondutores(ficticio, referencia, pendente, diametros);
    }

    public DadoNormativo<decimal> DiametroExternoMm(string tipo, decimal secaoMm2)
    {
        if (_pendente) return DadoNormativo<decimal>.Ausente(RegrasDeCatalogo.TodoCatalogo, "catálogo de condutores sem dados (TODO_CATALOGO)");
        if (!_diametros.TryGetValue(tipo, out var porSecao))
            return DadoNormativo<decimal>.Ausente(_referencia, $"tipo de condutor '{tipo}' fora do catálogo");

        return porSecao.TryGetValue(secaoMm2, out var diametro)
            ? DadoNormativo<decimal>.Com(diametro, _referencia)
            : DadoNormativo<decimal>.Ausente(_referencia, $"sem diâmetro para {PerfilNormativo.Numero(secaoMm2)} mm² em '{tipo}'");
    }
}

/// <summary>
///     Catálogo de eletrodutos: tamanhos nominais e diâmetros internos por tipo (para a ocupação).
/// </summary>
/// <remarks>Mesma disciplina do catálogo de condutores; o oficial está vazio até a escolha do fabricante.</remarks>
public sealed class CatalogoDeEletrodutos
{
    private static readonly Lazy<CatalogoDeEletrodutos> Oficial =
        new(() => Carregar(RegrasDeCatalogo.LerRecurso("Ampere.Core.Catalogos.eletrodutos.json")));

    private readonly string _referencia;
    private readonly bool _pendente;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<TamanhoDeEletroduto>> _tamanhos;

    private CatalogoDeEletrodutos(bool ficticio, string referencia, bool pendente, IReadOnlyDictionary<string, IReadOnlyList<TamanhoDeEletroduto>> tamanhos)
    {
        Ficticio = ficticio;
        _referencia = referencia;
        _pendente = pendente;
        _tamanhos = tamanhos;
    }

    /// <summary>Catálogo só para testes.</summary>
    public bool Ficticio { get; }

    /// <summary>Catálogo oficial, embarcado no assembly.</summary>
    public static CatalogoDeEletrodutos Padrao => Oficial.Value;

    /// <exception cref="CatalogoDeProdutoInvalidoException">Com todos os problemas encontrados.</exception>
    public static CatalogoDeEletrodutos Carregar(string json)
    {
        var arquivo = RegrasDeCatalogo.Desserializar(json, CatalogoDeProdutoJsonContexto.Default.ArquivoDeCatalogoDeEletrodutos);
        var problemas = new List<string>();
        var (ficticio, referencia, pendente) = RegrasDeCatalogo.Validar(arquivo.Meta, arquivo.Catalogo, "eletrodutos", arquivo.Ref, arquivo.Tipos?.Count ?? 0, problemas);

        var tamanhos = new Dictionary<string, IReadOnlyList<TamanhoDeEletroduto>>(StringComparer.Ordinal);
        foreach (var tipo in arquivo.Tipos ?? [])
        {
            if (string.IsNullOrWhiteSpace(tipo.Tipo))
            {
                problemas.Add("tipo de eletroduto sem nome");
                continue;
            }

            if (tamanhos.ContainsKey(tipo.Tipo)) problemas.Add($"tipo de eletroduto '{tipo.Tipo}' repetido");
            var lidos = new List<TamanhoDeEletroduto>();
            foreach (var tamanho in tipo.Tamanhos ?? [])
            {
                if (string.IsNullOrWhiteSpace(tamanho.Nominal)) problemas.Add($"{tipo.Tipo}: tamanho sem nome nominal");
                else if (lidos.Any(lido => lido.Nominal == tamanho.Nominal)) problemas.Add($"{tipo.Tipo}: tamanho nominal '{tamanho.Nominal}' repetido");
                if (tamanho.DiametroInternoMm is not > 0) problemas.Add($"{tipo.Tipo}: diâmetro interno não positivo em '{tamanho.Nominal}'");
                lidos.Add(new TamanhoDeEletroduto(tamanho.Nominal ?? string.Empty, tamanho.DiametroInternoMm ?? 0m));
            }

            if (lidos.Count == 0) problemas.Add($"{tipo.Tipo}: sem tamanhos");
            tamanhos[tipo.Tipo] = lidos.OrderBy(tamanho => tamanho.DiametroInternoMm).ThenBy(tamanho => tamanho.Nominal, StringComparer.Ordinal).ToList();
        }

        if (problemas.Count > 0) throw new CatalogoDeProdutoInvalidoException(problemas);
        return new CatalogoDeEletrodutos(ficticio, referencia, pendente, tamanhos);
    }

    /// <summary>Tamanhos do tipo, em ordem crescente de diâmetro interno.</summary>
    public DadoNormativo<IReadOnlyList<TamanhoDeEletroduto>> Tamanhos(string tipo)
    {
        if (_pendente)
            return DadoNormativo<IReadOnlyList<TamanhoDeEletroduto>>.Ausente(RegrasDeCatalogo.TodoCatalogo, "catálogo de eletrodutos sem dados (TODO_CATALOGO)");

        return _tamanhos.TryGetValue(tipo, out var tamanhos)
            ? DadoNormativo<IReadOnlyList<TamanhoDeEletroduto>>.Com(tamanhos, _referencia)
            : DadoNormativo<IReadOnlyList<TamanhoDeEletroduto>>.Ausente(_referencia, $"tipo de eletroduto '{tipo}' fora do catálogo");
    }
}

/// <summary>Catálogos de fabricante usados pelo dimensionamento.</summary>
public sealed record CatalogosDeProduto(CatalogoDeCondutores Condutores, CatalogoDeEletrodutos Eletrodutos)
{
    /// <summary>Catálogos oficiais, embarcados no assembly.</summary>
    public static CatalogosDeProduto Padrao => new(CatalogoDeCondutores.Padrao, CatalogoDeEletrodutos.Padrao);
}

/// <summary>Tamanho nominal de eletroduto (ex.: 3/4") e seu diâmetro interno, em mm.</summary>
public sealed record TamanhoDeEletroduto(string Nominal, decimal DiametroInternoMm);

/// <summary>O catálogo viola alguma regra. Traz todos os problemas encontrados.</summary>
public sealed class CatalogoDeProdutoInvalidoException(IReadOnlyList<string> problemas)
    : Exception("Catálogo inválido:\n" + string.Join("\n", problemas.Select(problema => "- " + problema)))
{
    public IReadOnlyList<string> Problemas { get; } = problemas;
}

/// <summary>Regras comuns aos catálogos: $meta, ref x valores, fictício x real, chaves e valores numéricos.</summary>
internal static class RegrasDeCatalogo
{
    public const string TodoCatalogo = "TODO_CATALOGO";

    public static T Desserializar<T>(string json, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> tipo)
    {
        try
        {
            return JsonSerializer.Deserialize(json, tipo) ?? throw new CatalogoDeProdutoInvalidoException(["JSON vazio"]);
        }
        catch (JsonException excecao)
        {
            throw new CatalogoDeProdutoInvalidoException([$"JSON inválido: {excecao.Message}"]);
        }
    }

    public static (bool Ficticio, string Referencia, bool Pendente) Validar(
        MetaDoCatalogo? meta, string? catalogo, string esperado, string? referencia, int quantidadeDeTipos, List<string> problemas)
    {
        if (meta is null) problemas.Add("$meta ausente");
        else if (meta.Ficticio is null) problemas.Add("$meta.ficticio ausente (true ou false)");
        if (catalogo != esperado) problemas.Add($"catálogo '{catalogo}' não é de {esperado}");

        var lida = referencia?.Trim() ?? string.Empty;
        if (lida.Length == 0) problemas.Add("sem ref (fabricante e edição, ou TODO_CATALOGO)");
        var pendente = lida == TodoCatalogo;
        if (pendente && quantidadeDeTipos > 0) problemas.Add("tem valores mas a ref é TODO_CATALOGO (valor sem fonte)");
        if (!pendente && lida.Length > 0 && quantidadeDeTipos == 0) problemas.Add($"ref '{lida}' sem valores");

        var ficticio = meta?.Ficticio ?? false;
        var citaFicticio = Normalizar(lida).Contains("FICTICIO");
        if (ficticio && !citaFicticio) problemas.Add("catálogo fictício precisa dizer 'FICTÍCIO' na ref");
        if (!ficticio && citaFicticio) problemas.Add("catálogo real com referência fictícia");

        return (ficticio, pendente ? TodoCatalogo : lida, pendente || lida.Length == 0);
    }

    public static IReadOnlyDictionary<decimal, decimal> PorSecao(string tipo, Dictionary<string, decimal>? valores, List<string> problemas)
    {
        var resultado = new Dictionary<decimal, decimal>();
        foreach (var (chave, valor) in valores ?? [])
        {
            if (!decimal.TryParse(chave, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var secao))
                problemas.Add($"{tipo}: chave numérica inválida '{chave}'");
            else if (valor <= 0)
                problemas.Add($"{tipo}: diâmetro não positivo em {chave} mm²");
            else
                resultado[secao] = valor;
        }

        if (resultado.Count == 0) problemas.Add($"{tipo}: sem diâmetros");
        return resultado;
    }

    public static string LerRecurso(string nome)
    {
        using var recurso = typeof(RegrasDeCatalogo).Assembly.GetManifestResourceStream(nome)
                            ?? throw new InvalidOperationException($"Recurso embarcado '{nome}' não encontrado.");
        using var leitor = new StreamReader(recurso, Encoding.UTF8);
        return leitor.ReadToEnd();
    }

    private static string Normalizar(string texto) =>
        new(texto.Normalize(NormalizationForm.FormD).Where(caractere => CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
            .Select(char.ToUpperInvariant).ToArray());
}

internal sealed record MetaDoCatalogo(string? Fonte, string? Versao, string? Data, bool? Ficticio, string? Situacao);

internal sealed record ArquivoDeCatalogoDeCondutores(
    [property: JsonPropertyName("$meta")] MetaDoCatalogo? Meta,
    string? Catalogo,
    string? Ref,
    List<TipoDeCondutorJson>? Tipos);

internal sealed record TipoDeCondutorJson(string? Tipo, Dictionary<string, decimal>? DiametroExternoMmPorSecaoMm2);

internal sealed record ArquivoDeCatalogoDeEletrodutos(
    [property: JsonPropertyName("$meta")] MetaDoCatalogo? Meta,
    string? Catalogo,
    string? Ref,
    List<TipoDeEletrodutoJson>? Tipos);

internal sealed record TipoDeEletrodutoJson(string? Tipo, List<TamanhoJson>? Tamanhos);

internal sealed record TamanhoJson(string? Nominal, decimal? DiametroInternoMm);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ArquivoDeCatalogoDeCondutores))]
[JsonSerializable(typeof(ArquivoDeCatalogoDeEletrodutos))]
internal sealed partial class CatalogoDeProdutoJsonContexto : JsonSerializerContext;
