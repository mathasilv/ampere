using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Ampere.Core.Normas;

/// <summary>
///     Perfil normativo: tabelas, fatores e limites consultados pelo motor de dimensionamento, cada valor com a
///     referência (item da norma) que o justifica.
/// </summary>
/// <remarks>
///     A regra 4 do AGENTS.md é aplicada na carga: tabela com ref TODO_NORMA não pode ter valores, e tabela com valores
///     precisa de ref. Perfil fictício (só testes) precisa se declarar e perfil real não pode citar referência fictícia.
///     Consulta sem dado devolve a ausência explicada — quem consulta para, nunca aproxima.
/// </remarks>
public sealed class PerfilNormativo
{
    /// <summary>Marcador de tabela sem fonte oficial ainda.</summary>
    public const string TodoNorma = "TODO_NORMA";

    private const string RecursoNBR5410 = "Ampere.Core.Normas.NBR5410_2004.perfil.json";

    private static readonly Lazy<PerfilNormativo> Oficial = new(() => Carregar(LerRecurso(RecursoNBR5410)));

    private readonly Tabela<IReadOnlyList<decimal>> _secoes;
    private readonly Tabela<IReadOnlyList<decimal>> _disjuntores;
    private readonly Tabela<IReadOnlyDictionary<string, int>> _condutoresCarregados;
    private readonly Tabela<IReadOnlyDictionary<string, decimal>> _secaoMinima;
    private readonly Tabela<IReadOnlyList<LinhaDeCapacidade>> _capacidade;
    private readonly Tabela<IReadOnlyList<LinhaDeTemperatura>> _temperatura;
    private readonly Tabela<IReadOnlyDictionary<int, decimal>> _agrupamento;
    private readonly Tabela<IReadOnlyDictionary<string, decimal>> _queda;
    private readonly Tabela<IReadOnlyDictionary<string, decimal>> _resistividade;
    private readonly Tabela<IReadOnlyDictionary<int, decimal>> _ocupacao;

    private PerfilNormativo(string nome, bool ficticio, Leitor leitor, TabelasDoPerfil tabelas)
    {
        Nome = nome;
        Ficticio = ficticio;
        _secoes = leitor.Lista("secoes_nominais_mm2", tabelas.SecoesNominaisMm2);
        _disjuntores = leitor.Lista("correntes_nominais_disjuntor_a", tabelas.CorrentesNominaisDisjuntorA);
        _condutoresCarregados = leitor.Ler<Dictionary<string, int>, IReadOnlyDictionary<string, int>>(
            "condutores_carregados", tabelas.CondutoresCarregados, valores => valores.Count == 0,
            (valores, nome1) => valores.ToDictionary(par => par.Key, par => leitor.Positivo(nome1, par.Value)));
        _secaoMinima = leitor.PorTexto("secao_minima_mm2", tabelas.SecaoMinimaMm2);
        _capacidade = leitor.Ler<List<LinhaDeCapacidadeJson>, IReadOnlyList<LinhaDeCapacidade>>(
            "capacidade_de_conducao_a", tabelas.CapacidadeDeConducaoA, valores => valores.Count == 0,
            (valores, nome1) => valores.Select(linha => leitor.Capacidade(nome1, linha)).ToList());
        _temperatura = leitor.Ler<List<LinhaDeTemperaturaJson>, IReadOnlyList<LinhaDeTemperatura>>(
            "fator_de_temperatura", tabelas.FatorDeTemperatura, valores => valores.Count == 0,
            (valores, nome1) => valores
                .Select(linha => new LinhaDeTemperatura(linha.Isolacao ?? string.Empty, leitor.PorDecimal(nome1, linha.PorTemperaturaC)))
                .ToList());
        _agrupamento = leitor.PorInteiro("fator_de_agrupamento", tabelas.FatorDeAgrupamento);
        _queda = leitor.PorTexto("queda_de_tensao_maxima_pct", tabelas.QuedaDeTensaoMaximaPct);
        _resistividade = leitor.PorTexto("resistividade_ohm_mm2_por_m", tabelas.ResistividadeOhmMm2PorM);
        _ocupacao = leitor.PorInteiro("ocupacao_maxima_eletroduto_pct", tabelas.OcupacaoMaximaEletrodutoPct);
    }

    /// <summary>Nome do perfil, gravado em AMP_PerfilNorma (ex.: "NBR5410:2004").</summary>
    public string Nome { get; }

    /// <summary>Perfil só para testes: nunca deve chegar a um projeto.</summary>
    public bool Ficticio { get; }

    /// <summary>Perfil oficial da NBR 5410:2004, embarcado no assembly.</summary>
    public static PerfilNormativo NBR5410_2004 => Oficial.Value;

