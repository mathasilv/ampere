using System.Globalization;
using System.Text;
using System.Text.Json;
using Ampere.Core.Cargas;

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

    private static readonly IReadOnlyDictionary<string, RegraNormativa> RegrasPorChave = new Dictionary<string, RegraNormativa>
    {
        ["corrente_de_projeto"] = RegraNormativa.CorrenteDeProjeto,
        ["coordenacao_condutor_protecao"] = RegraNormativa.CoordenacaoCondutorProtecao,
        ["queda_de_tensao"] = RegraNormativa.QuedaDeTensao,
        ["condutores_no_eletroduto"] = RegraNormativa.CondutoresNoEletroduto,
        ["coordenacao_idr_disjuntor"] = RegraNormativa.CoordenacaoIdrDisjuntor,
        ["demanda_do_quadro"] = RegraNormativa.DemandaDoQuadro,
        ["secao_do_neutro"] = RegraNormativa.SecaoDoNeutro,
        ["capacidade_de_interrupcao"] = RegraNormativa.CapacidadeDeInterrupcao
    };

    // Regras acrescentadas depois: perfil anterior sem elas carrega, e a memória cita TODO_NORMA.
    private static readonly IReadOnlySet<string> RegrasOpcionais = new HashSet<string>(StringComparer.Ordinal) { "capacidade_de_interrupcao" };

    private readonly Dictionary<RegraNormativa, string> _regras = [];
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
    private readonly Tabela<IReadOnlyList<decimal>> _idr;
    private readonly Tabela<IReadOnlyList<decimal>> _sensibilidadesDeIdr;
    private readonly Tabela<IReadOnlyDictionary<string, ProtecaoDiferencialDoLocal>> _protecaoDiferencial;
    private readonly Tabela<IReadOnlyDictionary<string, decimal>> _fatorDeDemanda;
    private readonly Tabela<IReadOnlyDictionary<decimal, decimal>> _secaoDeProtecao;
    private readonly Tabela<IReadOnlyList<string>>? _metodosComEletroduto;
    private readonly Tabela<IReadOnlyDictionary<int, decimal>>? _agrupamentoEnterrado;
    private readonly IReadOnlyList<string> _metodosEnterrados = [];
    private readonly Tabela<IReadOnlyDictionary<string, ConstrucoesDoMetodo>>? _construcoes;
    private readonly Tabela<IReadOnlyDictionary<string, decimal>>? _peForaDoCabo;
    private readonly Tabela<IReadOnlyDictionary<(string Material, string Isolacao), (decimal Ate300, decimal Acima300)>>? _fatorK;

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
                .Select(linha => new LinhaDeTemperatura(linha.Isolacao ?? string.Empty, leitor.Metodos(nome1, linha.Metodos), leitor.PorDecimal(nome1, linha.PorTemperaturaC),
                    leitor.RefDaLinha($"{nome1} ({linha.Isolacao}{(linha.Metodos is { } metodos ? $"; {string.Join(", ", metodos)}" : string.Empty)})", linha.Ref)))
                .ToList());
        _agrupamento = leitor.PorInteiro("fator_de_agrupamento", tabelas.FatorDeAgrupamento);
        _queda = leitor.PorTexto("queda_de_tensao_maxima_pct", tabelas.QuedaDeTensaoMaximaPct);
        _resistividade = leitor.PorTexto("resistividade_ohm_mm2_por_m", tabelas.ResistividadeOhmMm2PorM);
        _ocupacao = leitor.PorInteiro("ocupacao_maxima_eletroduto_pct", tabelas.OcupacaoMaximaEletrodutoPct);
        _idr = leitor.Lista("correntes_nominais_idr_a", tabelas.CorrentesNominaisIdrA);
        _sensibilidadesDeIdr = leitor.Lista("sensibilidades_nominais_idr_ma", tabelas.SensibilidadesNominaisIdrMa);
        _protecaoDiferencial = leitor.Ler<List<LinhaDeProtecaoDiferencialJson>, IReadOnlyDictionary<string, ProtecaoDiferencialDoLocal>>(
            "protecao_diferencial_por_local", tabelas.ProtecaoDiferencialPorLocal, valores => valores.Count == 0, leitor.ProtecaoDiferencial);
        _fatorDeDemanda = leitor.Ler<Dictionary<string, decimal>, IReadOnlyDictionary<string, decimal>>(
            "fator_de_demanda_por_tipo", tabelas.FatorDeDemandaPorTipo, valores => valores.Count == 0, leitor.Fracao);

        _secaoDeProtecao = leitor.Ler<Dictionary<string, decimal>, IReadOnlyDictionary<decimal, decimal>>(
            "secao_do_condutor_de_protecao_mm2", tabelas.SecaoDoCondutorDeProtecaoMm2, valores => valores.Count == 0, leitor.SecaoDeProtecao);

        // Opcionais: sem elas, todo método leva eletroduto e usa a tabela geral de agrupamento (perfis anteriores).
        if (tabelas.MetodosComEletroduto is { } comEletroduto)
        {
            _metodosComEletroduto = leitor.Ler<List<string>, IReadOnlyList<string>>("metodos_com_eletroduto", comEletroduto, valores => valores.Count == 0,
                (valores, nome1) => leitor.Metodos(nome1, valores)!);
        }

        if (tabelas.ConstrucoesPorMetodo is { } construcoes)
        {
            _construcoes = leitor.Ler<List<LinhaDeConstrucaoJson>, IReadOnlyDictionary<string, ConstrucoesDoMetodo>>(
                "construcoes_por_metodo", construcoes, valores => valores.Count == 0, leitor.Construcoes);
        }

        if (tabelas.SecaoMinimaDoPeForaDoCaboMm2 is { } peForaDoCabo) _peForaDoCabo = leitor.PorTexto("secao_minima_do_pe_fora_do_cabo_mm2", peForaDoCabo);

        if (tabelas.FatorKDeCurtoCircuito is { } fatorK)
        {
            _fatorK = leitor.Ler<List<LinhaDeFatorKJson>, IReadOnlyDictionary<(string Material, string Isolacao), (decimal Ate300, decimal Acima300)>>(
                "fator_k_de_curto_circuito", fatorK, valores => valores.Count == 0, leitor.FatorK);
        }

        if (tabelas.FatorDeAgrupamentoEnterrado is { } enterrado)
        {
            _agrupamentoEnterrado = leitor.PorInteiro("fator_de_agrupamento_enterrado", new TabelaJson<Dictionary<string, decimal>>(enterrado.Ref, enterrado.Valores));
            _metodosEnterrados = leitor.Metodos("fator_de_agrupamento_enterrado", enterrado.Metodos ?? [])!;
        }

        // Só o que o motor consegue levar adiante: método com fator de temperatura, material com resistividade.
        IReadOnlyList<LinhaDeCapacidade> capacidade = _capacidade.Pendente ? [] : _capacidade.Valores;
        IReadOnlyList<LinhaDeTemperatura> temperatura = _temperatura.Pendente ? [] : _temperatura.Valores;
        Coerencia(leitor, capacidade, temperatura);
        Vocabulario = new VocabularioDoPerfil(
            Distintos(capacidade.Select(linha => linha.Metodo).Where(metodo => temperatura.Any(linha => linha.Vale(metodo)))),
            Distintos(capacidade.Select(linha => linha.Isolacao)),
            Distintos(capacidade.Select(linha => linha.Material).Where(material => !_resistividade.Pendente && _resistividade.Valores.ContainsKey(material))),
            // Na ordem do arquivo (o dicionário da tabela não garante ordem).
            _protecaoDiferencial.Pendente
                ? []
                : Distintos((tabelas.ProtecaoDiferencialPorLocal?.Valores ?? [])
                    .Select(linha => linha.Local?.Trim() ?? string.Empty)
                    .Where(_protecaoDiferencial.Valores.ContainsKey)));
    }

    // Tabelas que dependem umas das outras: um erro de digitação não pode trocar a tabela usada em silêncio.
    private void Coerencia(Leitor leitor, IReadOnlyList<LinhaDeCapacidade> capacidade, IReadOnlyList<LinhaDeTemperatura> temperatura)
    {
        foreach (var (nome, tabela) in new[] { ("fator_de_agrupamento", _agrupamento), ("fator_de_agrupamento_enterrado", _agrupamentoEnterrado) })
        {
            if (tabela is not { Pendente: false }) continue;
            foreach (var (circuitos, fator) in tabela.Valores.Where(par => par.Value > 1m).OrderBy(par => par.Key))
                leitor.Problema($"{nome}: fator de {circuitos} circuitos acima de 1 ({NumeroEmTexto.Formatar(fator)})");
        }

        // A linha enterrada usa a temperatura do solo: a linha de temperatura que vale para ela precisa ser só das enterradas.
        foreach (var linha in temperatura)
        {
            var enterrados = _metodosEnterrados.Where(linha.Vale).ToList();
            if (enterrados.Count == 0) continue;
            if (linha.Metodos is null)
                leitor.Problema($"fator_de_temperatura: linha de {linha.Isolacao} sem métodos vale também para o método enterrado {string.Join(", ", enterrados)}");
            else if (linha.Metodos.Any(metodo => !_metodosEnterrados.Contains(metodo, StringComparer.Ordinal)))
                leitor.Problema($"fator_de_temperatura: linha de {linha.Isolacao} mistura método enterrado ({string.Join(", ", enterrados)}) com métodos ao ar");
        }

        // Todo condutor que o motor dimensiona tem k: senão, a verificação de curto-circuito pararia o cálculo.
        if (_fatorK is { Pendente: false } fatorK)
        {
            foreach (var (material, isolacao) in capacidade.Select(linha => (linha.Material, linha.Isolacao)).Distinct().Where(par => !fatorK.Valores.ContainsKey(par)))
                leitor.Problema($"fator_k_de_curto_circuito: sem linha para {material} com isolação {isolacao}, que a capacidade de condução tem");
        }

        var metodos = capacidade.Select(linha => linha.Metodo).ToHashSet(StringComparer.Ordinal);
        foreach (var (nome, lista) in new[]
                 {
                     ("metodos_com_eletroduto", _metodosComEletroduto is { Pendente: false } tabela ? tabela.Valores : []),
                     ("fator_de_agrupamento_enterrado", _metodosEnterrados),
                     ("construcoes_por_metodo", _construcoes is { Pendente: false } porMetodo ? porMetodo.Valores.Keys.ToList() : [])
                 })
        foreach (var metodo in lista.Where(metodo => metodos.Count > 0 && !metodos.Contains(metodo)))
            leitor.Problema($"{nome}: método '{metodo}' fora da tabela de capacidade de condução");
    }

    /// <summary>
    ///     Valores que as tabelas do perfil reconhecem, na ordem do arquivo, para os diálogos oferecerem só o que o motor
    ///     encontra. Tabela sem dados oficiais (TODO_NORMA) dá lista vazia.
    /// </summary>
    public VocabularioDoPerfil Vocabulario { get; }

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
        perfil.LerRegras(arquivo.Regras, leitor, problemas);

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

    /// <summary>Item da norma que fundamenta a regra, ou TODO_NORMA.</summary>
    public string ReferenciaDaRegra(RegraNormativa regra) => _regras[regra];

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

        var referencia = linha.Referencia ?? _capacidade.Referencia;
        return linha.PorSecao.TryGetValue(secaoMm2, out var capacidade)
            ? DadoNormativo<decimal>.Com(capacidade, referencia)
            : DadoNormativo<decimal>.Ausente(referencia, $"sem valor para {NumeroEmTexto.Formatar(secaoMm2)} mm² ({descricao})");
    }

    /// <summary>Seção mínima do condutor de proteção (PE) do mesmo material das fases, pela seção dos condutores de fase.</summary>
    public DadoNormativo<decimal> SecaoDoCondutorDeProtecaoMm2(decimal secaoDeFaseMm2) =>
        PorChave(_secaoDeProtecao, "secao_do_condutor_de_protecao_mm2", secaoDeFaseMm2,
            $"sem seção do condutor de proteção para fase de {NumeroEmTexto.Formatar(secaoDeFaseMm2)} mm²");

    /// <summary>
    ///     O método de instalação leva eletroduto? Sem a tabela metodos_com_eletroduto no perfil (ou pendente), todos levam.
    ///     Pela NBR 5410, C, E, F e G são cabos sobre parede ou ao ar livre: o motor não dimensiona eletroduto para eles.
    /// </summary>
    public bool ComEletroduto(string metodo) =>
        _metodosComEletroduto is not { Pendente: false } tabela || tabela.Valores.Contains(metodo, StringComparer.Ordinal);

    /// <summary>
    ///     A construção do condutor (condutor isolado, cabo unipolar ou multipolar) no método; nula sem a tabela no perfil
    ///     (ou pendente) ou com o método fora dela — aí nada é conferido.
    /// </summary>
    public AdmissaoDaConstrucao? Construcao(string metodo, string construcao)
    {
        if (_construcoes is not { Pendente: false } tabela || !tabela.Valores.TryGetValue(metodo, out var doMetodo)) return null;
        if (doMetodo.Admitidas.Contains(construcao, StringComparer.Ordinal)) return new AdmissaoDaConstrucao(true, null, doMetodo.Admitidas, tabela.Referencia);
        return doMetodo.Condicionais.TryGetValue(construcao, out var condicao)
            ? new AdmissaoDaConstrucao(true, condicao, doMetodo.Admitidas, tabela.Referencia)
            : new AdmissaoDaConstrucao(false, null, doMetodo.Admitidas, tabela.Referencia);
    }

    /// <summary>O método só admite cabo multipolar (o condutor de proteção é uma veia do cabo)? Falso sem a tabela.</summary>
    public bool SoCaboMultipolar(string metodo) =>
        _construcoes is { Pendente: false } tabela && tabela.Valores.TryGetValue(metodo, out var doMetodo)
                                                  && doMetodo.Admitidas.SequenceEqual([ConstrucoesDeCondutor.CaboMultipolar])
                                                  && doMetodo.Condicionais.Count == 0;

    /// <summary>
    ///     Seção mínima do condutor de proteção que não faz parte do cabo nem está no mesmo conduto das fases, por material;
    ///     nula sem a tabela no perfil.
    /// </summary>
    public DadoNormativo<decimal>? SecaoMinimaDoPeForaDoCaboMm2(string material) =>
        _peForaDoCabo is null
            ? null
            : PorChave(_peForaDoCabo, "secao_minima_do_pe_fora_do_cabo_mm2", material, $"sem seção mínima do condutor de proteção fora do cabo para {material}");

    /// <summary>
    ///     Fator k do condutor para a integral de Joule (k²S²), pelo material, pela isolação e pela seção (até 300 mm² ou
    ///     acima); nulo sem a tabela no perfil.
    /// </summary>
    public DadoNormativo<decimal>? FatorKDeCurtoCircuito(string material, string isolacao, decimal secaoMm2)
    {
        if (_fatorK is null) return null;
        if (_fatorK.Pendente) return Pendente<decimal>("fator_k_de_curto_circuito");
        return _fatorK.Valores.TryGetValue((material, isolacao), out var fator)
            ? DadoNormativo<decimal>.Com(secaoMm2 <= 300m ? fator.Ate300 : fator.Acima300, _fatorK.Referencia)
            : DadoNormativo<decimal>.Ausente(_fatorK.Referencia, $"sem fator k para {material} com isolação {isolacao}");
    }

    /// <summary>
    ///     Linha enterrada: o método tem tabela de agrupamento própria (fator_de_agrupamento_enterrado) e a temperatura que
    ///     vale para ele é a do solo.
    /// </summary>
    public bool Enterrado(string metodo) => _metodosEnterrados.Contains(metodo, StringComparer.Ordinal);

    /// <param name="metodo">Método de instalação: a linha precisa valer para ele (a Tabela 40 do ar não vale para linha enterrada).</param>
    public DadoNormativo<decimal> FatorDeTemperatura(string metodo, string isolacao, decimal temperaturaC)
    {
        if (_temperatura.Pendente) return Pendente<decimal>("fator_de_temperatura");

        var daIsolacao = _temperatura.Valores.Where(linha => linha.Isolacao == isolacao).ToList();
        if (daIsolacao.Count == 0) return DadoNormativo<decimal>.Ausente(_temperatura.Referencia, $"sem fatores para a isolação {isolacao}");
        var linha = daIsolacao.FirstOrDefault(linha => linha.Vale(metodo));
        if (linha is null)
            return DadoNormativo<decimal>.Ausente(_temperatura.Referencia, $"sem fatores de temperatura para o método {metodo} ({isolacao}) no perfil");

        var referencia = linha.Referencia ?? _temperatura.Referencia;
        return linha.PorTemperatura.TryGetValue(temperaturaC, out var fator)
            ? DadoNormativo<decimal>.Com(fator, referencia)
            : DadoNormativo<decimal>.Ausente(referencia, $"temperatura {NumeroEmTexto.Formatar(temperaturaC)} °C não tabelada para {isolacao}");
    }

    /// <summary>
    ///     Fator de agrupamento. Na tabela geral, as chaves são o início de cada faixa (ex.: "9" = 9 a 11, até a próxima
    ///     chave; a última vale dali em diante): vale a maior faixa que não passa do número de circuitos. Na das linhas
    ///     enterradas, a chave é o número exato de circuitos: acima da última, não há fator.
    /// </summary>
    public DadoNormativo<decimal> FatorDeAgrupamento(string metodo, int circuitos)
    {
        if (!Enterrado(metodo)) return FatorDeAgrupamento(circuitos);

        var tabela = _agrupamentoEnterrado!;
        if (tabela.Pendente) return Pendente<decimal>("fator_de_agrupamento_enterrado");
        if (circuitos < 1) return DadoNormativo<decimal>.Ausente(tabela.Referencia, $"número de circuitos inválido ({circuitos})");
        return tabela.Valores.TryGetValue(circuitos, out var fator)
            ? DadoNormativo<decimal>.Com(fator, tabela.Referencia)
            : DadoNormativo<decimal>.Ausente(tabela.Referencia,
                $"sem fator para {circuitos} circuitos em linha enterrada (método {metodo}; a tabela vai até {tabela.Valores.Keys.Max()} circuitos)");
    }

    private DadoNormativo<decimal> FatorDeAgrupamento(int circuitos)
    {
        if (_agrupamento.Pendente) return Pendente<decimal>("fator_de_agrupamento");
        if (circuitos < 1) return DadoNormativo<decimal>.Ausente(_agrupamento.Referencia, $"número de circuitos inválido ({circuitos})");

        return FaixaDeAgrupamento(circuitos) is { } faixa
            ? PorChave(_agrupamento, "fator_de_agrupamento", faixa.Inicio, $"sem fator para {circuitos} circuitos agrupados")
            : DadoNormativo<decimal>.Ausente(_agrupamento.Referencia, $"sem fator para {circuitos} circuitos agrupados");
    }

    /// <summary>
    ///     Faixa da tabela de agrupamento que vale para o método e o número de circuitos (fim nulo = "ou mais"); nula sem
    ///     tabela ou sem faixa. Linha enterrada: a faixa é o próprio número, se tabelado.
    /// </summary>
    public (int Inicio, int? Fim)? FaixaDeAgrupamento(string metodo, int circuitos)
    {
        if (!Enterrado(metodo)) return FaixaDeAgrupamento(circuitos);
        return _agrupamentoEnterrado is { Pendente: false } tabela && tabela.Valores.ContainsKey(circuitos) ? (circuitos, circuitos) : null;
    }

    private (int Inicio, int? Fim)? FaixaDeAgrupamento(int circuitos)
    {
        if (_agrupamento.Pendente || circuitos < 1) return null;

        var inicios = _agrupamento.Valores.Keys.Order().ToList();
        var indice = inicios.FindLastIndex(inicio => inicio <= circuitos);
        if (indice < 0) return null;
        return (inicios[indice], indice + 1 < inicios.Count ? inicios[indice + 1] - 1 : null);
    }

    public DadoNormativo<decimal> QuedaDeTensaoMaximaPct(string aplicacao) =>
        PorChave(_queda, "queda_de_tensao_maxima_pct", aplicacao, $"sem limite para '{aplicacao}'");

    public DadoNormativo<decimal> ResistividadeOhmMm2PorM(string material) =>
        PorChave(_resistividade, "resistividade_ohm_mm2_por_m", material, $"sem resistividade para {material}");

    /// <summary>Taxa máxima de ocupação; acima da maior faixa tabelada vale a maior (ex.: "3" = três ou mais).</summary>
    public DadoNormativo<decimal> OcupacaoMaximaDeEletrodutoPct(int condutores)
    {
        if (_ocupacao.Pendente) return Pendente<decimal>("ocupacao_maxima_eletroduto_pct");
        if (condutores < 1) return DadoNormativo<decimal>.Ausente(_ocupacao.Referencia, $"número de condutores inválido ({condutores})");

        return PorChave(_ocupacao, "ocupacao_maxima_eletroduto_pct", FaixaDeOcupacao(condutores)!.Value, $"sem taxa para {condutores} condutores");
    }

    /// <summary>Faixa da tabela de ocupação que vale para o número de condutores (acima da maior, a maior); nula sem tabela.</summary>
    public int? FaixaDeOcupacao(int condutores) =>
        _ocupacao.Pendente || condutores < 1 ? null : Math.Min(condutores, _ocupacao.Valores.Keys.Max());

    public DadoNormativo<IReadOnlyList<decimal>> CorrentesNominaisDeIdrA() => Inteira(_idr, "correntes_nominais_idr_a");

    /// <summary>Sensibilidades nominais (IΔn) de IDR, em mA: a do projetista precisa ser uma delas.</summary>
    public DadoNormativo<IReadOnlyList<decimal>> SensibilidadesNominaisDeIdrMa() => Inteira(_sensibilidadesDeIdr, "sensibilidades_nominais_idr_ma");

    /// <summary>Tabela inteira de proteção diferencial, por nome de local.</summary>
    public DadoNormativo<IReadOnlyDictionary<string, ProtecaoDiferencialDoLocal>> ProtecaoDiferencialPorLocal() =>
        Inteira(_protecaoDiferencial, "protecao_diferencial_por_local");

    /// <summary>Fator de demanda de um tipo de carga, para o quadro de cargas (F1.4).</summary>
    public DadoNormativo<decimal> FatorDeDemanda(string tipoDeCarga) =>
        PorChave(_fatorDeDemanda, "fator_de_demanda_por_tipo", tipoDeCarga, $"sem fator de demanda para '{tipoDeCarga}'");

    private void LerRegras(Dictionary<string, string>? regras, Leitor leitor, List<string> problemas)
    {
        foreach (var chave in (regras ?? []).Keys.Where(chave => !RegrasPorChave.ContainsKey(chave)))
            problemas.Add($"regra desconhecida '{chave}'");

        foreach (var (chave, regra) in RegrasPorChave)
        {
            var referencia = regras?.GetValueOrDefault(chave)?.Trim();
            if (string.IsNullOrEmpty(referencia))
            {
                if (!RegrasOpcionais.Contains(chave) || regras?.ContainsKey(chave) == true) problemas.Add($"regra {chave} sem ref (use o item da norma ou TODO_NORMA)");
                referencia = TodoNorma;
            }
            else
            {
                leitor.Referencias.Add(($"regra {chave}", referencia));
            }

            _regras[regra] = referencia;
        }
    }

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

    private static IReadOnlyList<string> Distintos(IEnumerable<string> valores) =>
        valores.Where(valor => valor.Length > 0).Distinct(StringComparer.Ordinal).ToList();

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

    /// <param name="Admitidas">Construções admitidas sem condição.</param>
    /// <param name="Condicionais">Construção admitida só sob a condição do texto.</param>
    private sealed record ConstrucoesDoMetodo(IReadOnlyList<string> Admitidas, IReadOnlyDictionary<string, string> Condicionais);

    /// <param name="Referencia">Ref própria da linha (ex.: a coluna da Tabela 38); nula = a da tabela.</param>
    private sealed record LinhaDeCapacidade(
        string Metodo, string Isolacao, string Material, int CondutoresCarregados, IReadOnlyDictionary<decimal, decimal> PorSecao, string? Referencia);

    /// <param name="Metodos">Métodos a que a linha se aplica; nulo = todos.</param>
    /// <param name="Referencia">Ref própria da linha; nula = a da tabela.</param>
    private sealed record LinhaDeTemperatura(string Isolacao, IReadOnlyList<string>? Metodos, IReadOnlyDictionary<decimal, decimal> PorTemperatura, string? Referencia)
    {
        public bool Vale(string metodo) => Metodos is null || Metodos.Contains(metodo, StringComparer.Ordinal);
    }

    /// <summary>Lê as tabelas aplicando a regra 4 e acumulando os problemas.</summary>
    private sealed class Leitor(List<string> problemas)
    {
        public List<(string Nome, string Referencia)> Referencias { get; } = [];

        public void Problema(string problema) => problemas.Add(problema);

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

        public IReadOnlyDictionary<(string Material, string Isolacao), (decimal Ate300, decimal Acima300)> FatorK(List<LinhaDeFatorKJson> linhas, string nome)
        {
            var resultado = new Dictionary<(string, string), (decimal, decimal)>();
            foreach (var linha in linhas)
            {
                var material = linha.Material?.Trim() ?? string.Empty;
                var isolacao = linha.Isolacao?.Trim() ?? string.Empty;
                if (material.Length == 0 || isolacao.Length == 0)
                {
                    problemas.Add($"{nome}: linha sem material ou sem isolação");
                    continue;
                }

                var fator = (Positivo($"{nome} ({material}; {isolacao})", linha.Ate300Mm2 ?? 0m), Positivo($"{nome} ({material}; {isolacao})", linha.AcimaDe300Mm2 ?? 0m));
                if (!resultado.TryAdd((material, isolacao), fator)) problemas.Add($"{nome}: {material} com isolação {isolacao} repetido");
            }

            return resultado;
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

        /// <summary>Fatores no intervalo (0, 1]: fator de demanda não é zero nem majora a potência instalada.</summary>
        public IReadOnlyDictionary<string, decimal> Fracao(Dictionary<string, decimal> valores, string nome)
        {
            var resultado = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var (chave, valor) in valores)
            {
                if (valor <= 0 || valor > 1m) problemas.Add($"{nome}: fator de '{chave}' fora do intervalo (0, 1]");
                else resultado[chave] = valor;
            }

            return resultado;
        }

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

        public IReadOnlyDictionary<string, ConstrucoesDoMetodo> Construcoes(List<LinhaDeConstrucaoJson> linhas, string nome)
        {
            var resultado = new Dictionary<string, ConstrucoesDoMetodo>(StringComparer.Ordinal);
            string? Valida(string? construcao, string metodo)
            {
                var texto = construcao?.Trim();
                if (texto is not null && ConstrucoesDeCondutor.Todas.Contains(texto, StringComparer.Ordinal)) return texto;
                problemas.Add($"{nome}: '{metodo}' com construção desconhecida '{construcao}' (use {string.Join(", ", ConstrucoesDeCondutor.Todas)})");
                return null;
            }

            foreach (var linha in linhas)
            {
                var metodo = linha.Metodo?.Trim();
                if (string.IsNullOrEmpty(metodo))
                {
                    problemas.Add($"{nome}: linha sem método");
                    continue;
                }

                if (resultado.ContainsKey(metodo)) problemas.Add($"{nome}: método '{metodo}' repetido");
                var admitidas = (linha.Construcoes ?? []).Select(construcao => Valida(construcao, metodo)).OfType<string>().ToList();
                if (admitidas.Count == 0) problemas.Add($"{nome}: '{metodo}' sem construção admitida");
                var condicionais = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var (construcao, condicao) in linha.Condicionais ?? [])
                {
                    if (Valida(construcao, metodo) is not { } valida) continue;
                    if (string.IsNullOrWhiteSpace(condicao)) problemas.Add($"{nome}: '{metodo}' com '{valida}' condicional sem a condição");
                    else if (admitidas.Contains(valida)) problemas.Add($"{nome}: '{metodo}' com '{valida}' admitida e condicional");
                    else condicionais[valida] = condicao.Trim();
                }

                resultado[metodo] = new ConstrucoesDoMetodo(admitidas, condicionais);
            }

            return resultado;
        }

        /// <summary>Seção do PE por seção de fase: positiva e nunca maior que a da fase.</summary>
        public IReadOnlyDictionary<decimal, decimal> SecaoDeProtecao(Dictionary<string, decimal> valores, string nome)
        {
            var resultado = PorDecimal(nome, valores);
            foreach (var (fase, protecao) in resultado.Where(par => par.Value > par.Key))
                problemas.Add($"{nome}: seção do condutor de proteção ({NumeroEmTexto.Formatar(protecao)} mm²) maior que a da fase ({NumeroEmTexto.Formatar(fase)} mm²)");
            return resultado;
        }

        public IReadOnlyList<string>? Metodos(string nome, List<string>? metodos)
        {
            if (metodos is null) return null;
            if (metodos.Count == 0 || metodos.Any(string.IsNullOrWhiteSpace)) problemas.Add($"{nome}: lista de métodos vazia ou com método em branco");
            return metodos.Select(metodo => metodo?.Trim() ?? string.Empty).ToList();
        }

        public LinhaDeCapacidade Capacidade(string nome, LinhaDeCapacidadeJson linha)
        {
            if (string.IsNullOrWhiteSpace(linha.Metodo) || string.IsNullOrWhiteSpace(linha.Isolacao) ||
                string.IsNullOrWhiteSpace(linha.Material) || linha.CondutoresCarregados is not > 0)
                problemas.Add($"{nome}: linha sem método, isolação, material ou condutores carregados");

            return new LinhaDeCapacidade(linha.Metodo ?? string.Empty, linha.Isolacao ?? string.Empty, linha.Material ?? string.Empty,
                linha.CondutoresCarregados ?? 0, PorDecimal(nome, linha.PorSecaoMm2),
                RefDaLinha($"{nome} ({linha.Metodo}, {linha.Isolacao}, {linha.Material}, {linha.CondutoresCarregados})", linha.Ref));
        }

        /// <summary>Ref própria de uma linha: ausente = a da tabela; presente, precisa ser fonte (nem vazia nem TODO_NORMA).</summary>
        public string? RefDaLinha(string linha, string? referencia)
        {
            if (referencia is null) return null;
            var texto = referencia.Trim();
            if (texto.Length == 0 || texto == TodoNorma)
            {
                problemas.Add($"{linha}: ref da linha vazia ou TODO_NORMA (linha com valores precisa de fonte)");
                return null;
            }

            Referencias.Add((linha, texto));
            return texto;
        }

        public IReadOnlyDictionary<string, ProtecaoDiferencialDoLocal> ProtecaoDiferencial(List<LinhaDeProtecaoDiferencialJson> linhas, string nome)
        {
            var resultado = new Dictionary<string, ProtecaoDiferencialDoLocal>(StringComparer.Ordinal);
            foreach (var linha in linhas)
            {
                var local = linha.Local?.Trim();
                if (string.IsNullOrEmpty(local))
                {
                    problemas.Add($"{nome}: linha sem local");
                    continue;
                }

                if (resultado.ContainsKey(local)) problemas.Add($"{nome}: local '{local}' repetido");
                if (linha.TiposDeCarga is null) problemas.Add($"{nome}: '{local}' sem tipos_de_carga (use [] se o local não exige IDR)");

                var tipos = new List<TipoDeCarga>();
                foreach (var texto in linha.TiposDeCarga ?? [])
                {
                    if (!CodigosDeTipoDeCarga.TryLer(texto, out var tipo) || tipo == TipoDeCarga.Reserva)
                        problemas.Add($"{nome}: '{local}' com tipo de carga inválido '{texto}'");
                    else if (tipos.Contains(tipo)) problemas.Add($"{nome}: '{local}' com tipo de carga '{texto}' repetido");
                    else tipos.Add(tipo);
                }

                if (linha.TiposDeCarga is { Count: > 0 } && linha.SensibilidadeMaximaMa is not > 0)
                    problemas.Add($"{nome}: '{local}' exige IDR sem sensibilidade_maxima_ma positiva");
                if (linha.TiposDeCarga is { Count: 0 } && linha.SensibilidadeMaximaMa is not null)
                    problemas.Add($"{nome}: '{local}' tem sensibilidade_maxima_ma mas nenhum tipo de carga que exija IDR");

                resultado[local] = new ProtecaoDiferencialDoLocal(local, tipos.Order().ToList(), tipos.Count > 0 ? linha.SensibilidadeMaximaMa : null);
            }

            return resultado;
        }

        public T Positivo<T>(string nome, T valor) where T : System.Numerics.INumber<T>
        {
            if (valor <= T.Zero) problemas.Add($"{nome}: valor não positivo ({NumeroEmTexto.Formatar(decimal.CreateChecked(valor))})");
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
