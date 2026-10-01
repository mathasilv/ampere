using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ampere.Core.Cargas;

namespace Ampere.Core.Demanda;

/// <summary>Faixa de uma tabela de demanda: vale acima da faixa anterior e até <paramref name="Ate" />, inclusive; nulo = acima da última.</summary>
public sealed record FaixaDeDemanda(decimal? Ate, decimal FatorPct);

/// <summary>Tabela de faixas (por quantidade de aparelhos ou por potência), com a referência do documento.</summary>
public sealed record TabelaDeFaixas(string Referencia, IReadOnlyList<FaixaDeDemanda> Faixas)
{
    /// <summary>A faixa em que o valor cai (a primeira com <c>Ate</c> ≥ valor, ou a última, aberta).</summary>
    public FaixaDeDemanda Para(decimal valor) => Faixas.First(faixa => faixa.Ate is null || valor <= faixa.Ate);
}

/// <summary>
///     Linha da tabela de iluminação e tomadas de uso geral: fator único, escalonado (ex.: 100% nos primeiros 12 kW, 50% no
///     que exceder) ou por faixa da potência total (residências).
/// </summary>
/// <param name="Escalonado">Cada faixa é a parcela da potência até <c>Ate</c> kW (acumulado), com o seu fator.</param>
/// <param name="PorFaixa">O fator da faixa em que a potência total cai vale para a potência toda.</param>
public sealed record EdificacaoDeDemanda(
    string Nome,
    decimal CargaMinimaWm2,
    decimal? FatorPct,
    IReadOnlyList<FaixaDeDemanda>? Escalonado,
    IReadOnlyList<FaixaDeDemanda>? PorFaixa,
    bool Residencial);

/// <summary>Coluna da tabela de aparelhos residenciais: os aparelhos que ela cobre e as faixas por quantidade.</summary>
public sealed record ColunaDeAparelhos(string Nome, IReadOnlyList<Aparelho> Aparelhos, IReadOnlyList<FaixaDeDemanda> Faixas);

/// <summary>
///     Regras de demanda de uma distribuidora (ex.: CELG CT 04/18): as tabelas do documento e a referência de cada uma,
///     carregadas de <c>data/distribuidoras</c>. Como o perfil normativo, o carregador recusa tabela malformada em vez de
///     adivinhar.
/// </summary>
public sealed class PerfilDeDemanda
{
    private static readonly Lazy<PerfilDeDemanda> CelgCt0418 =
        new(() => Carregar(LerRecurso("Ampere.Core.Demanda.CELG_CT-04-2018.demanda.json")));

    private PerfilDeDemanda(ArquivoDeDemanda arquivo, IReadOnlyList<EdificacaoDeDemanda> edificacoes, IReadOnlyList<ColunaDeAparelhos> colunas)
    {
        Nome = arquivo.Nome!;
        Fonte = arquivo.Meta!.Fonte!;
        Situacao = arquivo.Meta.Situacao ?? string.Empty;
        ReferenciaDaFormula = arquivo.Formula!.Ref!;
        Expressao = arquivo.Formula.Expressao!;
        ReferenciaDaIluminacao = arquivo.IluminacaoETug!.Ref!;
        Edificacoes = edificacoes;
        Aparelhos = colunas;
        ReferenciaDosAparelhos = arquivo.Aparelhos!.Ref!;
        ArCondicionadoResidencial = Tabela(arquivo.ArCondicionado!.Ref!, arquivo.ArCondicionado.Residencial!);
        ArCondicionadoComercial = Tabela(arquivo.ArCondicionado.Ref!, arquivo.ArCondicionado.Comercial!);
        LimiteDosFornosKw = arquivo.FornosEFogoes!.LimiteKw!.Value;
        FornosAteOLimite = Tabela(arquivo.FornosEFogoes.Ref!, arquivo.FornosEFogoes.AteOLimite!);
        FornosAcimaDoLimite = Tabela(arquivo.FornosEFogoes.Ref!, arquivo.FornosEFogoes.AcimaDoLimite!);
        Motores = new RegraDosMotores(arquivo.Motores!.Ref!, arquivo.Motores.Aplicacao!, arquivo.Motores.MaiorPct!.Value, arquivo.Motores.DemaisPct!.Value,
            arquivo.Motores.RefDoProjetista!);
        MaquinasDeSolda = new RegraDasMaquinasDeSolda(arquivo.MaquinasDeSolda!.Ref!, arquivo.MaquinasDeSolda.PorOrdemPct!, arquivo.MaquinasDeSolda.DemaisPct!.Value);
    }

    /// <summary>A CT 04/18 da CELG (hoje Equatorial Goiás), embarcada no assembly.</summary>
    public static PerfilDeDemanda CelgCt04_18 => CelgCt0418.Value;