    /// <exception cref="PerfilNormativoInvalidoException">Com todos os problemas encontrados.</exception>
    public static PerfilNormativo Carregar(string json)
    {
        ArquivoDePerfil? arquivo;
        try
        {
            arquivo = JsonSerializer.Deserialize(json, PerfilJsonContexto.Default.ArquivoDePerfil);
        }
        catch (JsonException excecao)
        {
            throw new PerfilNormativoInvalidoException([$"JSON inválido: {excecao.Message}"]);
        }

        var problemas = new List<string>();
        if (arquivo?.Meta is null) problemas.Add("$meta ausente");
        else if (arquivo.Meta.Ficticio is null) problemas.Add("$meta.ficticio ausente (true ou false)");
        if (string.IsNullOrWhiteSpace(arquivo?.Perfil)) problemas.Add("perfil sem nome");
        if (arquivo?.Tabelas is null) problemas.Add("tabelas ausentes");
        if (problemas.Count > 0) throw new PerfilNormativoInvalidoException(problemas);

        var ficticio = arquivo!.Meta!.Ficticio!.Value;
        var leitor = new Leitor(problemas);
        var perfil = new PerfilNormativo(arquivo.Perfil!.Trim(), ficticio, leitor, arquivo.Tabelas!);

        if (ficticio && !perfil.Nome.StartsWith("FICTICIO", StringComparison.Ordinal))
            problemas.Add("perfil fictício precisa começar com 'FICTICIO' no nome");
        if (!ficticio)
        {
            foreach (var (nome, referencia) in leitor.Referencias.Where(par => Normalizar(par.Referencia).Contains("FICTICIO")))
                problemas.Add($"perfil real com referência fictícia em {nome} ('{referencia}')");
        }

        if (problemas.Count > 0) throw new PerfilNormativoInvalidoException(problemas);
        return perfil;
    }

    public DadoNormativo<IReadOnlyList<decimal>> SecoesNominaisMm2() => Inteira(_secoes, "secoes_nominais_mm2");

    public DadoNormativo<IReadOnlyList<decimal>> CorrentesNominaisDeDisjuntorA() => Inteira(_disjuntores, "correntes_nominais_disjuntor_a");

    public DadoNormativo<int> CondutoresCarregados(string configuracao) =>
        PorChave(_condutoresCarregados, "condutores_carregados", configuracao, $"sem valor para a configuração {configuracao}");

    public DadoNormativo<decimal> SecaoMinimaMm2(string tipoDeCircuito) =>
        PorChave(_secaoMinima, "secao_minima_mm2", tipoDeCircuito, $"sem seção mínima para '{tipoDeCircuito}'");

    public DadoNormativo<decimal> CapacidadeDeConducaoA(string metodo, string isolacao, string material, int condutoresCarregados, decimal secaoMm2)
    {
        if (_capacidade.Pendente) return Pendente<decimal>("capacidade_de_conducao_a");

        var descricao = $"método {metodo}, {isolacao}, {material}, {condutoresCarregados} condutores carregados";
        var linha = _capacidade.Valores.FirstOrDefault(linha =>
            linha.Metodo == metodo && linha.Isolacao == isolacao && linha.Material == material && linha.CondutoresCarregados == condutoresCarregados);
        if (linha is null) return DadoNormativo<decimal>.Ausente(_capacidade.Referencia, $"sem linha para {descricao}");

        return linha.PorSecao.TryGetValue(secaoMm2, out var capacidade)
            ? DadoNormativo<decimal>.Com(capacidade, _capacidade.Referencia)
            : DadoNormativo<decimal>.Ausente(_capacidade.Referencia, $"sem valor para {Numero(secaoMm2)} mm² ({descricao})");
    }

    public DadoNormativo<decimal> FatorDeTemperatura(string isolacao, decimal temperaturaC)
    {
        if (_temperatura.Pendente) return Pendente<decimal>("fator_de_temperatura");

        var linha = _temperatura.Valores.FirstOrDefault(linha => linha.Isolacao == isolacao);
        if (linha is null) return DadoNormativo<decimal>.Ausente(_temperatura.Referencia, $"sem fatores para a isolação {isolacao}");

        return linha.PorTemperatura.TryGetValue(temperaturaC, out var fator)
            ? DadoNormativo<decimal>.Com(fator, _temperatura.Referencia)
            : DadoNormativo<decimal>.Ausente(_temperatura.Referencia, $"temperatura {Numero(temperaturaC)} °C não tabelada para {isolacao}");
    }

    public DadoNormativo<decimal> FatorDeAgrupamento(int circuitos) =>
        PorChave(_agrupamento, "fator_de_agrupamento", circuitos, $"sem fator para {circuitos} circuitos agrupados");

