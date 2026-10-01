using Ampere.Core.Cargas;
using Ampere.Core.Normas;

namespace Ampere.Core.Quadros;

/// <summary>Circuito lido do documento, ainda sem validação.</summary>
/// <param name="Id">Identificador do circuito no documento (é por ele que os resultados voltam).</param>
/// <param name="Numero">AMP_NumeroCircuito do sistema (ou o nome nativo).</param>
/// <param name="Tipo">AMP_TipoCarga do sistema (código), se gravado.</param>
/// <param name="PotenciaVA">Soma de AMP_PotenciaInstaladaVA dos membros, se houver.</param>
/// <param name="Fases">AMP_Fases do primeiro membro que tiver (ex.: "F+N").</param>
/// <param name="TensaoV">AMP_TensaoCircuitoV do primeiro membro que tiver.</param>
/// <param name="FasesNoQuadro">Fases do quadro que o circuito ocupa (rótulos do Revit, ex.: A, B); nulo se não identificadas.</param>
public sealed record CircuitoLido(long Id, string? Numero, string? Tipo, decimal? PotenciaVA, string? Fases, decimal? TensaoV,
    IReadOnlyList<string>? FasesNoQuadro = null);

/// <summary>Quadro do documento com os seus circuitos lidos.</summary>
/// <param name="Alimentacao">Alimentação do quadro pelo sistema de distribuição do Revit; nula se o quadro não tem um.</param>
public sealed record QuadroLido(long Id, string Nome, IReadOnlyList<CircuitoLido> Circuitos, AlimentacaoDoQuadro? Alimentacao = null);

/// <summary>Alimentação do quadro, do sistema de distribuição atribuído a ele no Revit.</summary>
/// <param name="Esquema">F+N, 2F, 2F+N, 3F ou 3F+N (o quadro de cargas calcula a corrente de F+N, 2F, 3F e 3F+N).</param>
/// <param name="TensaoV">Tensão de linha (fase-fase); em F+N, a fase-neutro.</param>
/// <param name="Origem">De onde veio, para a memória (ex.: "sistema de distribuição '220/127 Y' do quadro").</param>
/// <param name="Fases">Rótulos das fases do quadro (ex.: A, B, C), para as cargas por fase; nulo = os que os circuitos ocupam.</param>
public sealed record AlimentacaoDoQuadro(string Esquema, decimal TensaoV, string Origem, IReadOnlyList<string>? Fases = null)
{
    // Igualdade pelo conteúdo da lista de fases (o record compararia a referência).
    public bool Equals(AlimentacaoDoQuadro? outra) =>
        outra is not null && Esquema == outra.Esquema && TensaoV == outra.TensaoV && Origem == outra.Origem
        && (Fases ?? []).SequenceEqual(outra.Fases ?? [], StringComparer.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Esquema, TensaoV, Origem, Fases?.Count ?? 0);
}

/// <summary>Resultado do quadro de cargas de um quadro do documento.</summary>
/// <param name="CircuitosDasLinhas">Id do circuito de cada linha de <see cref="ResultadoDoQuadroDeCargas.Linhas" />, na mesma ordem.</param>
/// <param name="ForaDoQuadro">Circuitos lidos que ficaram fora do quadro (sem tipo ou sem potência), com o motivo nos problemas.</param>
/// <param name="Fases">Cargas por fase e desequilíbrio (indicador, fora da memória); nulo sem fases conhecidas.</param>
public sealed record ResultadoDoQuadro(
    long Id,
    string Nome,
    ResultadoDoQuadroDeCargas Quadro,
    IReadOnlyList<long> CircuitosDasLinhas,
    IReadOnlyList<CircuitoLido> ForaDoQuadro,
    BalancoDasFases? Fases = null);

/// <summary>
///     Uma linha a gravar num circuito: potência instalada, fator aplicado e o quadro em que ele está agora (AMP_Quadro, que
///     filtra a tabela do quadro). Nulo apaga o valor anterior — circuito fora do quadro ou sem quadro não fica com o fator
///     nem com o quadro da montagem passada.
/// </summary>
public sealed record LinhaParaGravar(long CircuitoId, string NumeroDoCircuito, decimal? PotenciaVA, decimal? Fator, string? Quadro);

/// <summary>O que a gravação fez: circuitos atualizados e quadros cujo hash o Revit não deixou gravar (grupo ou vínculo).</summary>
public sealed record GravacaoDosQuadros(int CircuitosAtualizados, IReadOnlyList<string> QuadrosSemMemoria);