    /// <summary>Nome curto, gravado na memória (ex.: "CELG CT 04/18").</summary>
    public string Nome { get; }

    public string Fonte { get; }

    /// <summary>Situação do documento (vigência, conferência), para o relatório.</summary>
    public string Situacao { get; }

    public string ReferenciaDaFormula { get; }

    /// <summary>A fórmula da demanda total como o documento a escreve.</summary>
    public string Expressao { get; }

    public string ReferenciaDaIluminacao { get; }

    /// <summary>Linhas da tabela de iluminação e tomadas, na ordem do documento.</summary>
    public IReadOnlyList<EdificacaoDeDemanda> Edificacoes { get; }

    public string ReferenciaDosAparelhos { get; }

    public IReadOnlyList<ColunaDeAparelhos> Aparelhos { get; }

    public TabelaDeFaixas ArCondicionadoResidencial { get; }

    public TabelaDeFaixas ArCondicionadoComercial { get; }

    /// <summary>Potência que separa as duas colunas dos fornos e fogões, em kW.</summary>
    public decimal LimiteDosFornosKw { get; }

    public TabelaDeFaixas FornosAteOLimite { get; }

    public TabelaDeFaixas FornosAcimaDoLimite { get; }

    public RegraDosMotores Motores { get; }

    public RegraDasMaquinasDeSolda MaquinasDeSolda { get; }

    /// <summary>A coluna da tabela de aparelhos que cobre o aparelho; nula se ele não está nela.</summary>
    public ColunaDeAparelhos? ColunaDe(Aparelho aparelho) => Aparelhos.FirstOrDefault(coluna => coluna.Aparelhos.Contains(aparelho));

    /// <exception cref="PerfilDeDemandaInvalidoException">Com todos os problemas encontrados.</exception>
    public static PerfilDeDemanda Carregar(string json)
    {
        ArquivoDeDemanda arquivo;
        try
        {
            arquivo = JsonSerializer.Deserialize(json, PerfilDeDemandaJsonContexto.Default.ArquivoDeDemanda)
                      ?? throw new PerfilDeDemandaInvalidoException(["JSON vazio"]);
        }
        catch (JsonException excecao)
        {
            throw new PerfilDeDemandaInvalidoException([$"JSON inválido: {excecao.Message}"]);
        }

        var problemas = new List<string>();
        if (arquivo.Meta is not { Fonte.Length: > 0, Versao.Length: > 0, Data.Length: > 0 }) problemas.Add("$meta sem fonte, versão ou data");
        if (string.IsNullOrWhiteSpace(arquivo.Nome)) problemas.Add("sem nome");
        if (arquivo.Formula is not { Ref.Length: > 0, Expressao.Length: > 0 }) problemas.Add("fórmula sem ref ou expressão");

        var edificacoes = LerEdificacoes(arquivo.IluminacaoETug, problemas);
        var colunas = LerColunas(arquivo.Aparelhos, problemas);
        if (arquivo.ArCondicionado is not { Ref.Length: > 0 }) problemas.Add("ar-condicionado sem ref");
        Faixas("ar-condicionado residencial", arquivo.ArCondicionado?.Residencial, problemas);
        Faixas("ar-condicionado comercial", arquivo.ArCondicionado?.Comercial, problemas);
        if (arquivo.FornosEFogoes is not { Ref.Length: > 0, LimiteKw: > 0m }) problemas.Add("fornos e fogões sem ref ou sem limite de potência positivo");
        Faixas("fornos e fogões até o limite", arquivo.FornosEFogoes?.AteOLimite, problemas);
        Faixas("fornos e fogões acima do limite", arquivo.FornosEFogoes?.AcimaDoLimite, problemas);
        if (arquivo.Motores is not { Ref.Length: > 0, Aplicacao.Length: > 0, RefDoProjetista.Length: > 0 } || !Fator(arquivo.Motores.MaiorPct) || !Fator(arquivo.Motores.DemaisPct))
            problemas.Add("motores sem ref, aplicação, ref do fator do projetista ou fatores em (0; 100]");
        if (arquivo.MaquinasDeSolda is not { Ref.Length: > 0, PorOrdemPct.Count: > 0 } || !arquivo.MaquinasDeSolda.PorOrdemPct.All(pct => Fator(pct))
            || !Fator(arquivo.MaquinasDeSolda.DemaisPct))
            problemas.Add("máquinas de solda sem ref ou com fatores fora de (0; 100]");

        if (problemas.Count > 0) throw new PerfilDeDemandaInvalidoException(problemas);
        return new PerfilDeDemanda(arquivo, edificacoes, colunas);
    }

