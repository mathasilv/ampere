using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ampere.Core.Memoria;

namespace Ampere.Core.Demanda;

/// <summary>
///     Uma linha da tabela do padrão de entrada: vale acima da linha anterior do mesmo fornecimento e até
///     <paramref name="AteKw" />, inclusive.
/// </summary>
/// <param name="RamalCobreConcentricoMm2">Ramal de conexão até 2 km da orla marítima, cabo de cobre concêntrico (nulo = a tabela não dá).</param>
/// <param name="RamalCobreMultiplexadoMm2">Ramal até 2 km da orla, cabo de cobre multiplexado.</param>
/// <param name="RamalAluminioMm2">Ramal a partir de 2 km da orla, cabo de alumínio multiplexado (<paramref name="RamalAluminioCabo" />).</param>
/// <param name="EletrodutoPol">Diâmetro nominal do eletroduto de aço galvanizado, em polegadas, como impresso (ex.: "1.1/2").</param>
/// <param name="CondutorFaseMm2">Condutor de cobre isolado mínimo do cliente, fase.</param>
/// <param name="CondutorNeutroMm2">Condutor de cobre isolado mínimo do cliente, neutro.</param>
/// <param name="AterramentoMm2">Condutor de aterramento (aço cobreado).</param>
/// <param name="Nota">Nota da linha (ex.: fornecimento opcional abaixo do limite), pelo número impresso.</param>
public sealed record LinhaDoPadrao(
    decimal AteKw,
    decimal DisjuntorA,
    decimal? RamalCobreConcentricoMm2,
    decimal? RamalCobreMultiplexadoMm2,
    decimal? RamalAluminioMm2,
    string? RamalAluminioCabo,
    string EletrodutoPol,
    decimal CondutorFaseMm2,
    decimal CondutorNeutroMm2,
    decimal AterramentoMm2,
    string EletrodutoDoAterramentoPol,
    string? Nota);

/// <summary>Um tipo de fornecimento da tabela (monofásico, bifásico, trifásico), com as linhas em ordem de carga.</summary>
/// <param name="Premissas">Valores admitidos nos cálculos da distribuidora para este fornecimento.</param>
public sealed record FornecimentoDoPadrao(string Nome, int Fases, string Premissas, IReadOnlyList<LinhaDoPadrao> Linhas);

/// <summary>Uma tabela do padrão de entrada, para uma tensão de fornecimento.</summary>
public sealed record TabelaDoPadrao(string Nome, string Tensao, string Referencia, IReadOnlyList<FornecimentoDoPadrao> Fornecimentos)
{
    /// <summary>Para a lista do diálogo (ex.: "Tabela 1 — 220/380 V").</summary>
    public string Descricao => $"{Nome} — {Tensao}";
}

/// <summary>
///     Padrão de entrada da distribuidora pela carga instalada (ex.: Equatorial NT.00001.EQTL rev. 09, Tabelas 1 e 2),
///     carregado de <c>data/distribuidoras</c>. Como o perfil normativo, o carregador recusa tabela malformada.
/// </summary>
public sealed class NormaDoPadraoDeEntrada
{
    private static readonly Lazy<NormaDoPadraoDeEntrada> Equatorial =
        new(() => Carregar(LerRecurso("Ampere.Core.Demanda.EQTL_NT.00001-09.padrao_de_entrada.json")));

    private NormaDoPadraoDeEntrada(ArquivoDoPadrao arquivo, IReadOnlyList<TabelaDoPadrao> tabelas)
    {
        Nome = arquivo.Nome!;
        Fonte = arquivo.Meta!.Fonte!;
        Situacao = arquivo.Meta.Situacao ?? string.Empty;
        ReferenciaDaCarga = arquivo.Carga!.Ref!;
        DecisaoDaCarga = arquivo.Carga.Decisao!;
        CondutorDoCliente = arquivo.CondutorDoCliente!;
        Notas = arquivo.Notas ?? [];
        Tabelas = tabelas;
    }

    /// <summary>A da Equatorial (NT.00001.EQTL rev. 09), embarcada no assembly.</summary>
    public static NormaDoPadraoDeEntrada EquatorialNt00001Rev09 => Equatorial.Value;

    public string Nome { get; }