/// <summary>
///     Porta para os quadros e circuitos de um documento no caso de uso do quadro de cargas (implementada pelo
///     adapter Revit).
/// </summary>
public interface IDocumentoDeQuadros : IDocumentoTransacional
{
    /// <summary>Quadros com circuitos atribuídos; quadro sem circuito fica de fora.</summary>
    IReadOnlyList<QuadroLido> LerQuadrosComCircuitos();

    /// <summary>Grava potência e fator nos circuitos; qualquer falha aborta a transação inteira.</summary>
    void GravarLinhas(IReadOnlyList<LinhaParaGravar> linhas);

    /// <summary>
    ///     Grava no próprio quadro o hash da memória do quadro de cargas (AMP_MemoriaCalculoId do quadro; o do circuito é
    ///     da memória do dimensionamento) e os fatores informados na montagem (para refazer o quadro depois). Nulo apaga o
    ///     anterior. Devolve <c>false</c>, sem gravar, se o quadro não aceita edição (em grupo ou vínculo) — o resto da
    ///     montagem segue.
    /// </summary>
    bool GravarMemoriaDoQuadro(long quadroId, string? hashDaMemoria, IReadOnlyDictionary<TipoDeCarga, decimal>? fatoresInformados);

    /// <summary>Hash da memória do quadro de cargas gravado no quadro (nulo se não há).</summary>
    string? LerMemoriaDoQuadro(long quadroId);

    /// <summary>Fatores informados na montagem que gravou a memória do quadro (nulo se não há; vazio = só os do perfil).</summary>
    IReadOnlyDictionary<TipoDeCarga, decimal>? LerFatoresDoQuadro(long quadroId);

    /// <summary>
    ///     Apaga o hash da memória de quadro de cargas dos quadros que não estão entre os montados (ficaram sem circuitos).
    ///     Devolve os nomes dos que o Revit não deixou editar e continuam com um hash.
    /// </summary>
    IReadOnlyList<string> ApagarMemoriaDosOutrosQuadros(IReadOnlyCollection<long> montados);

    /// <summary>Circuitos de força sem quadro (desconectados, ou com o quadro apagado), para apagar o que a montagem gravou neles.</summary>
    IReadOnlyList<CircuitoLido> LerCircuitosSemQuadro();

    /// <summary>Cria (substituindo se já existir) a tabela do quadro de cargas; devolve o nome da view criada.</summary>
    string CriarTabelaDoQuadro(string nomeDoQuadro);
}

/// <summary>
///     Caso de uso "Montar quadros de cargas" (especificação §3, F1.4): lê os quadros, valida os circuitos e monta
///     cada quadro contra o perfil. Não grava nada — o resultado é para exibir e para os relatórios.
/// </summary>
/// <remarks>
///     Esquema e tensão do quadro vêm do sistema de distribuição atribuído a ele no Revit (a memória registra a origem), e
///     circuito que essa alimentação não fornece (ex.: F+N 220 V num quadro 220/127 V) vira problema. Sem sistema de
///     distribuição, vêm dos próprios circuitos (AMP_Fases e AMP_TensaoCircuitoV uniformes): mistos ou ausentes deixam a
///     corrente sem cálculo, com o motivo nos problemas — o motor não escolhe por conta própria.
/// </remarks>
public static class QuadroDeCargasDoProjeto
{
    /// <summary>Nome da transação de gravação, que aparece no menu Desfazer do Revit.</summary>
    public const string NomeDaTransacao = "Ampere: montar quadro de cargas";
    public static IReadOnlyList<ResultadoDoQuadro> Executar(
        IDocumentoDeQuadros documento,
        PerfilNormativo perfil,
        IReadOnlyDictionary<TipoDeCarga, decimal>? fatoresInformados = null)
    {
        return documento.LerQuadrosComCircuitos().Select(quadro => Montar(quadro, perfil, fatoresInformados)).ToList();
    }

