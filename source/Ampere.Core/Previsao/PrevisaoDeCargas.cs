using Ampere.Core.Cargas;

namespace Ampere.Core.Previsao;

/// <summary>Ponto de carga classificado (num cômodo ou fora deles).</summary>
/// <param name="PotenciaVA">AMP_PotenciaInstaladaVA (nulo = vazio).</param>
/// <param name="Circuito">Circuito de força de que o ponto é membro (nulo = fora de circuito).</param>
/// <param name="TensaoV">AMP_TensaoCircuitoV (nulo = vazio), para a corrente do ponto.</param>
/// <param name="Fases">AMP_Fases (nulo = vazio), para a corrente do ponto.</param>
public sealed record PontoDoComodo(long Id, TipoDeCarga Tipo, decimal? PotenciaVA, long? Circuito = null, decimal? TensaoV = null, string? Fases = null);

/// <summary>Um cômodo do modelo (Room ou Space, inclusive de vínculo), com a geometria e os pontos que estão nele.</summary>
/// <param name="Chave">Identifica o cômodo no documento (o mesmo cômodo de um vínculo repetido aparece uma vez por instância).</param>
/// <param name="Nome">Nome do ambiente: a categoria é escolhida por nome.</param>
/// <param name="AreaM2">Área; zero = ambiente não fechado ou não colocado.</param>
/// <param name="Unidade">
///     Onde o cômodo está (o modelo, ou cada instância de vínculo): o conjunto dos cômodos molhados da alternativa de potência
///     é contado por unidade. Nulo = todos numa unidade só.
/// </param>
public sealed record ComodoDoProjeto(
    string Chave,
    string Nome,
    string? Numero,
    string? Pavimento,
    decimal AreaM2,
    decimal PerimetroM,
    IReadOnlyList<PontoDoComodo> Pontos,
    string? Unidade = null);

/// <summary>O que a previsão lê do documento.</summary>
/// <param name="Comodos">Rooms do modelo e dos vínculos ou, sem nenhum, Spaces, com os pontos classificados de cada um.</param>
/// <param name="PontosForaDosComodos">Pontos classificados que não estão em nenhum cômodo (contam na divisão dos circuitos).</param>
/// <param name="Circuitos">Nome de cada circuito de força dos pontos, para o relatório (ex.: "QD1-TUG-03").</param>
public sealed record LeituraDaPrevisao(
    IReadOnlyList<ComodoDoProjeto> Comodos,
    IReadOnlyList<PontoDoComodo> PontosForaDosComodos,
    IReadOnlyDictionary<long, string> Circuitos);

/// <summary>Circuito que não atende à divisão da instalação (9.5.3), com os pontos que causam a falta.</summary>
/// <param name="Regra">"equipamento acima do limite" ou "tomadas de cozinha", para o relatório.</param>
public sealed record FaltaDeDivisao(string Regra, string Circuito, string Descricao, IReadOnlyList<long> Pontos);

/// <summary>Porta para os cômodos e as categorias guardadas num documento (implementada pelo adapter Revit).</summary>
public interface IDocumentoDePrevisao : IDocumentoTransacional
{
    /// <summary>Cômodos, pontos fora deles e nomes dos circuitos.</summary>
    LeituraDaPrevisao Ler();

    /// <summary>Categoria guardada para cada nome de ambiente (vazio se o projeto não tem).</summary>
    IReadOnlyDictionary<string, string> LerCategorias();

    /// <summary>Grava as categorias (dentro de uma transação aberta); devolve o motivo se não pôde gravar, ou nulo.</summary>
    string? GravarCategorias(IReadOnlyDictionary<string, string> categoriaPorNome);
}