    public DadoNormativo<decimal> QuedaDeTensaoMaximaPct(string aplicacao) =>
        PorChave(_queda, "queda_de_tensao_maxima_pct", aplicacao, $"sem limite para '{aplicacao}'");

    public DadoNormativo<decimal> ResistividadeOhmMm2PorM(string material) =>
        PorChave(_resistividade, "resistividade_ohm_mm2_por_m", material, $"sem resistividade para {material}");

    /// <summary>Taxa máxima de ocupação; acima da maior faixa tabelada vale a maior (ex.: "3" = três ou mais).</summary>
    public DadoNormativo<decimal> OcupacaoMaximaDeEletrodutoPct(int condutores)
    {
        if (_ocupacao.Pendente) return Pendente<decimal>("ocupacao_maxima_eletroduto_pct");
        if (condutores < 1) return DadoNormativo<decimal>.Ausente(_ocupacao.Referencia, $"número de condutores inválido ({condutores})");

        var faixa = Math.Min(condutores, _ocupacao.Valores.Keys.Max());
        return PorChave(_ocupacao, "ocupacao_maxima_eletroduto_pct", faixa, $"sem taxa para {condutores} condutores");
    }

    internal static string Numero(decimal valor) => valor.ToString("0.############################", CultureInfo.InvariantCulture);

    private static DadoNormativo<T> Inteira<T>(Tabela<T> tabela, string nome) =>
        tabela.Pendente ? Pendente<T>(nome) : DadoNormativo<T>.Com(tabela.Valores, tabela.Referencia);

    private static DadoNormativo<TValor> PorChave<TChave, TValor>(
        Tabela<IReadOnlyDictionary<TChave, TValor>> tabela, string nome, TChave chave, string ausencia) where TChave : notnull
    {
        if (tabela.Pendente) return Pendente<TValor>(nome);
        return tabela.Valores.TryGetValue(chave, out var valor)
            ? DadoNormativo<TValor>.Com(valor, tabela.Referencia)
            : DadoNormativo<TValor>.Ausente(tabela.Referencia, ausencia);
    }

    private static DadoNormativo<T> Pendente<T>(string nome) =>
        DadoNormativo<T>.Ausente(TodoNorma, $"tabela {nome} sem dados oficiais (TODO_NORMA)");