    /// <summary>
    ///     O quadro de cargas de um quadro lido, sem gravar nada: o mesmo da montagem — é por ele que o alimentador refaz o
    ///     quadro com os fatores guardados e confere a memória gravada.
    /// </summary>
    public static ResultadoDoQuadro Montar(QuadroLido quadro, PerfilNormativo perfil, IReadOnlyDictionary<TipoDeCarga, decimal>? fatoresInformados)
    {
        var problemas = new List<string>();
        var circuitos = new List<CircuitoDoQuadro>();
        var idsDasLinhas = new List<long>();
        var foraDoQuadro = new List<CircuitoLido>();
        foreach (var lido in quadro.Circuitos)
        {
            var numero = string.IsNullOrWhiteSpace(lido.Numero) ? "(sem número)" : lido.Numero.Trim();
            if (!CodigosDeTipoDeCarga.TryLer(lido.Tipo, out var tipo))
            {
                problemas.Add($"circuito {numero}: sem AMP_TipoCarga reconhecido (crie os circuitos com o Ampere ou classifique-os)");
                foraDoQuadro.Add(lido);
                continue;
            }

            if (lido.PotenciaVA is not { } potencia)
            {
                problemas.Add($"circuito {numero}: sem potência instalada (classifique os pontos com AMP_PotenciaInstaladaVA)");
                foraDoQuadro.Add(lido);
                continue;
            }

            circuitos.Add(new CircuitoDoQuadro(numero, null, tipo, potencia));
            idsDasLinhas.Add(lido.Id);
        }

        if (quadro.Alimentacao is { } alimentacao)
        {
            Compatibilidade(quadro, alimentacao, problemas);
            var pelaAlimentacao = QuadroDeCargas.Montar(quadro.Nome, alimentacao.Esquema, alimentacao.TensaoV, circuitos, perfil, fatoresInformados, alimentacao.Origem);
            if (problemas.Count > 0) pelaAlimentacao = pelaAlimentacao with { Problemas = [.. pelaAlimentacao.Problemas, .. problemas] };
            return new ResultadoDoQuadro(quadro.Id, quadro.Nome, pelaAlimentacao, idsDasLinhas, foraDoQuadro,
                Fases(quadro, pelaAlimentacao, idsDasLinhas, alimentacao.Fases));
        }

        // Sem sistema de distribuição no quadro, o par (esquema, tensão) precisa ser único entre os circuitos que o informam.
        var pares = quadro.Circuitos
            .Where(lido => lido.Fases is { Length: > 0 } && lido.TensaoV is > 0)
            .Select(lido => (lido.Fases!.Trim(), lido.TensaoV!.Value))
            .Distinct()
            .ToList();
        string esquema;
        decimal tensao;
        switch (pares.Count)
        {
            case 1:
                (esquema, tensao) = pares[0];
                break;
            case 0:
                esquema = "F+N";
                tensao = 0m;
                problemas.Add("nenhum circuito com AMP_Fases e AMP_TensaoCircuitoV: corrente do quadro não calculada");
                break;
            default:
                esquema = "F+N";
                tensao = 0m;
                problemas.Add($"circuitos com esquemas/tensões diferentes ({string.Join(", ", pares.Select(par => $"{par.Item1} {NumeroEmTexto.Formatar(par.Item2)} V"))}) e quadro sem sistema de distribuição no Revit: corrente do quadro não calculada (atribua o sistema de distribuição ao quadro)");
                break;
        }

        // Montar devolve uma linha por circuito, na ordem recebida: é assim que os ids acompanham as linhas.
        var montado = QuadroDeCargas.Montar(quadro.Nome, esquema, tensao, circuitos, perfil, fatoresInformados);
        if (problemas.Count > 0) montado = montado with { Problemas = [.. montado.Problemas, .. problemas] };
        return new ResultadoDoQuadro(quadro.Id, quadro.Nome, montado, idsDasLinhas, foraDoQuadro, Fases(quadro, montado, idsDasLinhas, null));
    }

    // Folga de 2% para as tensões nominais arredondadas (380/220 V: 380 / √3 = 219,4 V).
    private const decimal Folga = 0.02m;

    // Linhas do quadro com as fases que cada circuito ocupa (pelo id, na ordem das linhas).
    private static BalancoDasFases? Fases(QuadroLido quadro, ResultadoDoQuadroDeCargas montado, IReadOnlyList<long> idsDasLinhas, IReadOnlyList<string>? fasesDoQuadro)
    {
        var porId = quadro.Circuitos.ToDictionary(circuito => circuito.Id);
        var circuitos = montado.Linhas
            .Select((linha, indice) => new CircuitoNasFases(linha.Numero, linha.PotenciaInstaladaVA, linha.DemandaVA, porId[idsDasLinhas[indice]].FasesNoQuadro))
            .ToList();
        return CargasPorFase.Calcular(fasesDoQuadro, montado.TensaoV > 0m ? CargasPorFase.FaseNeutro(montado.Esquema, montado.TensaoV) : null, circuitos);
    }

