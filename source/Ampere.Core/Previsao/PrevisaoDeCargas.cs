using Ampere.Core.Cargas;

namespace Ampere.Core.Previsao;

/// <summary>Ponto de carga classificado dentro de um cômodo.</summary>
/// <param name="PotenciaVA">AMP_PotenciaInstaladaVA (nulo = vazio).</param>
public sealed record PontoDoComodo(long Id, TipoDeCarga Tipo, decimal? PotenciaVA);

/// <summary>Um cômodo do modelo (Room ou Space, inclusive de vínculo), com a geometria e os pontos que estão nele.</summary>
/// <param name="Chave">Identifica o cômodo no documento (o mesmo cômodo de um vínculo repetido aparece uma vez por instância).</param>
/// <param name="Nome">Nome do ambiente: a categoria é escolhida por nome.</param>
/// <param name="AreaM2">Área; zero = ambiente não fechado ou não colocado.</param>
public sealed record ComodoDoProjeto(
    string Chave,
    string Nome,
    string? Numero,
    string? Pavimento,
    decimal AreaM2,
    decimal PerimetroM,
    IReadOnlyList<PontoDoComodo> Pontos);

/// <summary>Porta para os cômodos e as categorias guardadas num documento (implementada pelo adapter Revit).</summary>
public interface IDocumentoDePrevisao : IDocumentoTransacional
{
    /// <summary>Cômodos do projeto (Rooms do modelo e dos vínculos ou, sem nenhum, Spaces), com os pontos classificados de cada um.</summary>
    IReadOnlyList<ComodoDoProjeto> LerComodos();

    /// <summary>Categoria guardada para cada nome de ambiente (vazio se o projeto não tem).</summary>
    IReadOnlyDictionary<string, string> LerCategorias();

    /// <summary>Grava as categorias (dentro de uma transação aberta); devolve o motivo se não pôde gravar, ou nulo.</summary>
    string? GravarCategorias(IReadOnlyDictionary<string, string> categoriaPorNome);
}

/// <summary>O que a previsão fez.</summary>
/// <param name="Resultado">Nulo se houve problema (nada foi gravado).</param>
/// <param name="CategoriasNaoGravadas">Por que as categorias não ficaram no modelo (a avaliação vale assim mesmo), ou nulo.</param>
public sealed record ExecucaoDaPrevisao(ResultadoDaPrevisao? Resultado, IReadOnlyList<string> Problemas, string? CategoriasNaoGravadas);

public enum SituacaoDoComodo
{
    /// <summary>Atende à previsão mínima.</summary>
    Atende,

    /// <summary>Atende só pela alternativa de potência admitida quando o conjunto de cômodos molhados passa do limite.</summary>
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

/// <summary>Resultado da previsão: um item por cômodo, na ordem do relatório.</summary>
/// <param name="PontosNoConjunto">Pontos de tomada de uso geral no conjunto dos cômodos da potência maior (banheiros, cozinhas…).</param>
public sealed record ResultadoDaPrevisao(IReadOnlyList<AvaliacaoDoComodo> Comodos, int PontosNoConjunto, NormaDePrevisao Norma)
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
///         limite — o conjunto avaliado é o do projeto todo, então o relatório pede que o projetista confira o da unidade.</item>
///         <item>Ponto sem potência é falta: o cômodo não é dado como atendido com dado faltando.</item>
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
        IReadOnlyList<ComodoDoProjeto> comodos, IReadOnlyDictionary<string, string?> escolhas, NormaDePrevisao norma, IDocumentoDePrevisao documento)
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
        return new ExecucaoDaPrevisao(Avaliar(comodos, categorias, norma), [], motivo);
    }

    /// <param name="categoriaPorNome">Categoria escolhida para cada nome de ambiente (sem diferença de maiúsculas).</param>
    public static ResultadoDaPrevisao Avaliar(IReadOnlyList<ComodoDoProjeto> comodos, IReadOnlyDictionary<string, string> categoriaPorNome, NormaDePrevisao norma)
    {
        var categorias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (nome, categoria) in categoriaPorNome)
        {
            if (!string.IsNullOrWhiteSpace(nome) && !string.IsNullOrWhiteSpace(categoria)) categorias[nome.Trim()] = categoria.Trim();
        }

        string? Categoria(ComodoDoProjeto comodo) => categorias.GetValueOrDefault(comodo.Nome.Trim());

        var conjunto = comodos
            .Where(comodo => comodo.AreaM2 > 0m && norma.Regra(Categoria(comodo)) is { SeiscentosVa: true })
            .Sum(comodo => comodo.Pontos.Count(ponto => ponto.Tipo == TipoDeCarga.TUG));

        var avaliacoes = comodos
            .OrderBy(comodo => comodo.Pavimento ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(comodo => comodo.Nome, StringComparer.Ordinal)
            .ThenBy(comodo => comodo.Numero ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(comodo => comodo.Chave, StringComparer.Ordinal)
            .Select(comodo => Avaliar(comodo, Categoria(comodo), conjunto, norma))
            .ToList();
        return new ResultadoDaPrevisao(avaliacoes, conjunto, norma);
    }

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
            faltas.Add($"{Contagem(tomadas.Count, "ponto de tomada", "pontos de tomada")}, abaixo dos {tomadasMinimas} mínimos ({regra.Descrever()})");

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
                    observacoes.Add($"{descricao}: atende pela alternativa de {naAlternativa}, admitida com mais de {norma.ConjuntoAcimaDePontos} pontos " +
                                    $"no conjunto desses cômodos ({conjunto} nos cômodos avaliados; confira o conjunto da unidade)");
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