/// <summary>
///     A divisão da instalação (9.5.3) para o "Criar circuitos", tirada da previsão de cargas: os pontos em cômodo de
///     habitação e as tomadas que vão em circuitos só delas.
/// </summary>
/// <param name="PontosNaHabitacao">Pontos em cômodo de habitação (com categoria da norma).</param>
/// <param name="TomadasDeCircuitoExclusivo">Tomadas (TUG e TUE) em cômodo de circuito exclusivo (cozinhas, áreas de serviço…).</param>
public sealed record DivisaoDaInstalacao(
    IReadOnlySet<long> PontosNaHabitacao, IReadOnlySet<long> TomadasDeCircuitoExclusivo, decimal CorrenteIndependenteAcimaDeA, string Referencia)
{
    /// <summary>O ponto é equipamento (TUE, ar condicionado, motor) de habitação acima do limite: vai sozinho num circuito.</summary>
    public bool Independente(long id, TipoDeCarga tipo, decimal? potenciaVA, decimal? tensaoV, string? fases) =>
        PontosNaHabitacao.Contains(id) && PrevisaoDeCargas.Equipamentos.Contains(tipo)
                                       && PrevisaoDeCargas.CorrenteA(potenciaVA, tensaoV, fases) > CorrenteIndependenteAcimaDeA;

    /// <summary>O ponto é equipamento de habitação sem a corrente (sem potência, tensão ou fases): a regra não dá para aplicar.</summary>
    public bool SemCorrente(long id, TipoDeCarga tipo, decimal? potenciaVA, decimal? tensaoV, string? fases) =>
        PontosNaHabitacao.Contains(id) && PrevisaoDeCargas.Equipamentos.Contains(tipo) && PrevisaoDeCargas.CorrenteA(potenciaVA, tensaoV, fases) is null;
}

/// <summary>O que a previsão fez.</summary>
/// <param name="Resultado">Nulo se houve problema (nada foi gravado).</param>
/// <param name="CategoriasNaoGravadas">Por que as categorias não ficaram no modelo (a avaliação vale assim mesmo), ou nulo.</param>
public sealed record ExecucaoDaPrevisao(ResultadoDaPrevisao? Resultado, IReadOnlyList<string> Problemas, string? CategoriasNaoGravadas);

public enum SituacaoDoComodo
{
    /// <summary>Atende à previsão mínima.</summary>
    Atende,

    /// <summary>
    ///     Atende só pela alternativa de potência admitida quando o conjunto de cômodos molhados passa do limite — o conjunto
    ///     contado é o do modelo ou do vínculo; o projetista confere se é o da unidade habitacional.
    /// </summary>
    AtendePelaAlternativa,

    NaoAtende,

    /// <summary>O projetista ainda não escolheu a categoria do cômodo.</summary>
    SemCategoria,

    /// <summary>O projetista disse que o cômodo não é local de habitação.</summary>
    ForaDaHabitacao,

    /// <summary>Ambiente sem área (não fechado ou não colocado).</summary>
    SemArea
}

/// <summary>Avaliação de um cômodo: o mínimo da norma, o que o modelo tem e o que falta.</summary>
/// <param name="IluminacaoMinimaVA">Nula quando o cômodo não é avaliado.</param>
/// <param name="TomadasMinimas">Pontos de tomada mínimos; nulo quando o cômodo não é avaliado.</param>
/// <param name="Faltas">O que não atende (vazio = atende).</param>
/// <param name="Observacoes">O que o relatório precisa dizer além das faltas (ex.: a alternativa usada, a nota da norma).</param>
public sealed record AvaliacaoDoComodo(
    ComodoDoProjeto Comodo,
    string? Categoria,
    SituacaoDoComodo Situacao,
    decimal? IluminacaoMinimaVA,
    decimal IluminacaoNoModeloVA,
    int PontosDeLuz,
    int? TomadasMinimas,
    int Tomadas,
    IReadOnlyList<string> Faltas,
    IReadOnlyList<string> Observacoes);

/// <summary>Resultado da previsão: um item por cômodo, na ordem do relatório, e as faltas na divisão dos circuitos.</summary>
/// <param name="PontosNoConjunto">
///     Pontos de tomada de uso geral no conjunto dos cômodos da potência maior (banheiros, cozinhas…), por unidade (vazio = a
///     unidade sem nome).
/// </param>
/// <param name="Divisao">Circuitos dos cômodos de habitação que não atendem à divisão da instalação, na ordem dos nomes.</param>
public sealed record ResultadoDaPrevisao(
    IReadOnlyList<AvaliacaoDoComodo> Comodos, IReadOnlyDictionary<string, int> PontosNoConjunto, NormaDePrevisao Norma, IReadOnlyList<FaltaDeDivisao> Divisao)
{
    public int Contar(SituacaoDoComodo situacao) => Comodos.Count(comodo => comodo.Situacao == situacao);
}