    /// <summary>Circuito com esquema ou tensão que a alimentação do quadro não fornece vira problema (não para a montagem).</summary>
    private static void Compatibilidade(QuadroLido quadro, AlimentacaoDoQuadro alimentacao, List<string> problemas)
    {
        var comNeutro = alimentacao.Esquema.EndsWith("+N", StringComparison.Ordinal);
        var faseNeutro = CargasPorFase.FaseNeutro(alimentacao.Esquema, alimentacao.TensaoV) ?? 0m;
        var fasesDoQuadro = alimentacao.Esquema.StartsWith("3F", StringComparison.Ordinal) ? 3 : alimentacao.Esquema.StartsWith("2F", StringComparison.Ordinal) ? 2 : 1;
        foreach (var circuito in quadro.Circuitos.Where(circuito => circuito.Fases is { Length: > 0 } && circuito.TensaoV is > 0))
        {
            var fases = circuito.Fases!.Trim();
            var tensao = circuito.TensaoV!.Value;
            var (fasesDoCircuito, neutro, esperada) = fases switch
            {
                "F+N" => (1, true, faseNeutro),
                "2F" => (2, false, alimentacao.TensaoV),
                "2F+N" => (2, true, alimentacao.TensaoV),
                "3F" => (3, false, alimentacao.TensaoV),
                "3F+N" => (3, true, alimentacao.TensaoV),
                _ => (0, false, 0m)
            };
            var cabe = fasesDoCircuito > 0 && fasesDoCircuito <= fasesDoQuadro && (!neutro || comNeutro)
                       && (alimentacao.Esquema != "F+N" || fases == "F+N")
                       && Math.Abs(tensao - esperada) <= esperada * Folga;
            if (!cabe)
            {
                problemas.Add($"circuito {circuito.Numero ?? "(sem número)"} ({fases} {NumeroEmTexto.Formatar(tensao)} V) incompatível com a alimentação do quadro " +
                              $"({alimentacao.Esquema} {NumeroEmTexto.Formatar(alimentacao.TensaoV)} V)");
            }
        }
    }

    /// <summary>
    ///     Grava os resultados numa única transação — um único desfazer: em cada circuito, a potência instalada, o fator
    ///     aplicado e o quadro atual; em cada quadro, o hash da memória do quadro (é ela que justifica os fatores). Nada da
    ///     montagem anterior sobrevive com cara de atual: linha sem fator apaga o fator anterior; circuito fora do quadro fica
    ///     com a potência lida (ou nenhuma) e sem fator; circuito sem quadro perde também o AMP_Quadro; quadro incompleto, ou
    ///     que ficou sem circuitos, perde o hash — mesmo quando nenhum quadro tem circuitos. Quadro em grupo ou vínculo fica
    ///     sem o hash e é informado; qualquer outra recusa do Revit aborta tudo.
    /// </summary>
    /// <param name="fatoresInformados">Os fatores usados na montagem: ficam guardados em cada quadro, com o hash da memória.</param>
    public static GravacaoDosQuadros Gravar(
        IReadOnlyList<ResultadoDoQuadro> resultados, IDocumentoDeQuadros documento, IReadOnlyDictionary<TipoDeCarga, decimal>? fatoresInformados = null)
    {
        var linhas = resultados
            .SelectMany(resultado => resultado.Quadro.Linhas
                .Select((linha, indice) => new LinhaParaGravar(resultado.CircuitosDasLinhas[indice], linha.Numero, linha.PotenciaInstaladaVA, linha.Fator, resultado.Nome))
                .Concat(resultado.ForaDoQuadro.Select(lido => new LinhaParaGravar(lido.Id, lido.Numero ?? "(sem número)", lido.PotenciaVA, null, resultado.Nome))))
            .Concat(documento.LerCircuitosSemQuadro().Select(lido => new LinhaParaGravar(lido.Id, lido.Numero ?? "(sem número)", lido.PotenciaVA, null, null)))
            .ToList();

        var semMemoria = new List<string>();
        documento.EmUmaTransacao(NomeDaTransacao, () =>
        {
            if (linhas.Count > 0) documento.GravarLinhas(linhas);
            foreach (var resultado in resultados)
            {
                if (!documento.GravarMemoriaDoQuadro(resultado.Id, resultado.Quadro.Memoria?.Hash(), fatoresInformados)) semMemoria.Add(resultado.Nome);
            }

            semMemoria.AddRange(documento.ApagarMemoriaDosOutrosQuadros(resultados.Select(resultado => resultado.Id).ToList()));
        });
        return new GravacaoDosQuadros(linhas.Count, semMemoria);
    }

    /// <summary>
    ///     Cria a tabela de cada quadro numa única transação: os circuitos com os AMP_* gravados, como view nativa do
    ///     Revit (aparece no navegador de projeto, pode ir para prancha). Tabela já existente é recriada — o comando é
    ///     idempotente.
    /// </summary>
    public static IReadOnlyList<string> CriarTabelas(IReadOnlyList<ResultadoDoQuadro> resultados, IDocumentoDeQuadros documento)
    {
        var nomes = new List<string>();
        documento.EmUmaTransacao(NomeDaTransacao, () => nomes.AddRange(resultados.Select(resultado => documento.CriarTabelaDoQuadro(resultado.Nome))));
        return nomes;
    }
}
