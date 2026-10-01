using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ampere.Core.Previsao;

/// <summary>
///     Pontos de tomada mínimos de uma categoria de cômodo: um mínimo fixo, um ponto a cada tantos metros de perímetro (ou
///     fração), ou os dois — o perímetro só acima de uma área, se houver.
/// </summary>
/// <param name="Alinea">Alínea do item da norma (ex.: "b").</param>
/// <param name="SeiscentosVa">Cômodo em que os primeiros pontos de tomada recebem a potência maior (banheiros, cozinhas…).</param>
/// <param name="Nota">O que a norma pede e o modelo não permite conferir (ex.: tomadas acima da bancada).</param>
/// <param name="CircuitoExclusivo">As tomadas do cômodo vão em circuitos só de tomadas desses cômodos.</param>
public sealed record RegraDoComodo(
    string Comodo,
    string Alinea,
    int? Minimo,
    decimal? PerimetroPorPontoM,
    decimal? PerimetroAcimaDeM2,
    bool SeiscentosVa,
    string? Nota,
    bool CircuitoExclusivo = false)
{
    /// <summary>Pontos de tomada mínimos para a área e o perímetro do cômodo.</summary>
    public int PontosMinimos(decimal areaM2, decimal perimetroM)
    {
        var minimo = Minimo ?? 0;
        if (PerimetroPorPontoM is not { } passo || (PerimetroAcimaDeM2 is { } limite && areaM2 <= limite)) return minimo;
        return Math.Max(minimo, (int)Math.Ceiling(perimetroM / passo));
    }

    /// <summary>A regra em texto, para o relatório (ex.: "um ponto a cada 5 m de perímetro, ou fração").</summary>
    public string Descrever()
    {
        var porPerimetro = PerimetroPorPontoM is { } passo ? $"um ponto a cada {NumeroEmTexto.FormatarParaLeitura(passo)} m de perímetro, ou fração" : null;
        return (Minimo, porPerimetro, PerimetroAcimaDeM2) switch
        {
            ({ } minimo, { } perimetro, { } area) =>
                $"{Pontos(minimo)} até {NumeroEmTexto.FormatarParaLeitura(area)} m²; acima, {perimetro}",
            ({ } minimo, { } perimetro, null) => $"{perimetro}, no mínimo {Pontos(minimo)}",
            (null, { } perimetro, _) => perimetro,
            ({ } minimo, null, _) => $"pelo menos {Pontos(minimo)}",
            _ => "sem mínimo"
        };
    }

    private static string Pontos(int quantidade) => quantidade == 1 ? "1 ponto" : $"{quantidade} pontos";
}

/// <summary>
///     Previsão mínima de cargas dos locais de habitação (NBR 5410, 9.5.2), carregada de
///     <c>data/normas/NBR5410/2004/previsao_de_cargas.json</c>: valores com a referência, nunca no código. Como o perfil
///     normativo, o carregador recusa arquivo malformado em vez de adivinhar.
/// </summary>
public sealed class NormaDePrevisao
{
    private static readonly Lazy<NormaDePrevisao> Oficial =
        new(() => Carregar(LerRecurso("Ampere.Core.Previsao.NBR5410_2004.previsao_de_cargas.json")));

    private NormaDePrevisao(ArquivoDePrevisao arquivo, IReadOnlyList<RegraDoComodo> comodos)
    {
        Perfil = arquivo.Perfil!;
        Fonte = arquivo.Meta!.Fonte!;
        var iluminacao = arquivo.Iluminacao!;
        ReferenciaDaIluminacao = iluminacao.Ref!;
        PontosDeLuzMinimos = iluminacao.PontosMinimos!.Value;
        AreaInicialM2 = iluminacao.AreaInicialM2!.Value;
        PotenciaInicialVA = iluminacao.PotenciaInicialVa!.Value;
        ACadaM2 = iluminacao.ACadaM2!.Value;
        AcrescimoVA = iluminacao.AcrescimoVa!.Value;
        ReferenciaDasTomadas = arquivo.Tomadas!.Ref!;
        ReferenciaDaDivisao = arquivo.Divisao!.Ref!;
        CorrenteIndependenteAcimaDeA = arquivo.Divisao.CorrenteIndependenteAcimaDeA!.Value;
        Comodos = comodos;
        var potencia = arquivo.PotenciaDasTomadas!;
        ReferenciaDaPotencia = potencia.Ref!;
        SeiscentosVA = potencia.SeiscentosVa!.Value;
        PontosDeSeiscentos = potencia.PontosDeSeiscentos!.Value;
        PontosDeSeiscentosNaAlternativa = potencia.PontosDeSeiscentosNaAlternativa!.Value;
        ConjuntoAcimaDePontos = potencia.ConjuntoAcimaDePontos!.Value;
        DemaisVA = potencia.DemaisVa!.Value;
    }

    /// <summary>A da NBR 5410:2004, embarcada no assembly.</summary>
    public static NormaDePrevisao NBR5410_2004 => Oficial.Value;

    /// <summary>Perfil normativo a que pertence (ex.: "NBR5410:2004").</summary>
    public string Perfil { get; }