/// <summary>
///     Caso de uso "Previsão de cargas": confere cada cômodo de habitação contra a previsão mínima da NBR 5410 (9.5.2) —
///     carga de iluminação pela área, número de pontos de tomada pelo perímetro ou pela área e potência mínima de cada ponto
///     de tomada. A categoria de cada cômodo é escolha do projetista, por nome de ambiente; o Ampere não a deduz do nome.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Iluminação: soma das potências dos pontos de Iluminação do cômodo; pontos de luz contados. A alternativa da
///         ABNT NBR 5413 não é avaliada.</item>
///         <item>Tomadas: só os pontos TUG (a tomada de uso específico, TUE, atende a um aparelho e não entra na contagem).
///         Potência: nos cômodos da potência maior, os primeiros pontos (os de maior potência) precisam dela e os demais da
///         menor; a alternativa da norma (menos pontos com a potência maior) só vale quando o conjunto desses cômodos passa do
///         limite — contado no modelo ou em cada instância de vínculo, que pode não ser a unidade habitacional: a situação
///         própria pede que o projetista confira.</item>
///         <item>Cômodo em que a norma admite a tomada fora dele (varanda, cômodo pequeno): continua com a falta, e a
///         observação diz a admissão, que o modelo não permite conferir.</item>
///         <item>Ponto sem potência é falta: o cômodo não é dado como atendido com dado faltando.</item>
///         <item>Divisão (9.5.3), só para pontos em cômodos de habitação: equipamento acima do limite de corrente (TUE, ar
///         condicionado e motor — critério do Ampere para "equipamento", a TUG e a iluminação não são de um equipamento)
///         num circuito com outros pontos; tomada TUG de cozinha ou área de serviço num circuito com ponto que não é TUG
///         desses cômodos (inclusive ponto fora de cômodo). Corrente do ponto: P / V, ou P / (√3 · V) no trifásico; sem
///         potência ou tensão, o ponto não entra.</item>
///     </list>
/// </remarks>
public static class PrevisaoDeCargas
{
    /// <summary>Categoria do diálogo para o cômodo que não é local de habitação (não avaliado).</summary>
    public const string ForaDaHabitacao = "Não é local de habitação";

    /// <summary>Categorias para o diálogo: as da norma, na ordem do arquivo, e a de fora da habitação.</summary>
    public static IReadOnlyList<string> Categorias(NormaDePrevisao norma) => [.. norma.Comodos.Select(regra => regra.Comodo), ForaDaHabitacao];

    /// <summary>Nome da transação, que aparece no menu Desfazer do Revit.</summary>
    public const string NomeDaTransacao = "Ampere: categorias dos cômodos";

    /// <summary>
    ///     Guarda as escolhas do projetista junto com as que o projeto já tinha (escolha vazia apaga a do nome) e avalia os
    ///     cômodos. Categoria fora da norma é problema: nada é gravado.
    /// </summary>
    /// <param name="escolhas">Categoria escolhida no diálogo para cada nome de ambiente; vazia = sem categoria.</param>
    public static ExecucaoDaPrevisao Executar(
        LeituraDaPrevisao leitura, IReadOnlyDictionary<string, string?> escolhas, NormaDePrevisao norma, IDocumentoDePrevisao documento)
    {
        var validas = Categorias(norma);
        var problemas = escolhas
            .Where(par => !string.IsNullOrWhiteSpace(par.Value) && !validas.Contains(par.Value.Trim(), StringComparer.Ordinal))
            .Select(par => $"{par.Key}: categoria '{par.Value}' fora da previsão de cargas da norma")
            .ToList();
        if (problemas.Count > 0) return new ExecucaoDaPrevisao(null, problemas, null);

        var categorias = new Dictionary<string, string>(documento.LerCategorias(), StringComparer.OrdinalIgnoreCase);
        foreach (var (nome, categoria) in escolhas)
        {
            if (string.IsNullOrWhiteSpace(nome)) continue;
            if (string.IsNullOrWhiteSpace(categoria)) categorias.Remove(nome.Trim());
            else categorias[nome.Trim()] = categoria.Trim();
        }

        string? motivo = null;
        documento.EmUmaTransacao(NomeDaTransacao, () => motivo = documento.GravarCategorias(categorias));
        return new ExecucaoDaPrevisao(Avaliar(leitura, categorias, norma), [], motivo);
    }