    public string Fonte { get; }

    public string Situacao { get; }

    public string ReferenciaDaCarga { get; }

    /// <summary>Como a potência instalada (VA) vira carga em kW.</summary>
    public string DecisaoDaCarga { get; }

    /// <summary>A nota do documento sobre o condutor do cliente.</summary>
    public string CondutorDoCliente { get; }

    /// <summary>Texto de cada nota citada nas linhas, pelo número impresso.</summary>
    public IReadOnlyDictionary<string, string> Notas { get; }

    public IReadOnlyList<TabelaDoPadrao> Tabelas { get; }

    /// <summary>Diâmetro em polegadas como impresso ("3/4", "1.1/2", "2") em número.</summary>
    public static decimal? Polegadas(string texto)
    {
        decimal? Fracao(string parte)
        {
            var barra = parte.Split('/');
            if (barra.Length == 1) return decimal.TryParse(parte, NumberStyles.None, CultureInfo.InvariantCulture, out var inteiro) ? inteiro : null;
            return barra.Length == 2
                   && decimal.TryParse(barra[0], NumberStyles.None, CultureInfo.InvariantCulture, out var numerador)
                   && decimal.TryParse(barra[1], NumberStyles.None, CultureInfo.InvariantCulture, out var denominador) && denominador > 0m
                ? numerador / denominador
                : null;
        }

        var partes = texto.Trim().Split('.');
        return partes.Length switch
        {
            1 => Fracao(partes[0]),
            2 when !partes[0].Contains('/') && partes[1].Contains('/') && Fracao(partes[0]) is { } inteiro && Fracao(partes[1]) is { } fracao => inteiro + fracao,
            _ => null
        };
    }