    private static string Normalizar(string texto) =>
        new(texto.Normalize(NormalizationForm.FormD).Where(caractere => CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
            .Select(char.ToUpperInvariant).ToArray());

    private static string LerRecurso(string nome)
    {
        using var recurso = typeof(PerfilNormativo).Assembly.GetManifestResourceStream(nome)
                            ?? throw new InvalidOperationException($"Recurso embarcado '{nome}' não encontrado.");
        using var leitor = new StreamReader(recurso, Encoding.UTF8);
        return leitor.ReadToEnd();
    }

    private sealed record Tabela<T>(string Referencia, T Valores, bool Pendente);

    private sealed record LinhaDeCapacidade(string Metodo, string Isolacao, string Material, int CondutoresCarregados, IReadOnlyDictionary<decimal, decimal> PorSecao);

    private sealed record LinhaDeTemperatura(string Isolacao, IReadOnlyDictionary<decimal, decimal> PorTemperatura);

    /// <summary>Lê as tabelas aplicando a regra 4 e acumulando os problemas.</summary>
    private sealed class Leitor(List<string> problemas)
    {
        public List<(string Nome, string Referencia)> Referencias { get; } = [];

        public Tabela<TResultado> Ler<TJson, TResultado>(
            string nome, TabelaJson<TJson>? tabela, Func<TJson, bool> vazia, Func<TJson, string, TResultado> converter)
            where TResultado : class
        {
            if (tabela is null)
            {
                problemas.Add($"{nome}: ausente");
                return new Tabela<TResultado>(TodoNorma, default!, true);
            }

            var referencia = tabela.Ref?.Trim() ?? string.Empty;
            if (referencia.Length == 0)
            {
                problemas.Add($"{nome}: sem ref");
                return new Tabela<TResultado>(TodoNorma, default!, true);
            }

            Referencias.Add((nome, referencia));
            var semValores = tabela.Valores is null || vazia(tabela.Valores);
            if (referencia == TodoNorma)
            {
                if (!semValores) problemas.Add($"{nome}: tem valores mas a ref é TODO_NORMA (valor sem fonte)");
                return new Tabela<TResultado>(TodoNorma, default!, true);
            }

            if (semValores)
            {
                problemas.Add($"{nome}: ref '{referencia}' sem valores");
                return new Tabela<TResultado>(referencia, default!, true);
            }

            return new Tabela<TResultado>(referencia, converter(tabela.Valores!, nome), false);
        }

        public Tabela<IReadOnlyList<decimal>> Lista(string nome, TabelaJson<List<decimal>>? tabela) =>
            Ler<List<decimal>, IReadOnlyList<decimal>>(nome, tabela, valores => valores.Count == 0,
                (valores, nome1) => valores.Select(valor => Positivo(nome1, valor)).Distinct().Order().ToList());

        public Tabela<IReadOnlyDictionary<string, decimal>> PorTexto(string nome, TabelaJson<Dictionary<string, decimal>>? tabela) =>
            Ler<Dictionary<string, decimal>, IReadOnlyDictionary<string, decimal>>(nome, tabela, valores => valores.Count == 0,
                (valores, nome1) => valores.ToDictionary(par => par.Key, par => Positivo(nome1, par.Value)));

        public Tabela<IReadOnlyDictionary<int, decimal>> PorInteiro(string nome, TabelaJson<Dictionary<string, decimal>>? tabela) =>
            Ler<Dictionary<string, decimal>, IReadOnlyDictionary<int, decimal>>(nome, tabela, valores => valores.Count == 0, (valores, nome1) =>
            {
                var resultado = new Dictionary<int, decimal>();
                foreach (var (chave, valor) in valores)
                {
                    if (int.TryParse(chave, NumberStyles.None, CultureInfo.InvariantCulture, out var numero)) resultado[numero] = Positivo(nome1, valor);
                    else problemas.Add($"{nome1}: chave numérica inválida '{chave}'");
                }

                return resultado;
            });

        public IReadOnlyDictionary<decimal, decimal> PorDecimal(string nome, Dictionary<string, decimal>? valores)
        {
            var resultado = new Dictionary<decimal, decimal>();
            foreach (var (chave, valor) in valores ?? [])
            {
                if (decimal.TryParse(chave, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var numero)) resultado[numero] = Positivo(nome, valor);
                else problemas.Add($"{nome}: chave numérica inválida '{chave}'");
            }

            if (resultado.Count == 0) problemas.Add($"{nome}: linha sem valores");
            return resultado;
        }

        public LinhaDeCapacidade Capacidade(string nome, LinhaDeCapacidadeJson linha)
        {
            if (string.IsNullOrWhiteSpace(linha.Metodo) || string.IsNullOrWhiteSpace(linha.Isolacao) ||
                string.IsNullOrWhiteSpace(linha.Material) || linha.CondutoresCarregados is not > 0)
                problemas.Add($"{nome}: linha sem método, isolação, material ou condutores carregados");

            return new LinhaDeCapacidade(linha.Metodo ?? string.Empty, linha.Isolacao ?? string.Empty, linha.Material ?? string.Empty,
                linha.CondutoresCarregados ?? 0, PorDecimal(nome, linha.PorSecaoMm2));
        }

        public T Positivo<T>(string nome, T valor) where T : System.Numerics.INumber<T>
        {
            if (valor <= T.Zero) problemas.Add($"{nome}: valor não positivo ({valor})");
            return valor;
        }
    }
}

/// <summary>
///     Resultado de uma consulta ao perfil: o valor com a referência que o justifica, ou a ausência explicada.
/// </summary>
/// <remarks>Ler <see cref="Valor" /> de um dado ausente lança exceção — nunca devolve zero em silêncio.</remarks>
public sealed class DadoNormativo<T>
{
    private readonly T? _valor;

    private DadoNormativo(T? valor, string referencia, string? ausencia)
    {
        _valor = valor;
        Referencia = referencia;
        Ausencia = ausencia;
    }

    public T Valor => Ausencia is null ? _valor! : throw new InvalidOperationException($"Dado normativo ausente: {Ausencia}");

    /// <summary>Item da norma que justifica o valor, ou TODO_NORMA.</summary>
    public string Referencia { get; }

    /// <summary>Por que não há valor; <c>null</c> se disponível.</summary>
    public string? Ausencia { get; }

    public bool Disponivel => Ausencia is null;

    internal static DadoNormativo<T> Com(T valor, string referencia) => new(valor, referencia, null);

    internal static DadoNormativo<T> Ausente(string referencia, string ausencia) => new(default, referencia, ausencia);
}

/// <summary>O perfil viola alguma regra. Traz todos os problemas encontrados.</summary>
public sealed class PerfilNormativoInvalidoException(IReadOnlyList<string> problemas)
    : Exception("Perfil normativo inválido:\n" + string.Join("\n", problemas.Select(problema => "- " + problema)))
{
    public IReadOnlyList<string> Problemas { get; } = problemas;
}