    /// <param name="categoriaPorNome">Categoria escolhida para cada nome de ambiente (sem diferença de maiúsculas).</param>
    public static ResultadoDaPrevisao Avaliar(LeituraDaPrevisao leitura, IReadOnlyDictionary<string, string> categoriaPorNome, NormaDePrevisao norma)
    {
        var comodos = leitura.Comodos;
        var categorias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (nome, categoria) in categoriaPorNome)
        {
            if (!string.IsNullOrWhiteSpace(nome) && !string.IsNullOrWhiteSpace(categoria)) categorias[nome.Trim()] = categoria.Trim();
        }

        string? Categoria(ComodoDoProjeto comodo) => categorias.GetValueOrDefault(comodo.Nome.Trim());

        var conjunto = comodos
            .Where(comodo => comodo.AreaM2 > 0m && norma.Regra(Categoria(comodo)) is { SeiscentosVa: true })
            .GroupBy(comodo => comodo.Unidade ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Sum(comodo => comodo.Pontos.Count(ponto => ponto.Tipo == TipoDeCarga.TUG)), StringComparer.Ordinal);

        var avaliacoes = comodos
            .OrderBy(comodo => comodo.Pavimento ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(comodo => comodo.Nome, StringComparer.Ordinal)
            .ThenBy(comodo => comodo.Numero ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(comodo => comodo.Chave, StringComparer.Ordinal)
            .Select(comodo => Avaliar(comodo, Categoria(comodo), conjunto.GetValueOrDefault(comodo.Unidade ?? string.Empty), norma))
            .ToList();
        // Um ponto fica num cômodo só (o adapter acha um); se vier repetido, vale o primeiro, sem exceção.
        var regraDoPonto = new Dictionary<long, RegraDoComodo>();
        foreach (var comodo in comodos.Where(comodo => comodo.AreaM2 > 0m))
        {
            if (norma.Regra(Categoria(comodo)) is not { } regra) continue;
            foreach (var ponto in comodo.Pontos) regraDoPonto.TryAdd(ponto.Id, regra);
        }
        return new ResultadoDaPrevisao(avaliacoes, conjunto, norma, Divisao(leitura, regraDoPonto, norma));
    }

    /// <summary>Tipos de carga tratados como equipamento no limite de corrente do circuito independente (critério do Ampere).</summary>
    public static IReadOnlyList<TipoDeCarga> Equipamentos { get; } = [TipoDeCarga.TUE, TipoDeCarga.ArCondicionado, TipoDeCarga.Motor];

    /// <summary>A divisão da instalação para o "Criar circuitos", com as categorias guardadas (vazias = nada a dividir).</summary>
    public static DivisaoDaInstalacao DivisaoParaCircuitos(LeituraDaPrevisao leitura, IReadOnlyDictionary<string, string> categoriaPorNome, NormaDePrevisao norma)
    {
        var categorias = new Dictionary<string, string>(categoriaPorNome.Where(par => !string.IsNullOrWhiteSpace(par.Key) && !string.IsNullOrWhiteSpace(par.Value))
            .ToDictionary(par => par.Key.Trim(), par => par.Value.Trim(), StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        var naHabitacao = new HashSet<long>();
        var exclusivas = new HashSet<long>();
        foreach (var comodo in leitura.Comodos.Where(comodo => comodo.AreaM2 > 0m))
        {
            if (norma.Regra(categorias.GetValueOrDefault(comodo.Nome.Trim())) is not { } regra) continue;
            foreach (var ponto in comodo.Pontos)
            {
                naHabitacao.Add(ponto.Id);
                if (regra.CircuitoExclusivo && ponto.Tipo is TipoDeCarga.TUG or TipoDeCarga.TUE) exclusivas.Add(ponto.Id);
            }
        }

        return new DivisaoDaInstalacao(naHabitacao, exclusivas, norma.CorrenteIndependenteAcimaDeA, norma.ReferenciaDaDivisao);
    }

    /// <summary>Corrente do ponto: P / V, ou P / (√3 · V) no trifásico; nula sem potência ou tensão.</summary>
    public static decimal? CorrenteA(decimal? potenciaVA, decimal? tensaoV, string? fases)
    {
        if (potenciaVA is not { } potencia || tensaoV is not > 0m) return null;
        var trifasico = fases is { } texto && texto.StartsWith("3F", StringComparison.Ordinal);
        return potencia / (tensaoV.Value * (trifasico ? Raiz3 : 1m));
    }

    private static List<FaltaDeDivisao> Divisao(LeituraDaPrevisao leitura, IReadOnlyDictionary<long, RegraDoComodo> regraDoPonto, NormaDePrevisao norma)
    {
        var faltas = new List<FaltaDeDivisao>();
        var porCircuito = leitura.Comodos.SelectMany(comodo => comodo.Pontos).Concat(leitura.PontosForaDosComodos)
            .Where(ponto => ponto.Circuito is not null)
            .GroupBy(ponto => ponto.Circuito!.Value)
            .Select(grupo => (Nome: leitura.Circuitos.GetValueOrDefault(grupo.Key) ?? $"circuito {grupo.Key}", Pontos: grupo.OrderBy(ponto => ponto.Id).ToList()))
            .OrderBy(circuito => circuito.Nome, StringComparer.Ordinal);
        bool NaHabitacao(PontoDoComodo ponto) => regraDoPonto.ContainsKey(ponto.Id);
        // Ponto de tomada (TUG ou TUE) num cômodo de circuito exclusivo: a TUE acima do limite já cai na regra anterior.
        bool DeCozinha(PontoDoComodo ponto) =>
            (ponto.Tipo is TipoDeCarga.TUG or TipoDeCarga.TUE) && regraDoPonto.GetValueOrDefault(ponto.Id) is { CircuitoExclusivo: true };

        foreach (var (nome, pontos) in porCircuito)
        {
            var acimaDoLimite = pontos
                .Where(ponto => NaHabitacao(ponto) && Equipamentos.Contains(ponto.Tipo))
                .Select(ponto => (Ponto: ponto, Corrente: Corrente(ponto)))
                .Where(par => par.Corrente > norma.CorrenteIndependenteAcimaDeA)
                .ToList();
            if (acimaDoLimite.Count > 0 && pontos.Count > 1)
            {
                var correntes = string.Join(", ", acimaDoLimite.Select(par => $"{N(Math.Round(par.Corrente!.Value, 2, MidpointRounding.AwayFromZero))} A"));
                faltas.Add(new FaltaDeDivisao("equipamento acima do limite", nome,
                    $"{Contagem(acimaDoLimite.Count, "equipamento", "equipamentos")} acima de {N(norma.CorrenteIndependenteAcimaDeA)} A ({correntes}) num circuito de " +
                    $"{pontos.Count} pontos: cada um precisa de circuito independente",
                    acimaDoLimite.Select(par => par.Ponto.Id).ToList()));
            }

            if (pontos.Any(ponto => ponto.Tipo == TipoDeCarga.TUG && DeCozinha(ponto)) && pontos.Where(ponto => !DeCozinha(ponto)).ToList() is { Count: > 0 } outros)
            {
                faltas.Add(new FaltaDeDivisao("tomadas de cozinha", nome,
                    $"tomadas de cozinha ou área de serviço com {Contagem(outros.Count, "ponto", "pontos")} de outro tipo ou de outro cômodo: " +
                    "essas tomadas vão em circuitos só delas",
                    outros.Select(ponto => ponto.Id).ToList()));
            }
        }

        return faltas;
    }

    private static decimal? Corrente(PontoDoComodo ponto) => CorrenteA(ponto.PotenciaVA, ponto.TensaoV, ponto.Fases);

    private const decimal Raiz3 = 1.7320508075688772935274463415m;

    private static AvaliacaoDoComodo Avaliar(ComodoDoProjeto comodo, string? categoria, int conjunto, NormaDePrevisao norma)
    {
        var luzes = comodo.Pontos.Where(ponto => ponto.Tipo == TipoDeCarga.Iluminacao).ToList();
        var tomadas = comodo.Pontos.Where(ponto => ponto.Tipo == TipoDeCarga.TUG).ToList();
        var iluminacao = luzes.Sum(ponto => ponto.PotenciaVA ?? 0m);

        AvaliacaoDoComodo NaoAvaliado(SituacaoDoComodo situacao, string? observacao) =>
            new(comodo, categoria, situacao, null, iluminacao, luzes.Count, null, tomadas.Count, [], observacao is null ? [] : [observacao]);

        if (categoria is null) return NaoAvaliado(SituacaoDoComodo.SemCategoria, null);
        if (categoria == ForaDaHabitacao) return NaoAvaliado(SituacaoDoComodo.ForaDaHabitacao, null);
        if (norma.Regra(categoria) is not { } regra) return NaoAvaliado(SituacaoDoComodo.SemCategoria, $"categoria '{categoria}' fora da norma: escolha de novo");
        if (comodo.AreaM2 <= 0m) return NaoAvaliado(SituacaoDoComodo.SemArea, "ambiente sem área (não fechado ou não colocado)");

        var faltas = new List<string>();
        var observacoes = new List<string>();

        var iluminacaoMinima = norma.IluminacaoMinimaVA(comodo.AreaM2);
        if (luzes.Count < norma.PontosDeLuzMinimos)
            faltas.Add($"{Contagem(luzes.Count, "ponto de luz", "pontos de luz")}, abaixo do mínimo de {norma.PontosDeLuzMinimos}");
        if (iluminacao < iluminacaoMinima)
            faltas.Add($"iluminação de {N(iluminacao)} VA, abaixo dos {N(iluminacaoMinima)} VA mínimos");

        var tomadasMinimas = regra.PontosMinimos(comodo.AreaM2, comodo.PerimetroM);
        if (tomadas.Count < tomadasMinimas)
        {
            faltas.Add($"{Contagem(tomadas.Count, "ponto de tomada", "pontos de tomada")}, abaixo dos {tomadasMinimas} mínimos ({regra.Descrever()})");
            if (regra.TomadaForaDoComodo is { } admissao && (regra.TomadaForaDoComodoAteM2 is not { } ate || comodo.AreaM2 <= ate))
                observacoes.Add($"{admissao}: confira se é o caso");
        }

        var semPotencia = comodo.Pontos.Count(ponto => (ponto.Tipo is TipoDeCarga.Iluminacao or TipoDeCarga.TUG) && ponto.PotenciaVA is null);
        if (semPotencia > 0) faltas.Add($"{Contagem(semPotencia, "ponto", "pontos")} sem AMP_PotenciaInstaladaVA");

        // Só as potências informadas: o ponto sem potência já é falta acima.
        var potencias = tomadas.Where(ponto => ponto.PotenciaVA is not null).Select(ponto => ponto.PotenciaVA!.Value).OrderDescending().ToList();
        var abaixoDoMinimo = potencias.Count(potencia => potencia < norma.DemaisVA);
        if (abaixoDoMinimo > 0)
            faltas.Add($"{Contagem(abaixoDoMinimo, "ponto de tomada", "pontos de tomada")} abaixo de {N(norma.DemaisVA)} VA");

        var situacao = faltas.Count > 0 ? SituacaoDoComodo.NaoAtende : SituacaoDoComodo.Atende;
        if (regra.SeiscentosVa)
        {
            var comAPotenciaMaior = potencias.Count(potencia => potencia >= norma.SeiscentosVA);
            var exigidos = Math.Min(potencias.Count, norma.PontosDeSeiscentos);
            var naAlternativa = Math.Min(potencias.Count, norma.PontosDeSeiscentosNaAlternativa);
            if (comAPotenciaMaior < exigidos)
            {
                var descricao = $"{Contagem(comAPotenciaMaior, "ponto de tomada", "pontos de tomada")} com {N(norma.SeiscentosVA)} VA ou mais, de {exigidos} exigidos";
                if (comAPotenciaMaior >= naAlternativa && conjunto > norma.ConjuntoAcimaDePontos)
                {
                    var onde = comodo.Unidade is { Length: > 0 } unidade ? $"em {unidade}" : "nos cômodos avaliados";
                    observacoes.Add($"{descricao}: atende pela alternativa de {naAlternativa}, admitida com mais de {norma.ConjuntoAcimaDePontos} pontos " +
                                    $"no conjunto desses cômodos da unidade ({conjunto} {onde}; confira se é uma unidade só)");
                    if (situacao == SituacaoDoComodo.Atende) situacao = SituacaoDoComodo.AtendePelaAlternativa;
                }
                else
                {
                    faltas.Add(descricao);
                    situacao = SituacaoDoComodo.NaoAtende;
                }
            }
        }

        if (regra.Nota is { } nota) observacoes.Add(nota);
        return new AvaliacaoDoComodo(comodo, regra.Comodo, situacao, iluminacaoMinima, iluminacao, luzes.Count, tomadasMinimas, tomadas.Count, faltas, observacoes);
    }

    private static string Contagem(int quantidade, string singular, string plural) => $"{quantidade} {(quantidade == 1 ? singular : plural)}";

    private static string N(decimal valor) => NumeroEmTexto.FormatarParaLeitura(valor);
}