    /// <exception cref="PadraoDeEntradaInvalidoException">Com todos os problemas encontrados.</exception>
    public static NormaDoPadraoDeEntrada Carregar(string json)
    {
        ArquivoDoPadrao? arquivo;
        try
        {
            arquivo = JsonSerializer.Deserialize(json, PadraoJsonContexto.Default.ArquivoDoPadrao);
        }
        catch (JsonException excecao)
        {
            throw new PadraoDeEntradaInvalidoException([$"JSON inválido: {excecao.Message}"]);
        }

        var problemas = new List<string>();
        if (arquivo is null) throw new PadraoDeEntradaInvalidoException(["arquivo vazio"]);
        if (arquivo.Meta?.Fonte is not { Length: > 0 } || arquivo.Meta.Ficticio is null) problemas.Add("$meta sem fonte ou sem ficticio");
        if (string.IsNullOrWhiteSpace(arquivo.Nome)) problemas.Add("sem nome");
        if (string.IsNullOrWhiteSpace(arquivo.Carga?.Ref) || string.IsNullOrWhiteSpace(arquivo.Carga.Decisao)) problemas.Add("carga sem ref ou sem decisão");
        if (string.IsNullOrWhiteSpace(arquivo.CondutorDoCliente)) problemas.Add("sem a nota do condutor do cliente");

        var tabelas = new List<TabelaDoPadrao>();
        foreach (var tabela in arquivo.Tabelas ?? [])
        {
            var nome = tabela.Tabela?.Trim() ?? string.Empty;
            if (nome.Length == 0 || string.IsNullOrWhiteSpace(tabela.Tensao) || string.IsNullOrWhiteSpace(tabela.Ref))
            {
                problemas.Add("tabela sem nome, tensão ou ref");
                continue;
            }

            if (tabelas.Any(lida => lida.Nome == nome)) problemas.Add($"{nome} repetida");
            var fornecimentos = new List<FornecimentoDoPadrao>();
            foreach (var fornecimento in tabela.Fornecimentos ?? [])
            {
                var qual = $"{nome}, {fornecimento.Fornecimento}";
                if (string.IsNullOrWhiteSpace(fornecimento.Fornecimento) || fornecimento.Fases is not (1 or 2 or 3) || string.IsNullOrWhiteSpace(fornecimento.Premissas))
                {
                    problemas.Add($"{qual}: fornecimento sem nome, fases (1 a 3) ou premissas");
                    continue;
                }

                var linhas = new List<LinhaDoPadrao>();
                foreach (var linha in fornecimento.Linhas ?? [])
                {
                    var onde = $"{qual}, até {linha.AteKw} kW";
                    if (linha.AteKw is not > 0m || linha.DisjuntorA is not > 0m || linha.CondutorFaseMm2 is not > 0m || linha.CondutorNeutroMm2 is not > 0m
                        || linha.AterramentoAcoCobreadoMm2 is not > 0m)
                        problemas.Add($"{onde}: carga, disjuntor, condutores do cliente e aterramento precisam ser positivos");
                    if (linhas.Count > 0 && linha.AteKw <= linhas[^1].AteKw) problemas.Add($"{onde}: fora da ordem crescente de carga");
                    if (linha.EletrodutoAcoGalvanizadoPol is not { } eletroduto || Polegadas(eletroduto) is null
                        || linha.EletrodutoAterramentoPol is not { } doAterramento || Polegadas(doAterramento) is null)
                        problemas.Add($"{onde}: eletroduto sem diâmetro em polegadas legível");
                    if (linha.RamalAluminioMm2 is not null && string.IsNullOrWhiteSpace(linha.RamalAluminioCabo)) problemas.Add($"{onde}: ramal de alumínio sem o cabo");
                    if (linha.Nota is { } nota && !(arquivo.Notas ?? []).ContainsKey(nota)) problemas.Add($"{onde}: {nota} sem texto em 'notas'");
                    linhas.Add(new LinhaDoPadrao(linha.AteKw ?? 0m, linha.DisjuntorA ?? 0m, linha.RamalCobreConcentricoMm2, linha.RamalCobreMultiplexadoMm2,
                        linha.RamalAluminioMm2, linha.RamalAluminioCabo?.Trim(), linha.EletrodutoAcoGalvanizadoPol?.Trim() ?? string.Empty,
                        linha.CondutorFaseMm2 ?? 0m, linha.CondutorNeutroMm2 ?? 0m, linha.AterramentoAcoCobreadoMm2 ?? 0m,
                        linha.EletrodutoAterramentoPol?.Trim() ?? string.Empty, linha.Nota?.Trim()));
                }

                if (linhas.Count == 0) problemas.Add($"{qual}: sem linhas");
                fornecimentos.Add(new FornecimentoDoPadrao(fornecimento.Fornecimento.Trim(), fornecimento.Fases.Value, fornecimento.Premissas.Trim(), linhas));
            }

            if (fornecimentos.Count == 0) problemas.Add($"{nome}: sem fornecimentos");
            tabelas.Add(new TabelaDoPadrao(nome, tabela.Tensao.Trim(), tabela.Ref.Trim(), fornecimentos));
        }

        if (tabelas.Count == 0) problemas.Add("sem tabelas");
        if (problemas.Count > 0) throw new PadraoDeEntradaInvalidoException(problemas);
        return new NormaDoPadraoDeEntrada(arquivo, tabelas);
    }

    private static string LerRecurso(string nome)
    {
        using var recurso = typeof(NormaDoPadraoDeEntrada).Assembly.GetManifestResourceStream(nome)
                            ?? throw new InvalidOperationException($"Recurso embarcado '{nome}' não encontrado.");
        using var leitor = new StreamReader(recurso, Encoding.UTF8);
        return leitor.ReadToEnd();
    }
}

/// <summary>O arquivo do padrão de entrada viola alguma regra. Traz todos os problemas encontrados.</summary>
public sealed class PadraoDeEntradaInvalidoException(IReadOnlyList<string> problemas)
    : Exception("Padrão de entrada inválido:\n" + string.Join("\n", problemas.Select(problema => "- " + problema)))
{
    public IReadOnlyList<string> Problemas { get; } = problemas;
}

/// <summary>O que o projetista escolhe para o padrão de entrada.</summary>
/// <param name="Tabela">Nome da tabela (pela tensão de fornecimento, ex.: "Tabela 1").</param>
/// <param name="Fornecimento">Tipo de fornecimento escolhido; nulo = o menor que atende a carga.</param>
public sealed record EscolhaDoPadrao(string Tabela, string? Fornecimento = null);

