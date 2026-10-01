using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ampere.Core.Surtos;

/// <summary>Esquemas de aterramento que a seleção do DPS conhece (NBR 5410, 4.2.2.2 e Tabela 49).</summary>
public static class EsquemasDeAterramento
{
    public const string TnS = "TN-S";
    public const string TnC = "TN-C";

    /// <summary>Entra TN-C e passa a TN-S no quadro de distribuição principal (Figura 13, nota b).</summary>
    public const string TnCS = "TN-C-S";

    public const string Tt = "TT";
    public const string ItComNeutro = "IT com neutro";
    public const string ItSemNeutro = "IT sem neutro";

    public static IReadOnlyList<string> Todos { get; } = [TnS, TnCS, TnC, Tt, ItComNeutro, ItSemNeutro];
}

/// <summary>Ligações de um DPS na Tabela 49 e na Figura 13.</summary>
public static class LigacoesDoDps
{
    public const string FaseNeutro = "fase-neutro";
    public const string FasePe = "fase-PE";
    public const string FasePen = "fase-PEN";
    public const string NeutroPe = "neutro-PE";

    internal static IReadOnlyList<string> Todas { get; } = [FaseNeutro, FasePe, FasePen, NeutroPe];

    /// <summary>Para leitura (ex.: "fase–PE").</summary>
    public static string Descrever(string ligacao) => ligacao.Replace('-', '–');
}

/// <summary>Uc mínimo de uma célula da Tabela 49: um fator sobre Uo (fase-neutro) ou sobre U (entre fases).</summary>
/// <param name="Texto">Como impresso (ex.: "1,1 Uo", "√3 Uo", "U").</param>
public sealed record UcDaTabela(string Texto, decimal Fator, bool EntreFases)
{
    private const decimal Raiz3 = 1.7320508075688772935274463415m;

    /// <summary>A expressão da memória (ex.: "Uc ≥ 1,1 · Uo").</summary>
    public string Expressao => Fator == 1m ? $"Uc ≥ {Base}" : $"Uc ≥ {FatorEmTexto} · {Base}";

    public string Base => EntreFases ? "U" : "Uo";

    private string FatorEmTexto => Fator == Raiz3 ? "√3" : NumeroEmTexto.FormatarParaLeitura(Fator);

    /// <summary>"1,1 Uo", "√3 Uo", "Uo" ou "U" (fator com vírgula decimal); nulo se ilegível.</summary>
    public static UcDaTabela? Ler(string texto)
    {
        var partes = texto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var (fator, tensao) = partes.Length switch
        {
            1 => ("1", partes[0]),
            2 => (partes[0], partes[1]),
            _ => (null, null)
        };
        if (tensao is not ("Uo" or "U")) return null;
        decimal? valor = fator switch
        {
            "√3" => Raiz3,
            null => null,
            _ => decimal.TryParse(fator, NumberStyles.AllowDecimalPoint, CultureInfo.GetCultureInfo("pt-BR"), out var lido) && lido > 0m ? lido : null
        };
        return valor is { } positivo ? new UcDaTabela(texto.Trim(), positivo, tensao == "U") : null;
    }
}

/// <summary>Uma linha da Tabela 31: as tensões nominais da linha e a tensão de impulso por categoria, em kV.</summary>
/// <param name="Sistemas">Como impresso: trifásicos "Uo/U" e monofásicos com neutro "Uo-U".</param>
/// <param name="TensoesFaseNeutroV">Uo de cada sistema da linha (as linhas da tabela não repetem Uo).</param>
public sealed record LinhaDaTabela31(IReadOnlyList<string> Sistemas, IReadOnlyList<decimal> TensoesFaseNeutroV, decimal CategoriaIKv, decimal CategoriaIiKv,
    decimal CategoriaIiiKv, decimal CategoriaIvKv);

/// <summary>Os valores mínimos de corrente de um DPS: por modo de proteção e, no esquema 3, o do neutro–PE.</summary>
public sealed record CorrenteDoDps(string Referencia, decimal PorModoKa, decimal NeutroPeTrifasicaKa, decimal NeutroPeMonofasicaKa);

/// <summary>Uma exigência que o Ampere não confere (depende do produto): o projetista confere no catálogo.</summary>
public sealed record ExigenciaAConferir(string Referencia, string Texto);

