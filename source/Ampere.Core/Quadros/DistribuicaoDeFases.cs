namespace Ampere.Core.Quadros;

/// <summary>Circuito que a sugestão muda de fase.</summary>
/// <param name="Atuais">Fases que ele ocupa hoje no quadro; nulas se não identificadas no Revit.</param>
public sealed record MudancaDeFase(long Id, string Numero, IReadOnlyList<string>? Atuais, IReadOnlyList<string> Sugeridas);

/// <summary>
///     Distribuição dos circuitos nas fases sugerida para o quadro, pela corrente de cada fase (a do alimentador). Indicador:
///     o Ampere não muda as fases no modelo.
/// </summary>
/// <param name="Fases">Fases do quadro, na ordem das correntes.</param>
/// <param name="CorrentesAntesA">Corrente de cada fase hoje, sem os circuitos sem fase identificada (como em "Cargas por fase").</param>
/// <param name="CorrentesDepoisA">Corrente de cada fase com as mudanças, com todos os circuitos.</param>
/// <param name="Mudancas">Só os circuitos que mudam (ou que hoje estão sem fase identificada), em ordem de número.</param>
/// <param name="PelaDemanda">Correntes pela demanda (<c>true</c>) ou, sem ela, pela potência instalada.</param>
public sealed record SugestaoDeFases(
    IReadOnlyList<string> Fases,
    IReadOnlyList<decimal> CorrentesAntesA,
    IReadOnlyList<decimal> CorrentesDepoisA,
    IReadOnlyList<MudancaDeFase> Mudancas,
    bool PelaDemanda);

/// <summary>
///     Sugestão de fases para equilibrar o quadro: parte da distribuição de hoje (circuitos sem fase entram na fase menos
///     carregada) e muda ou troca circuitos enquanto a fase de maior corrente cair pelo menos 0,1 A, ou, sem piorá-la, a
///     soma dos quadrados das correntes cair pelo menos 1% (as outras fases se aproximam). Mudança que ganha menos que isso
///     não vale o trabalho de mover o circuito no quadro.
/// </summary>
/// <remarks>
///     Corrente de cada fase = soma das correntes de linha dos circuitos nela, como em <see cref="CargasPorFase" />. Circuitos
///     trifásicos ficam onde estão. Determinística: circuitos em ordem de número, fases na ordem do quadro, e a melhor
///     mudança de cada rodada (empate: a primeira encontrada).
/// </remarks>
public static class DistribuicaoDeFases
{
    private const decimal GanhoMinimoA = 0.1m;
    private const decimal GanhoMinimoDosQuadrados = 0.01m;
    private const int MaximoDeRodadas = 500;

    /// <summary>Nula se não há o que distribuir (quadro de uma fase) ou a corrente de algum circuito não sai.</summary>
    /// <param name="fasesDoQuadro">Rótulos das fases do quadro; nulo = os que os circuitos ocupam.</param>
    public static SugestaoDeFases? Sugerir(IReadOnlyList<string>? fasesDoQuadro, decimal? faseNeutroV, IReadOnlyList<CircuitoNasFases> circuitos)
    {
        var fases = (fasesDoQuadro is { Count: > 0 } ? fasesDoQuadro : circuitos.SelectMany(circuito => circuito.Fases ?? []))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        if (fases.Count < 2 || circuitos.Count == 0) return null;

        var pelaDemanda = circuitos.All(circuito => circuito.DemandaVA is not null);
        var itens = new List<Item>();
        foreach (var circuito in circuitos.OrderBy(circuito => circuito.Numero, StringComparer.Ordinal).ThenBy(circuito => circuito.Id))
        {
            var corrente = CargasPorFase.CorrenteDeLinha(circuito, pelaDemanda ? circuito.DemandaVA!.Value : circuito.PotenciaInstaladaVA, faseNeutroV);
            var polos = CargasPorFase.Polos(circuito.Configuracao);
            if (corrente is null || polos is null || polos > fases.Count) return null;

            var opcoes = Combinacoes(fases.Count, polos.Value);
            var atual = circuito.Fases is { } doCircuito && doCircuito.Count == polos && doCircuito.All(fase => fases.Contains(fase, StringComparer.Ordinal))
                ? doCircuito.Select(fase => fases.IndexOf(fase)).Order().ToArray()
                : null;
            itens.Add(new Item(circuito, corrente.Value, opcoes, atual));
        }

        var antes = new decimal[fases.Count];
        foreach (var item in itens.Where(item => item.Atual is not null)) Somar(antes, item.Atual!, item.CorrenteA);

        // Começa de hoje; os sem fase entram, do maior para o menor, onde a maior corrente fica menor.
        var carga = (decimal[])antes.Clone();
        var escolhidas = itens.Select(item => item.Atual).ToArray();
        foreach (var indice in Enumerable.Range(0, itens.Count).Where(indice => escolhidas[indice] is null).OrderByDescending(indice => itens[indice].CorrenteA))
        {
            var melhor = itens[indice].Opcoes.MinBy(opcao => Pontuar(carga, null, 0m, opcao, itens[indice].CorrenteA))!;
            escolhidas[indice] = melhor;
            Somar(carga, melhor, itens[indice].CorrenteA);
        }

        for (var rodada = 0; rodada < MaximoDeRodadas && Melhorar(itens, escolhidas, carga); rodada++)
        {
        }

        var mudancas = itens
            .Select((item, indice) => (Item: item, Sugeridas: escolhidas[indice]!))
            .Where(par => par.Item.Atual is null || !par.Item.Atual.SequenceEqual(par.Sugeridas))
            .Select(par => new MudancaDeFase(par.Item.Circuito.Id, par.Item.Circuito.Numero, par.Item.Atual?.Select(fase => fases[fase]).ToList(),
                par.Sugeridas.Select(fase => fases[fase]).ToList()))
            .ToList();
        return new SugestaoDeFases(fases, antes, carga, mudancas, pelaDemanda);
    }