    public string Fonte { get; }

    public string ReferenciaDaIluminacao { get; }

    public int PontosDeLuzMinimos { get; }

    public decimal AreaInicialM2 { get; }

    public decimal PotenciaInicialVA { get; }

    public decimal ACadaM2 { get; }

    public decimal AcrescimoVA { get; }

    public string ReferenciaDasTomadas { get; }

    /// <summary>Categorias de cômodo, na ordem do arquivo.</summary>
    public IReadOnlyList<RegraDoComodo> Comodos { get; }

    public string ReferenciaDaPotencia { get; }

    /// <summary>Referência da divisão da instalação em circuitos (9.5.3).</summary>
    public string ReferenciaDaDivisao { get; }

    /// <summary>Acima desta corrente nominal, o ponto de um equipamento constitui circuito independente.</summary>
    public decimal CorrenteIndependenteAcimaDeA { get; }

    /// <summary>Potência mínima dos primeiros pontos de tomada dos cômodos <see cref="RegraDoComodo.SeiscentosVa" />.</summary>
    public decimal SeiscentosVA { get; }

    public int PontosDeSeiscentos { get; }

    /// <summary>Pontos com a potência maior na alternativa admitida quando o conjunto passa de <see cref="ConjuntoAcimaDePontos" />.</summary>
    public int PontosDeSeiscentosNaAlternativa { get; }

    public int ConjuntoAcimaDePontos { get; }

    /// <summary>Potência mínima dos demais pontos de tomada.</summary>
    public decimal DemaisVA { get; }

    /// <summary>Carga mínima de iluminação do cômodo: a inicial até a área inicial; acima, o acréscimo a cada passo inteiro.</summary>
    public decimal IluminacaoMinimaVA(decimal areaM2) =>
        areaM2 <= AreaInicialM2 ? PotenciaInicialVA : PotenciaInicialVA + AcrescimoVA * decimal.Floor((areaM2 - AreaInicialM2) / ACadaM2);

    /// <summary>A regra da categoria, ou nula se a categoria não é da norma.</summary>
    public RegraDoComodo? Regra(string? categoria) =>
        categoria is null ? null : Comodos.FirstOrDefault(regra => string.Equals(regra.Comodo, categoria.Trim(), StringComparison.Ordinal));

    /// <exception cref="PrevisaoInvalidaException">Com todos os problemas encontrados.</exception>
    public static NormaDePrevisao Carregar(string json)
    {
        ArquivoDePrevisao? arquivo;
        try
        {
            arquivo = JsonSerializer.Deserialize(json, PrevisaoJsonContexto.Default.ArquivoDePrevisao);
        }
        catch (JsonException excecao)
        {
            throw new PrevisaoInvalidaException([$"JSON inválido: {excecao.Message}"]);
        }

        var problemas = new List<string>();
        if (arquivo is null) throw new PrevisaoInvalidaException(["arquivo vazio"]);
        if (arquivo.Meta?.Fonte is not { Length: > 0 } || arquivo.Meta.Ficticio is null) problemas.Add("$meta sem fonte ou sem ficticio");
        if (string.IsNullOrWhiteSpace(arquivo.Perfil)) problemas.Add("sem perfil");

        var referencias = new List<string>();
        void Ref(string nome, string? referencia)
        {
            if (string.IsNullOrWhiteSpace(referencia) || referencia.Trim() == "TODO_NORMA") problemas.Add($"{nome}: sem ref (valor sem fonte)");
            else referencias.Add(referencia);
        }

        void Positivo(string nome, decimal? valor)
        {
            if (valor is not > 0m) problemas.Add($"{nome}: ausente ou não positivo");
        }

        if (arquivo.Iluminacao is not { } iluminacao) problemas.Add("iluminacao ausente");
        else
        {
            Ref("iluminacao", iluminacao.Ref);
            Positivo("iluminacao.pontos_minimos", iluminacao.PontosMinimos);
            Positivo("iluminacao.area_inicial_m2", iluminacao.AreaInicialM2);
            Positivo("iluminacao.potencia_inicial_va", iluminacao.PotenciaInicialVa);
            Positivo("iluminacao.a_cada_m2", iluminacao.ACadaM2);
            Positivo("iluminacao.acrescimo_va", iluminacao.AcrescimoVa);
        }

        var regras = new List<RegraDoComodo>();
        if (arquivo.Tomadas is not { } tomadas) problemas.Add("tomadas ausente");
        else
        {
            Ref("tomadas", tomadas.Ref);
            foreach (var comodo in tomadas.Comodos ?? [])
            {
                var nome = comodo.Comodo?.Trim();
                if (string.IsNullOrEmpty(nome)) { problemas.Add("tomadas: cômodo sem nome"); continue; }
                if (regras.Any(regra => regra.Comodo == nome)) problemas.Add($"tomadas: cômodo '{nome}' repetido");
                if (string.IsNullOrWhiteSpace(comodo.Alinea)) problemas.Add($"tomadas: '{nome}' sem alínea");
                if (comodo.Minimo is null && comodo.PerimetroPorPontoM is null) problemas.Add($"tomadas: '{nome}' sem mínimo nem perímetro por ponto");
                if (comodo.Minimo is not null) Positivo($"tomadas: '{nome}'.minimo", comodo.Minimo);
                if (comodo.PerimetroPorPontoM is not null) Positivo($"tomadas: '{nome}'.perimetro_por_ponto_m", comodo.PerimetroPorPontoM);
                if (comodo.PerimetroAcimaDeM2 is not null) Positivo($"tomadas: '{nome}'.perimetro_acima_de_m2", comodo.PerimetroAcimaDeM2);
                regras.Add(new RegraDoComodo(nome, comodo.Alinea?.Trim() ?? string.Empty, comodo.Minimo, comodo.PerimetroPorPontoM, comodo.PerimetroAcimaDeM2,
                    comodo.SeiscentosVa ?? false, string.IsNullOrWhiteSpace(comodo.Nota) ? null : comodo.Nota.Trim(), comodo.CircuitoExclusivo ?? false));
            }

            if (regras.Count == 0) problemas.Add("tomadas: nenhum cômodo");
        }

        if (arquivo.Divisao is not { } divisao) problemas.Add("divisao ausente");
        else
        {
            Ref("divisao", divisao.Ref);
            Positivo("divisao.corrente_independente_acima_de_a", divisao.CorrenteIndependenteAcimaDeA);
        }

        if (arquivo.PotenciaDasTomadas is not { } potencia) problemas.Add("potencia_das_tomadas ausente");
        else
        {
            Ref("potencia_das_tomadas", potencia.Ref);
            Positivo("potencia_das_tomadas.seiscentos_va", potencia.SeiscentosVa);
            Positivo("potencia_das_tomadas.pontos_de_seiscentos", potencia.PontosDeSeiscentos);
            Positivo("potencia_das_tomadas.pontos_de_seiscentos_na_alternativa", potencia.PontosDeSeiscentosNaAlternativa);
            Positivo("potencia_das_tomadas.conjunto_acima_de_pontos", potencia.ConjuntoAcimaDePontos);
            Positivo("potencia_das_tomadas.demais_va", potencia.DemaisVa);
        }

        if (arquivo.Meta?.Ficticio == false && referencias.Any(referencia => Normalizar(referencia).Contains("FICTICIO")))
            problemas.Add("arquivo real com referência fictícia");
        if (problemas.Count > 0) throw new PrevisaoInvalidaException(problemas);
        return new NormaDePrevisao(arquivo, regras);
    }