/// <summary>Padrão de entrada: a linha da tabela (nula se não saiu), a carga, a memória e o que impediu.</summary>
public sealed record ResultadoDoPadrao(
    TabelaDoPadrao? Tabela,
    FornecimentoDoPadrao? Fornecimento,
    LinhaDoPadrao? Linha,
    decimal CargaKw,
    MemoriaDeCalculo? Memoria,
    IReadOnlyList<string> Problemas);

/// <summary>
///     Caso de uso do padrão de entrada pela carga instalada: a linha da tabela da distribuidora para a tensão escolhida, no
///     menor tipo de fornecimento que atende a carga (ou no escolhido pelo projetista), com disjuntor, ramal de conexão,
///     eletrodutos, condutores do cliente e aterramento numa memória de cálculo. Não grava nada no modelo.
/// </summary>
/// <remarks>
///     A faixa de cada linha começa acima da linha anterior do mesmo fornecimento. Fornecimento escolhido acima do menor
///     (ex.: trifásico para 10 kW) usa a primeira faixa dele, e a nota da linha (o consumidor paga a diferença) vai para a
///     memória. Carga acima da última linha: o padrão não sai — a tabela não cobre.
/// </remarks>
public static class PadraoDeEntrada
{
    public const string Nome = "Padrão de entrada";

    public static ResultadoDoPadrao Calcular(decimal potenciaInstaladaVA, EscolhaDoPadrao escolha, NormaDoPadraoDeEntrada norma)
    {
        var carga = potenciaInstaladaVA / 1000m;
        var tabela = norma.Tabelas.FirstOrDefault(tabela => tabela.Nome == escolha.Tabela?.Trim());
        if (tabela is null) return Falha(null, carga, $"tabela '{escolha.Tabela}' fora do padrão de entrada ({norma.Nome})");
        if (carga <= 0m) return Falha(tabela, carga, "sem carga instalada: nada a dimensionar");

        FornecimentoDoPadrao? fornecimento;
        if (escolha.Fornecimento is { } escolhido)
        {
            fornecimento = tabela.Fornecimentos.FirstOrDefault(candidato => candidato.Nome == escolhido.Trim());
            if (fornecimento is null) return Falha(tabela, carga, $"fornecimento '{escolhido}' fora da {tabela.Nome} ({tabela.Tensao})");
            if (fornecimento.Linhas[^1].AteKw < carga)
                return Falha(tabela, carga, $"carga instalada de {N(carga)} kW acima do limite do fornecimento {fornecimento.Nome} na {tabela.Nome} ({N(fornecimento.Linhas[^1].AteKw)} kW)");
        }
        else
        {
            fornecimento = tabela.Fornecimentos.FirstOrDefault(candidato => candidato.Linhas[^1].AteKw >= carga);
            if (fornecimento is null)
            {
                var maior = tabela.Fornecimentos.Max(candidato => candidato.Linhas[^1].AteKw);
                return Falha(tabela, carga, $"carga instalada de {N(carga)} kW acima da {tabela.Nome} (até {N(maior)} kW): o padrão não sai da tabela");
            }
        }

        var indice = fornecimento.Linhas.ToList().FindIndex(linha => linha.AteKw >= carga);
        var linha = fornecimento.Linhas[indice];
        var inferior = indice == 0 ? 0m : fornecimento.Linhas[indice - 1].AteKw;
        var faixa = indice == 0 ? $"até {N(linha.AteKw)} kW" : $"acima de {N(inferior)} e até {N(linha.AteKw)} kW";
        var referencia = $"{tabela.Referencia}; {fornecimento.Nome}, {faixa}";

        var passos = new List<PassoDeCalculo>
        {
            new(norma.ReferenciaDaCarga, "Carga instalada", "C = ΣP / 1000", [new ValorDoPasso("ΣP", potenciaInstaladaVA, "VA")], carga, "kW",
                $"pontos classificados, sem as reservas; {norma.DecisaoDaCarga}"),
            new(tabela.Referencia, "Tipo de fornecimento",
                escolha.Fornecimento is null ? $"o primeiro fornecimento da {tabela.Nome} que vai até C" : "escolhido pelo projetista",
                [new ValorDoPasso("C", carga, "kW")], fornecimento.Fases, "fases",
                $"{fornecimento.Nome} em {tabela.Tensao}; premissas da distribuidora: {fornecimento.Premissas}"),
            new(referencia, "Disjuntor termomagnético", "In = tabela (C)", [new ValorDoPasso("C", carga, "kW")], linha.DisjuntorA, "A",
                linha.Nota is { } nota ? $"{nota}: {norma.Notas[nota]}" : null),
            new(referencia, "Condutor do cliente — fase", "S = tabela (C)", [], linha.CondutorFaseMm2, "mm²", $"cobre isolado, mínimo; {norma.CondutorDoCliente}"),
            new(referencia, "Condutor do cliente — neutro", "SN = tabela (C)", [], linha.CondutorNeutroMm2, "mm²", "cobre isolado, mínimo"),
            new(referencia, "Eletroduto de aço galvanizado", $"Ø = tabela (\"{linha.EletrodutoPol}\")", [], NormaDoPadraoDeEntrada.Polegadas(linha.EletrodutoPol), "pol",
                "diâmetro nominal"),
            new(referencia, "Condutor de aterramento (aço cobreado)", "S = tabela (C)", [], linha.AterramentoMm2, "mm²"),
            new(referencia, "Eletroduto do aterramento", $"Ø = tabela (\"{linha.EletrodutoDoAterramentoPol}\")", [],
                NormaDoPadraoDeEntrada.Polegadas(linha.EletrodutoDoAterramentoPol), "pol", "diâmetro nominal")
        };
        if (linha.RamalCobreMultiplexadoMm2 is { } multiplexado)
            passos.Add(new PassoDeCalculo(referencia, "Ramal de conexão até 2 km da orla marítima — cobre multiplexado", "S = tabela (C)", [], multiplexado, "mm²",
                linha.RamalCobreConcentricoMm2 is { } concentrico ? $"ou cabo de cobre concêntrico de {N(concentrico)} mm²" : null));
        if (linha.RamalAluminioMm2 is { } aluminio)
            passos.Add(new PassoDeCalculo(referencia, $"Ramal de conexão a partir de 2 km da orla marítima — alumínio multiplexado {linha.RamalAluminioCabo}",
                "S = tabela (C)", [], aluminio, "mm²"));

        return new ResultadoDoPadrao(tabela, fornecimento, linha, carga, new MemoriaDeCalculo(Nome, norma.Nome, passos), []);
    }