    // A melhor mudança (um circuito para outras fases) ou troca (dois circuitos de fases diferentes com o mesmo número de
    // polos) desta rodada, aplicada se ganha o mínimo.
    private static bool Melhorar(List<Item> itens, int[]?[] escolhidas, decimal[] carga)
    {
        var atual = Pontuacao(carga);
        (int Indice, int[] Opcao, int Outro, int[]? OutraOpcao, (decimal Maior, decimal Quadrados) Pontos)? melhor = null;

        for (var indice = 0; indice < itens.Count; indice++)
        {
            var item = itens[indice];
            foreach (var opcao in item.Opcoes.Where(opcao => !opcao.SequenceEqual(escolhidas[indice]!)))
            {
                var pontos = Pontuar(carga, escolhidas[indice], item.CorrenteA, opcao, item.CorrenteA);
                if (Ganha(pontos, atual) && (melhor is null || Menor(pontos, melhor.Value.Pontos))) melhor = (indice, opcao, -1, null, pontos);
            }

            for (var outro = indice + 1; outro < itens.Count; outro++)
            {
                var deOutro = escolhidas[outro]!;
                if (itens[outro].Opcoes.Count < 2 || item.Opcoes.Count < 2 || deOutro.Length != escolhidas[indice]!.Length || deOutro.SequenceEqual(escolhidas[indice]!))
                    continue;

                var trocada = (decimal[])carga.Clone();
                Somar(trocada, escolhidas[indice]!, -item.CorrenteA);
                Somar(trocada, deOutro, -itens[outro].CorrenteA);
                Somar(trocada, deOutro, item.CorrenteA);
                Somar(trocada, escolhidas[indice]!, itens[outro].CorrenteA);
                var pontos = Pontuacao(trocada);
                if (Ganha(pontos, atual) && (melhor is null || Menor(pontos, melhor.Value.Pontos))) melhor = (indice, deOutro, outro, escolhidas[indice], pontos);
            }
        }

        if (melhor is not { } escolha) return false;
        Somar(carga, escolhidas[escolha.Indice]!, -itens[escolha.Indice].CorrenteA);
        Somar(carga, escolha.Opcao, itens[escolha.Indice].CorrenteA);
        if (escolha.Outro >= 0)
        {
            Somar(carga, escolhidas[escolha.Outro]!, -itens[escolha.Outro].CorrenteA);
            Somar(carga, escolha.OutraOpcao!, itens[escolha.Outro].CorrenteA);
            escolhidas[escolha.Outro] = escolha.OutraOpcao;
        }

        escolhidas[escolha.Indice] = escolha.Opcao;
        return true;
    }

    private static bool Ganha((decimal Maior, decimal Quadrados) novo, (decimal Maior, decimal Quadrados) atual) =>
        novo.Maior <= atual.Maior - GanhoMinimoA
        || (novo.Maior <= atual.Maior && novo.Quadrados <= atual.Quadrados * (1m - GanhoMinimoDosQuadrados));

    private static bool Menor((decimal Maior, decimal Quadrados) um, (decimal Maior, decimal Quadrados) outro) =>
        um.Maior < outro.Maior || (um.Maior == outro.Maior && um.Quadrados < outro.Quadrados);

    private static (decimal Maior, decimal Quadrados) Pontuar(decimal[] carga, int[]? de, decimal correnteDe, int[] para, decimal correntePara)
    {
        var copia = (decimal[])carga.Clone();
        if (de is not null) Somar(copia, de, -correnteDe);
        Somar(copia, para, correntePara);
        return Pontuacao(copia);
    }

    private static (decimal Maior, decimal Quadrados) Pontuacao(decimal[] carga) => (carga.Max(), carga.Sum(corrente => corrente * corrente));

    private static void Somar(decimal[] carga, int[] fases, decimal corrente)
    {
        foreach (var fase in fases) carga[fase] += corrente;
    }

    // As combinações de 'polos' fases entre as 'quantas' do quadro, em ordem (A; B; C, ou AB; AC; BC).
    private static List<int[]> Combinacoes(int quantas, int polos) => polos switch
    {
        1 => Enumerable.Range(0, quantas).Select(fase => new[] { fase }).ToList(),
        2 => Enumerable.Range(0, quantas).SelectMany(primeira => Enumerable.Range(primeira + 1, quantas - primeira - 1).Select(segunda => new[] { primeira, segunda })).ToList(),
        _ => [Enumerable.Range(0, quantas).ToArray()]
    };

    private sealed record Item(CircuitoNasFases Circuito, decimal CorrenteA, List<int[]> Opcoes, int[]? Atual);
}