    private static List<EdificacaoDeDemanda> LerEdificacoes(IluminacaoJson? tabela, List<string> problemas)
    {
        var lidas = new List<EdificacaoDeDemanda>();
        if (tabela is not { Ref.Length: > 0, Edificacoes.Count: > 0 })
        {
            problemas.Add("iluminação e tomadas sem ref ou sem edificações");
            return lidas;
        }

        foreach (var linha in tabela.Edificacoes)
        {
            var nome = linha.Nome?.Trim() ?? string.Empty;
            if (nome.Length == 0) problemas.Add("edificação sem nome");
            else if (lidas.Any(lida => lida.Nome == nome)) problemas.Add($"edificação '{nome}' repetida");
            var regras = (linha.FdPct is null ? 0 : 1) + (linha.Escalonado is null ? 0 : 1) + (linha.FaixasKw is null ? 0 : 1);
            if (regras != 1) problemas.Add($"{nome}: precisa de exatamente uma regra (fd_pct, escalonado ou faixas_kw)");
            if (linha.FdPct is { } fator && !Fator(fator)) problemas.Add($"{nome}: fator fora de (0; 100]");
            if (linha.CargaMinimaWM2 is not > 0m) problemas.Add($"{nome}: carga mínima não positiva");
            if (linha.FaixasKw is not null != (linha.Residencial == true)) problemas.Add($"{nome}: faixas por potência são das residências (residencial: true), e só delas");
            lidas.Add(new EdificacaoDeDemanda(nome, linha.CargaMinimaWM2 ?? 0m, linha.FdPct,
                linha.Escalonado is null ? null : Faixas($"{nome} (escalonado)", linha.Escalonado, problemas),
                linha.FaixasKw is null ? null : Faixas($"{nome} (faixas)", linha.FaixasKw, problemas),
                linha.Residencial == true));
        }

        if (lidas.Count(edificacao => edificacao.Residencial) != 1) problemas.Add("a tabela de iluminação precisa de uma (e só uma) linha residencial");
        return lidas;
    }

    // Os aparelhos que a fórmula conta pela tabela de aparelhos residenciais (b1 a b5, b7 e b8): cada um numa coluna só.
    private static readonly Aparelho[] DaTabelaDeAparelhos =
    [
        Aparelho.Chuveiro, Aparelho.Torneira, Aparelho.LavaLoucas, Aparelho.AquecedorDePassagem, Aparelho.AquecedorDeAcumulacao,
        Aparelho.SecadoraDeRoupa, Aparelho.MicroOndas
    ];

    private static List<ColunaDeAparelhos> LerColunas(AparelhosJson? tabela, List<string> problemas)
    {
        var colunas = new List<ColunaDeAparelhos>();
        if (tabela is not { Ref.Length: > 0, Colunas.Count: > 0 })
        {
            problemas.Add("aparelhos sem ref ou sem colunas");
            return colunas;
        }

        foreach (var coluna in tabela.Colunas)
        {
            var nome = coluna.Nome?.Trim() ?? string.Empty;
            if (nome.Length == 0) problemas.Add("coluna de aparelhos sem nome");
            var aparelhos = new List<Aparelho>();
            foreach (var codigo in coluna.Aparelhos ?? [])
            {
                if (!CodigosDeAparelho.TryLer(codigo, out var aparelho)) problemas.Add($"{nome}: aparelho '{codigo}' desconhecido");
                else if (!DaTabelaDeAparelhos.Contains(aparelho)) problemas.Add($"{nome}: '{codigo}' não é aparelho desta tabela");
                else aparelhos.Add(aparelho);
            }

            colunas.Add(new ColunaDeAparelhos(nome, aparelhos, Faixas(nome, coluna.Faixas, problemas)));
        }

        foreach (var aparelho in DaTabelaDeAparelhos)
        {
            var quantas = colunas.Count(coluna => coluna.Aparelhos.Contains(aparelho));
            if (quantas != 1) problemas.Add($"{CodigosDeAparelho.Codigo(aparelho)}: em {quantas} colunas da tabela de aparelhos (precisa de uma)");
        }

        return colunas;
    }