/// <summary>
///     Seleção dos DPS da linha de energia pela NBR 5410 (6.3.5.2), carregada de <c>data/normas/NBR5410/2004/dps.json</c>:
///     valores com a referência, nunca no código. Como o perfil normativo, o carregador recusa arquivo malformado.
/// </summary>
public sealed class NormaDeDps
{
    private static readonly Lazy<NormaDeDps> Nbr5410 = new(() => Carregar(LerRecurso("Ampere.Core.Surtos.NBR5410_2004.dps.json")));

    private NormaDeDps(ArquivoDeDps arquivo, IReadOnlyDictionary<(string Ligacao, string Esquema), UcDaTabela> uc, IReadOnlyList<LinhaDaTabela31> tabela31)
    {
        Nome = arquivo.Perfil!;
        Fonte = arquivo.Meta!.Fonte!;
        Situacao = arquivo.Meta.Situacao ?? string.Empty;
        ReferenciaDaLocalizacao = arquivo.Localizacao!.Ref!;
        LocalizacaoDaLinhaExterna = arquivo.Localizacao.LinhaExterna!;
        LocalizacaoDasDescargasDiretas = arquivo.Localizacao.DescargasDiretas!;
        ReferenciaDoEsquemaDeConexao = arquivo.EsquemaDeConexao!.Ref!;
        ReferenciaDoEsquema3Obrigatorio = arquivo.EsquemaDeConexao.Esquema3Obrigatorio!;
        ReferenciaDoUc = arquivo.UcMinimo!.Ref!;
        Uc = uc;
        ReferenciaDoNivelDeProtecao = arquivo.NivelDeProtecao!.Ref!;
        Tabela31 = tabela31;
        CorrenteNominalDeDescarga = Corrente(arquivo.CorrenteNominalDeDescarga!);
        CorrenteDeImpulso = Corrente(arquivo.CorrenteDeImpulso!);
        ReferenciaDaCorrenteSubsequente = arquivo.CorrenteSubsequenteNeutroPe!.Ref!;
        CorrenteSubsequenteMinimaA = arquivo.CorrenteSubsequenteNeutroPe.MinimaA!.Value;
        ReferenciaDoCondutorDeConexao = arquivo.CondutorDeConexao!.Ref!;
        SecaoDoCondutorDeConexaoMm2 = arquivo.CondutorDeConexao.SecaoMinimaCobreMm2!.Value;
        SecaoDoCondutorDeConexaoDescargasDiretasMm2 = arquivo.CondutorDeConexao.SecaoMinimaCobreDescargasDiretasMm2!.Value;
        ComprimentoMaximoDaConexaoM = arquivo.CondutorDeConexao.ComprimentoMaximoM!.Value;
        ReferenciaDaImunidadeDoDr = arquivo.ImunidadeDoDr!.Ref!;
        ImunidadeDoDrKa = arquivo.ImunidadeDoDr.MinimaKa!.Value;
        AConferir = arquivo.AConferir!.Select(item => new ExigenciaAConferir(item.Ref!.Trim(), item.Texto!.Trim())).ToList();
    }

    /// <summary>A da NBR 5410:2004, embarcada no assembly.</summary>
    public static NormaDeDps NBR5410_2004 => Nbr5410.Value;

    /// <summary>Perfil normativo da memória (ex.: "NBR5410:2004").</summary>
    public string Nome { get; }

    public string Fonte { get; }

    public string Situacao { get; }

    public string ReferenciaDaLocalizacao { get; }

    public string LocalizacaoDaLinhaExterna { get; }

    public string LocalizacaoDasDescargasDiretas { get; }

    public string ReferenciaDoEsquemaDeConexao { get; }

    public string ReferenciaDoEsquema3Obrigatorio { get; }

    public string ReferenciaDoUc { get; }

    /// <summary>Tabela 49: Uc mínimo por (ligação, coluna do esquema de aterramento); célula vazia = não se aplica.</summary>
    public IReadOnlyDictionary<(string Ligacao, string Esquema), UcDaTabela> Uc { get; }

    public string ReferenciaDoNivelDeProtecao { get; }

    public IReadOnlyList<LinhaDaTabela31> Tabela31 { get; }

    public CorrenteDoDps CorrenteNominalDeDescarga { get; }

    public CorrenteDoDps CorrenteDeImpulso { get; }

    public string ReferenciaDaCorrenteSubsequente { get; }

    public decimal CorrenteSubsequenteMinimaA { get; }

    public string ReferenciaDoCondutorDeConexao { get; }

    public decimal SecaoDoCondutorDeConexaoMm2 { get; }

    public decimal SecaoDoCondutorDeConexaoDescargasDiretasMm2 { get; }