    private static ResultadoDoPadrao Falha(TabelaDoPadrao? tabela, decimal carga, string problema) => new(tabela, null, null, carga, null, [problema]);

    private static string N(decimal valor) => NumeroEmTexto.FormatarParaLeitura(valor);
}

// Formato em disco (tudo anulável: a validação relata o que faltar).
internal sealed record ArquivoDoPadrao(
    [property: JsonPropertyName("$meta")] MetaDoPadrao? Meta,
    string? Nome,
    CargaDoPadraoJson? Carga,
    string? CondutorDoCliente,
    Dictionary<string, string>? Notas,
    List<TabelaDoPadraoJson>? Tabelas);

internal sealed record MetaDoPadrao(string? Fonte, string? Versao, string? Data, bool? Ficticio, string? Situacao);

internal sealed record CargaDoPadraoJson(string? Ref, string? Decisao);

internal sealed record TabelaDoPadraoJson(string? Tabela, string? Tensao, string? Ref, List<FornecimentoDoPadraoJson>? Fornecimentos);

internal sealed record FornecimentoDoPadraoJson(string? Fornecimento, int? Fases, string? Premissas, List<LinhaDoPadraoJson>? Linhas);

internal sealed record LinhaDoPadraoJson(
    decimal? AteKw,
    decimal? DisjuntorA,
    decimal? RamalCobreConcentricoMm2,
    decimal? RamalCobreMultiplexadoMm2,
    decimal? RamalAluminioMm2,
    string? RamalAluminioCabo,
    string? EletrodutoAcoGalvanizadoPol,
    decimal? CondutorFaseMm2,
    decimal? CondutorNeutroMm2,
    decimal? AterramentoAcoCobreadoMm2,
    string? EletrodutoAterramentoPol,
    string? Nota = null);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ArquivoDoPadrao))]
internal sealed partial class PadraoJsonContexto : JsonSerializerContext;