    private static string Normalizar(string texto) =>
        new(texto.Normalize(NormalizationForm.FormD).Where(caractere => CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
            .Select(char.ToUpperInvariant).ToArray());

    private static string LerRecurso(string nome)
    {
        using var recurso = typeof(NormaDePrevisao).Assembly.GetManifestResourceStream(nome)
                            ?? throw new InvalidOperationException($"Recurso embarcado '{nome}' não encontrado.");
        using var leitor = new StreamReader(recurso, Encoding.UTF8);
        return leitor.ReadToEnd();
    }
}

/// <summary>O arquivo de previsão de cargas viola alguma regra. Traz todos os problemas encontrados.</summary>
public sealed class PrevisaoInvalidaException(IReadOnlyList<string> problemas)
    : Exception("Previsão de cargas inválida:\n" + string.Join("\n", problemas.Select(problema => "- " + problema)))
{
    public IReadOnlyList<string> Problemas { get; } = problemas;
}

// Formato em disco (tudo anulável: a validação relata o que faltar).
internal sealed record ArquivoDePrevisao(
    [property: JsonPropertyName("$meta")] MetaDaPrevisao? Meta,
    string? Perfil,
    IluminacaoJson? Iluminacao,
    TomadasJson? Tomadas,
    DivisaoJson? Divisao,
    PotenciaDasTomadasJson? PotenciaDasTomadas);

internal sealed record MetaDaPrevisao(string? Fonte, string? Versao, string? Data, bool? Ficticio, string? Situacao);

internal sealed record IluminacaoJson(string? Ref, int? PontosMinimos, decimal? AreaInicialM2, decimal? PotenciaInicialVa, decimal? ACadaM2, decimal? AcrescimoVa);

internal sealed record TomadasJson(string? Ref, List<ComodoJson>? Comodos);

internal sealed record ComodoJson(
    string? Comodo, string? Alinea, int? Minimo, decimal? PerimetroPorPontoM, decimal? PerimetroAcimaDeM2, bool? SeiscentosVa, string? Nota, bool? CircuitoExclusivo);

internal sealed record DivisaoJson(string? Ref, decimal? CorrenteIndependenteAcimaDeA);

internal sealed record PotenciaDasTomadasJson(
    string? Ref, decimal? SeiscentosVa, int? PontosDeSeiscentos, int? PontosDeSeiscentosNaAlternativa, int? ConjuntoAcimaDePontos, decimal? DemaisVa);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ArquivoDePrevisao))]
internal sealed partial class PrevisaoJsonContexto : JsonSerializerContext;