    public decimal ComprimentoMaximoDaConexaoM { get; }

    public string ReferenciaDaImunidadeDoDr { get; }

    public decimal ImunidadeDoDrKa { get; }

    public IReadOnlyList<ExigenciaAConferir> AConferir { get; }

    /// <summary>A linha da Tabela 31 da tensão fase-neutro (arredondada ao volt), ou nula.</summary>
    public LinhaDaTabela31? LinhaDaTensao(decimal tensaoFaseNeutroV)
    {
        var uo = Math.Round(tensaoFaseNeutroV, 0, MidpointRounding.AwayFromZero);
        return Tabela31.FirstOrDefault(linha => linha.TensoesFaseNeutroV.Contains(uo));
    }

    /// <exception cref="NormaDeDpsInvalidaException">Com todos os problemas encontrados.</exception>
    public static NormaDeDps Carregar(string json)
    {
        ArquivoDeDps? arquivo;
        try
        {
            arquivo = JsonSerializer.Deserialize(json, DpsJsonContexto.Default.ArquivoDeDps);
        }
        catch (JsonException excecao)
        {
            throw new NormaDeDpsInvalidaException([$"JSON inválido: {excecao.Message}"]);
        }

        if (arquivo is null) throw new NormaDeDpsInvalidaException(["arquivo vazio"]);
        var problemas = new List<string>();
        if (arquivo.Meta?.Fonte is not { Length: > 0 } || arquivo.Meta.Ficticio is null) problemas.Add("$meta sem fonte ou sem ficticio");
        if (string.IsNullOrWhiteSpace(arquivo.Perfil)) problemas.Add("sem perfil");
        if (string.IsNullOrWhiteSpace(arquivo.Localizacao?.Ref) || string.IsNullOrWhiteSpace(arquivo.Localizacao.LinhaExterna)
                                                              || string.IsNullOrWhiteSpace(arquivo.Localizacao.DescargasDiretas))
            problemas.Add("localizacao sem ref ou sem os textos das alíneas");
        if (string.IsNullOrWhiteSpace(arquivo.EsquemaDeConexao?.Ref) || string.IsNullOrWhiteSpace(arquivo.EsquemaDeConexao.Esquema3Obrigatorio))
            problemas.Add("esquema_de_conexao sem ref");

        var uc = new Dictionary<(string, string), UcDaTabela>();
        if (string.IsNullOrWhiteSpace(arquivo.UcMinimo?.Ref)) problemas.Add("uc_minimo sem ref");
        foreach (var linha in arquivo.UcMinimo?.Linhas ?? [])
        {
            var onde = $"uc_minimo, {linha.Ligacao} no {linha.Esquema}";
            if (linha.Ligacao is not { } ligacao || !LigacoesDoDps.Todas.Contains(ligacao, StringComparer.Ordinal))
                problemas.Add($"{onde}: ligação fora de {string.Join(", ", LigacoesDoDps.Todas)}");
            else if (linha.Esquema is not { } esquema || esquema == EsquemasDeAterramento.TnCS || !EsquemasDeAterramento.Todos.Contains(esquema, StringComparer.Ordinal))
                problemas.Add($"{onde}: esquema fora das colunas da Tabela 49");
            else if (UcDaTabela.Ler(linha.Uc ?? string.Empty) is not { } valor) problemas.Add($"{onde}: Uc '{linha.Uc}' ilegível (ex.: \"1,1 Uo\", \"√3 Uo\", \"U\")");
            else if (!uc.TryAdd((ligacao, esquema), valor)) problemas.Add($"{onde}: repetida");
        }

        if (uc.Count == 0) problemas.Add("uc_minimo sem linhas");

        var tabela31 = new List<LinhaDaTabela31>();
        if (string.IsNullOrWhiteSpace(arquivo.NivelDeProtecao?.Ref)) problemas.Add("nivel_de_protecao sem ref");
        foreach (var linha in arquivo.NivelDeProtecao?.Linhas ?? [])
        {
            var sistemas = (linha.SistemasTrifasicos ?? []).Select(sistema => (Texto: sistema, Separador: '/'))
                .Concat((linha.SistemasMonofasicos ?? []).Select(sistema => (Texto: sistema, Separador: '-')))
                .ToList();
            var onde = $"nivel_de_protecao, {string.Join(", ", sistemas.Select(sistema => sistema.Texto))}";
            var tensoes = sistemas.Select(sistema => FaseNeutro(sistema.Texto, sistema.Separador)).ToList();
            if (sistemas.Count == 0 || tensoes.Any(tensao => tensao is null)) problemas.Add($"{onde}: sistemas ausentes ou ilegíveis (\"Uo/U\" ou \"Uo-U\")");
            if (linha.CategoriaIKv is not > 0m || linha.CategoriaIiKv is not > 0m || linha.CategoriaIiiKv is not > 0m || linha.CategoriaIvKv is not > 0m)
                problemas.Add($"{onde}: tensões de impulso precisam ser positivas");
            var uos = tensoes.OfType<decimal>().Distinct().ToList();
            if (tabela31.SelectMany(lida => lida.TensoesFaseNeutroV).Intersect(uos).Any()) problemas.Add($"{onde}: Uo repetido em outra linha");
            tabela31.Add(new LinhaDaTabela31(sistemas.Select(sistema => sistema.Texto.Trim()).ToList(), uos, linha.CategoriaIKv ?? 0m, linha.CategoriaIiKv ?? 0m,
                linha.CategoriaIiiKv ?? 0m, linha.CategoriaIvKv ?? 0m));
        }

        if (tabela31.Count == 0) problemas.Add("nivel_de_protecao sem linhas");
        ValidarCorrente(arquivo.CorrenteNominalDeDescarga, "corrente_nominal_de_descarga", problemas);
        ValidarCorrente(arquivo.CorrenteDeImpulso, "corrente_de_impulso", problemas);
        if (string.IsNullOrWhiteSpace(arquivo.CorrenteSubsequenteNeutroPe?.Ref) || arquivo.CorrenteSubsequenteNeutroPe.MinimaA is not > 0m)
            problemas.Add("corrente_subsequente_neutro_pe sem ref ou sem valor positivo");
        if (string.IsNullOrWhiteSpace(arquivo.CondutorDeConexao?.Ref) || arquivo.CondutorDeConexao.SecaoMinimaCobreMm2 is not > 0m
                                                                     || arquivo.CondutorDeConexao.SecaoMinimaCobreDescargasDiretasMm2 is not > 0m
                                                                     || arquivo.CondutorDeConexao.ComprimentoMaximoM is not > 0m)
            problemas.Add("condutor_de_conexao sem ref ou sem valores positivos");
        if (string.IsNullOrWhiteSpace(arquivo.ImunidadeDoDr?.Ref) || arquivo.ImunidadeDoDr.MinimaKa is not > 0m)
            problemas.Add("imunidade_do_dr sem ref ou sem valor positivo");
        if (arquivo.AConferir is not { Count: > 0 } || arquivo.AConferir.Any(item => string.IsNullOrWhiteSpace(item.Ref) || string.IsNullOrWhiteSpace(item.Texto)))
            problemas.Add("a_conferir vazio ou com item sem ref ou sem texto");

        // Toda referência é do texto oficial: TODO_NORMA é valor sem fonte, e o arquivo real não cita fonte fictícia.
        string?[] referencias =
        [
            arquivo.Localizacao?.Ref, arquivo.EsquemaDeConexao?.Ref, arquivo.EsquemaDeConexao?.Esquema3Obrigatorio, arquivo.UcMinimo?.Ref,
            arquivo.NivelDeProtecao?.Ref, arquivo.CorrenteNominalDeDescarga?.Ref, arquivo.CorrenteDeImpulso?.Ref, arquivo.CorrenteSubsequenteNeutroPe?.Ref,
            arquivo.CondutorDeConexao?.Ref, arquivo.ImunidadeDoDr?.Ref, .. (arquivo.AConferir ?? []).Select(item => item.Ref)
        ];
        if (referencias.Any(referencia => referencia?.Trim() == "TODO_NORMA")) problemas.Add("referência TODO_NORMA (valor sem fonte)");
        if (arquivo.Meta?.Ficticio == false && referencias.Any(referencia => referencia is not null && Normalizar(referencia).Contains("FICTICIO")))
            problemas.Add("arquivo real com referência fictícia");

        if (problemas.Count > 0) throw new NormaDeDpsInvalidaException(problemas);
        return new NormaDeDps(arquivo, uc, tabela31);
    }