    // Faixas: 'até' crescente, a última aberta (nula), fatores em (0; 100].
    private static List<FaixaDeDemanda> Faixas(string tabela, List<List<decimal?>>? linhas, List<string> problemas)
    {
        var faixas = new List<FaixaDeDemanda>();
        if (linhas is not { Count: > 0 })
        {
            problemas.Add($"{tabela}: sem faixas");
            return faixas;
        }

        for (var indice = 0; indice < linhas.Count; indice++)
        {
            var linha = linhas[indice];
            if (linha.Count != 2 || linha[1] is not { } fator)
            {
                problemas.Add($"{tabela}: faixa {indice + 1} não é [até, fd %]");
                continue;
            }

            var ultima = indice == linhas.Count - 1;
            if (ultima != linha[0] is null) problemas.Add($"{tabela}: só a última faixa é aberta (até nulo)");
            if (!Fator(fator)) problemas.Add($"{tabela}: fator fora de (0; 100] na faixa {indice + 1}");
            if (linha[0] is { } ate && faixas.LastOrDefault()?.Ate is { } anterior && ate <= anterior) problemas.Add($"{tabela}: faixas fora de ordem ({anterior} e {ate})");
            if (linha[0] is <= 0m) problemas.Add($"{tabela}: faixa {indice + 1} com 'até' não positivo");
            faixas.Add(new FaixaDeDemanda(linha[0], fator));
        }

        return faixas;
    }

    private static TabelaDeFaixas Tabela(string referencia, List<List<decimal?>> linhas) =>
        new(referencia, linhas.Select(linha => new FaixaDeDemanda(linha[0], linha[1]!.Value)).ToList());

    private static bool Fator(decimal? pct) => pct is > 0m and <= 100m;

    private static string LerRecurso(string nome)
    {
        using var recurso = typeof(PerfilDeDemanda).Assembly.GetManifestResourceStream(nome)
                            ?? throw new InvalidOperationException($"Recurso embarcado '{nome}' não encontrado.");
        using var leitor = new StreamReader(recurso, Encoding.UTF8);
        return leitor.ReadToEnd();
    }
}

/// <summary>Motores pela regra do documento (ex.: d.1 da CT 04/18: 80% no maior, 50% nos demais).</summary>
/// <param name="Aplicacao">Onde o documento manda aplicar (ex.: edifícios residenciais de uso coletivo).</param>
/// <param name="ReferenciaDoProjetista">Onde o documento deixa o fator ao projetista (ex.: d.2 da CT 04/18).</param>
public sealed record RegraDosMotores(string Referencia, string Aplicacao, decimal MaiorPct, decimal DemaisPct, string ReferenciaDoProjetista);

/// <summary>Máquinas de solda a transformador: fator por ordem de potência (maior, segundo, terceiro) e o dos demais.</summary>
public sealed record RegraDasMaquinasDeSolda(string Referencia, IReadOnlyList<decimal> PorOrdemPct, decimal DemaisPct);

/// <summary>O arquivo de demanda viola alguma regra. Traz todos os problemas encontrados.</summary>
public sealed class PerfilDeDemandaInvalidoException(IReadOnlyList<string> problemas)
    : Exception("Perfil de demanda inválido:\n" + string.Join("\n", problemas.Select(problema => "- " + problema)))
{
    public IReadOnlyList<string> Problemas { get; } = problemas;
}

internal sealed record MetaDeDemanda(string? Fonte, string? Versao, string? Data, string? Situacao, string? Faixas);

internal sealed record FormulaJson(string? Ref, string? Expressao);

internal sealed record EdificacaoJson(
    string? Nome,
    [property: JsonPropertyName("carga_minima_w_m2")] decimal? CargaMinimaWM2,
    decimal? FdPct,
    List<List<decimal?>>? Escalonado,
    List<List<decimal?>>? FaixasKw,
    bool? Residencial);

internal sealed record IluminacaoJson(string? Ref, List<EdificacaoJson>? Edificacoes);

internal sealed record ColunaJson(string? Nome, List<string>? Aparelhos, List<List<decimal?>>? Faixas);

internal sealed record AparelhosJson(string? Ref, List<ColunaJson>? Colunas);

internal sealed record ArCondicionadoJson(string? Ref, List<List<decimal?>>? Residencial, List<List<decimal?>>? Comercial);

internal sealed record FornosJson(string? Ref, decimal? LimiteKw, List<List<decimal?>>? AteOLimite, List<List<decimal?>>? AcimaDoLimite);

internal sealed record MotoresJson(string? Ref, string? Aplicacao, decimal? MaiorPct, decimal? DemaisPct, string? RefDoProjetista);

internal sealed record SoldaJson(string? Ref, List<decimal>? PorOrdemPct, decimal? DemaisPct);

internal sealed record ArquivoDeDemanda(
    [property: JsonPropertyName("$meta")] MetaDeDemanda? Meta,
    string? Nome,
    FormulaJson? Formula,
    [property: JsonPropertyName("iluminacao_e_tug")] IluminacaoJson? IluminacaoETug,
    AparelhosJson? Aparelhos,
    ArCondicionadoJson? ArCondicionado,
    FornosJson? FornosEFogoes,
    MotoresJson? Motores,
    SoldaJson? MaquinasDeSolda);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ArquivoDeDemanda))]
internal sealed partial class PerfilDeDemandaJsonContexto : JsonSerializerContext;