    private static string Normalizar(string texto) =>
        new(texto.Normalize(NormalizationForm.FormD).Where(caractere => CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
            .Select(char.ToUpperInvariant).ToArray());

    private static decimal? FaseNeutro(string sistema, char separador)
    {
        var partes = sistema.Split(separador);
        return partes.Length == 2
               && decimal.TryParse(partes[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var uo)
               && decimal.TryParse(partes[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var u) && uo > 0m && u > uo
            ? uo
            : null;
    }

    private static void ValidarCorrente(CorrenteDoDpsJson? corrente, string nome, List<string> problemas)
    {
        if (string.IsNullOrWhiteSpace(corrente?.Ref) || corrente.PorModoKa is not > 0m || corrente.NeutroPeEsquema3TrifasicaKa is not > 0m
            || corrente.NeutroPeEsquema3MonofasicaKa is not > 0m)
            problemas.Add($"{nome} sem ref ou sem valores positivos");
    }

    private static CorrenteDoDps Corrente(CorrenteDoDpsJson corrente) =>
        new(corrente.Ref!.Trim(), corrente.PorModoKa!.Value, corrente.NeutroPeEsquema3TrifasicaKa!.Value, corrente.NeutroPeEsquema3MonofasicaKa!.Value);

    private static string LerRecurso(string nome)
    {
        using var recurso = typeof(NormaDeDps).Assembly.GetManifestResourceStream(nome)
                            ?? throw new InvalidOperationException($"Recurso embarcado '{nome}' não encontrado.");
        using var leitor = new StreamReader(recurso, Encoding.UTF8);
        return leitor.ReadToEnd();
    }
}

/// <summary>O arquivo dos DPS viola alguma regra. Traz todos os problemas encontrados.</summary>
public sealed class NormaDeDpsInvalidaException(IReadOnlyList<string> problemas)
    : Exception("Dados dos DPS inválidos:\n" + string.Join("\n", problemas.Select(problema => "- " + problema)))
{
    public IReadOnlyList<string> Problemas { get; } = problemas;
}

// Formato em disco (tudo anulável: a validação relata o que faltar).
internal sealed record ArquivoDeDps(
    [property: JsonPropertyName("$meta")] MetaDeDps? Meta,
    string? Perfil,
    LocalizacaoDoDpsJson? Localizacao,
    EsquemaDeConexaoJson? EsquemaDeConexao,
    UcMinimoJson? UcMinimo,
    NivelDeProtecaoJson? NivelDeProtecao,
    CorrenteDoDpsJson? CorrenteNominalDeDescarga,
    CorrenteDoDpsJson? CorrenteDeImpulso,
    CorrenteSubsequenteJson? CorrenteSubsequenteNeutroPe,
    CondutorDeConexaoJson? CondutorDeConexao,
    ImunidadeDoDrJson? ImunidadeDoDr,
    List<ExigenciaJson>? AConferir);

internal sealed record MetaDeDps(string? Fonte, string? Versao, string? Data, bool? Ficticio, string? Situacao);

internal sealed record LocalizacaoDoDpsJson(string? Ref, string? LinhaExterna, string? DescargasDiretas);

internal sealed record EsquemaDeConexaoJson(string? Ref, [property: JsonPropertyName("esquema_3_obrigatorio")] string? Esquema3Obrigatorio);

internal sealed record UcMinimoJson(string? Ref, List<CelulaDaTabela49Json>? Linhas);

internal sealed record CelulaDaTabela49Json(string? Ligacao, string? Esquema, string? Uc);

internal sealed record NivelDeProtecaoJson(string? Ref, List<LinhaDaTabela31Json>? Linhas);

internal sealed record LinhaDaTabela31Json(
    List<string>? SistemasTrifasicos,
    List<string>? SistemasMonofasicos,
    decimal? CategoriaIvKv,
    decimal? CategoriaIiiKv,
    decimal? CategoriaIiKv,
    decimal? CategoriaIKv);

internal sealed record CorrenteDoDpsJson(
    string? Ref,
    decimal? PorModoKa,
    [property: JsonPropertyName("neutro_pe_esquema_3_trifasica_ka")] decimal? NeutroPeEsquema3TrifasicaKa,
    [property: JsonPropertyName("neutro_pe_esquema_3_monofasica_ka")] decimal? NeutroPeEsquema3MonofasicaKa);

internal sealed record CorrenteSubsequenteJson(string? Ref, decimal? MinimaA);

internal sealed record CondutorDeConexaoJson(string? Ref, decimal? SecaoMinimaCobreMm2, decimal? SecaoMinimaCobreDescargasDiretasMm2, decimal? ComprimentoMaximoM);

internal sealed record ImunidadeDoDrJson(string? Ref, decimal? MinimaKa);

internal sealed record ExigenciaJson(string? Ref, string? Texto);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ArquivoDeDps))]
internal sealed partial class DpsJsonContexto : JsonSerializerContext;
