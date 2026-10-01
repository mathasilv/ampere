using Ampere.Core.Cargas;
using Ampere.Core.Catalogos;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Memoria;
using Ampere.Core.Normas;
using Ampere.Core.Quadros;

namespace Ampere.Core.Alimentadores;

/// <summary>AMP_* de entrada do circuito que alimenta um quadro, como o adapter os lê (nulo = parâmetro vazio).</summary>
public sealed record DadosDoAlimentador(
    long Id,
    decimal? ComprimentoM,
    string? MetodoDeInstalacao,
    string? Isolacao,
    string? TipoDeCondutor,
    DecisoesDoProjetista? Decisoes = null,
    ComprimentoDoRevit? ComprimentoNoRevit = null,
    IReadOnlyList<string>? ProblemasDeLeitura = null);

/// <summary>Quadro do documento e o circuito que o alimenta.</summary>
/// <param name="Alimentador">O circuito de que o quadro é carga; nulo = quadro sem alimentador no Revit.</param>
/// <param name="Origem">Equipamento de onde sai o alimentador (ex.: o quadro geral ou a medição).</param>
/// <param name="OrigemAlimentada">A origem também é alimentada por um circuito do modelo (alimentação em cascata).</param>
/// <param name="AlimentaQuadros">O quadro alimenta outros quadros (a demanda deles não entra no quadro de cargas dele).</param>
/// <param name="Impedimento">
///     O que o modelo tem e o Ampere ainda não calcula (ex.: quadro alimentado por mais de um circuito, circuito
///     alimentador que também alimenta outras cargas, transformador); nulo = nada. O alimentador, se houver, fica apagado.
/// </param>
/// <param name="OutrosAlimentadores">
///     Os demais circuitos de que o quadro é carga (com mais de um, há impedimento): ficam com os resultados apagados.
/// </param>
public sealed record QuadroComAlimentador(
    long QuadroId,
    string Quadro,
    DadosDoAlimentador? Alimentador,
    string? Origem,
    bool OrigemAlimentada,
    bool AlimentaQuadros,
    string? Impedimento = null,
    IReadOnlyList<long>? OutrosAlimentadores = null);

/// <summary>Porta dos alimentadores (implementada pelo adapter Revit): leitura dos quadros e dos terminais, e gravação.</summary>
public interface IDocumentoDeAlimentadores : IDocumentoDeDimensionamento
{
    /// <summary>Quadros com circuitos, cada um com o circuito que o alimenta.</summary>
    IReadOnlyList<QuadroComAlimentador> LerAlimentadores();

    /// <summary>Condições do projeto (da última rodada completa do "Dimensionar circuitos").</summary>
    CondicoesDoProjeto? LerCondicoes();

    /// <summary>Condições da rodada que dimensionou cada circuito (só os que as têm).</summary>
    IReadOnlyDictionary<long, CondicoesDoProjeto> LerCondicoesDosCircuitos(IReadOnlyCollection<long> ids);

    /// <summary>Os AMP_* de resultado gravados nos circuitos (para conferir que os terminais estão em dia no modelo).</summary>
    IReadOnlyDictionary<long, ResultadosNoCircuito> LerResultados(IReadOnlyCollection<long> ids);
}

/// <summary>O alimentador de um quadro: o cálculo (se houve) e o que impediu.</summary>
/// <param name="Circuito">Resultado do circuito alimentador (nulo se o quadro não tem alimentador no Revit).</param>
/// <param name="Problemas">O que impediu o cálculo (o circuito fica com os resultados anteriores apagados).</param>
/// <param name="OutrosApagados">Os demais alimentadores do quadro, sem cálculo: gravados apagados junto.</param>
public sealed record ResultadoDoAlimentador(
    long QuadroId, string Quadro, ResultadoDoCircuito? Circuito, IReadOnlyList<string> Problemas, IReadOnlyList<ResultadoDoCircuito>? OutrosApagados = null);

/// <summary>
///     Caso de uso "Dimensionar alimentadores": o circuito que alimenta cada quadro, pelo mesmo motor dos circuitos
///     terminais, com a demanda do quadro e a queda de tensão que sobra.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Potência: a demanda do quadro de cargas, refeito com os fatores guardados na montagem e conferido pelo hash
///         gravado no quadro (quadro desatualizado, incompleto ou com pendências para o cálculo).</item>
///         <item>IB em 3F e 3F+N: a corrente da fase de maior corrente (soma das correntes de linha dos circuitos nela),
///         mais a dos circuitos sem fase identificada no Revit (podem estar todos nela), nunca a média. Sem a corrente de
///         cada fase, o alimentador para.</item>
///         <item>Queda de tensão em 3F e 3F+N com carga desequilibrada: a da carga mais desfavorecida — fase-neutro, com o
///         retorno pelo neutro (I<sub>N</sub> ≤ maior − menor corrente fase-neutro das fases, premissa de cargas no mesmo
///         fator de potência, declarada na memória), ou entre fases, até 2 · I<sub>B</sub>. Só cargas trifásicas:
///         equilibrada. Critério do Ampere, não item da norma.</item>
///         <item>Limite de queda: o alimentador fica com o que sobra do limite total da instalação (ponto de entrega,
///         transformador ou gerador próprio, tomado na origem do alimentador) depois da maior queda dos circuitos
///         terminais do quadro (reservas fora), refeitos com as condições da rodada que os dimensionou e conferidos com a
///         memória gravada em cada um.</item>
///         <item>Alimentação em cascata (a origem também é alimentada por circuito do modelo) e quadro que alimenta outros
///         quadros ficam de fora, com o motivo: a soma das demandas e das quedas em série ainda não é feita.</item>
///         <item>Gravação numa única transação, como nos terminais: alimentador que não pôde ser calculado fica com os
///         resultados anteriores apagados. As condições do projeto vão junto em cada alimentador calculado.</item>
///     </list>
/// </remarks>
public static class DimensionamentoDeAlimentadores
{
    public const string NomeDaTransacao = "Ampere: dimensionar alimentadores";

    /// <summary>Origens da instalação, na tabela de queda máxima do perfil, com o texto para a memória.</summary>
    public static readonly IReadOnlyDictionary<string, string> Origens = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ponto_de_entrega"] = "instalação alimentada em baixa tensão pela distribuidora (a partir do ponto de entrega)",
        ["transformador_proprio"] = "instalação alimentada por transformador próprio",
        ["transformador_da_distribuidora"] = "ponto de entrega nos terminais secundários do transformador MT/BT da distribuidora",
        ["gerador"] = "instalação alimentada por gerador próprio"
    };

    public static IReadOnlyList<ResultadoDoAlimentador> Executar(
        string origemDaInstalacao, PerfilNormativo perfil, CatalogosDeProduto catalogos, IDocumentoDeAlimentadores documento, IDocumentoDeQuadros quadros)
    {
        var condicoes = documento.LerCondicoes();
        var resultados = Calcular(origemDaInstalacao, perfil, catalogos, documento, quadros);

        var paraGravar = resultados.Select(resultado => resultado.Circuito).OfType<ResultadoDoCircuito>()
            .Concat(resultados.SelectMany(resultado => resultado.OutrosApagados ?? []))
            .ToList();
        if (paraGravar.Count > 0)
        {
            documento.EmUmaTransacao(NomeDaTransacao, () =>
            {
                documento.GravarResultados(paraGravar);
                var calculados = paraGravar.Where(circuito => circuito.Memoria is not null).Select(circuito => circuito.Id).ToList();
                if (condicoes is not null && calculados.Count > 0) documento.GravarCondicoes(condicoes, calculados, doProjetoTodo: false);
            });
        }

        return resultados;
    }

    /// <summary>O mesmo cálculo do <see cref="Executar" />, sem gravar nada (a verificação confere as memórias gravadas).</summary>
    public static IReadOnlyList<ResultadoDoAlimentador> Calcular(
        string origemDaInstalacao, PerfilNormativo perfil, CatalogosDeProduto catalogos, IDocumentoDeAlimentadores documento, IDocumentoDeQuadros quadros)
    {
        if (!Origens.ContainsKey(origemDaInstalacao))
            throw new ArgumentOutOfRangeException(nameof(origemDaInstalacao), origemDaInstalacao, "origem da instalação desconhecida");

        var condicoes = documento.LerCondicoes();
        var lidos = quadros.LerQuadrosComCircuitos().ToDictionary(quadro => quadro.Id);
        return documento.LerAlimentadores()
            .Select(quadro => Alimentador(quadro, origemDaInstalacao, condicoes, lidos, perfil, catalogos, documento, quadros))
            .ToList();
    }

    private static ResultadoDoAlimentador Alimentador(
        QuadroComAlimentador quadro, string origem, CondicoesDoProjeto? condicoes, IReadOnlyDictionary<long, QuadroLido> lidos, PerfilNormativo perfil,
        CatalogosDeProduto catalogos, IDocumentoDeAlimentadores documento, IDocumentoDeQuadros quadros)
    {
        if (quadro.Alimentador is not { } dados)
            return new ResultadoDoAlimentador(quadro.QuadroId, quadro.Quadro, null,
                [quadro.Impedimento ?? "sem circuito alimentador no Revit: ligue o quadro ao quadro de onde ele sai (ou à medição)"]);

        var nome = $"Alimentador {quadro.Quadro}";
        var problemas = new List<string>();
        ResultadoDoAlimentador Apagado() =>
            new(quadro.QuadroId, quadro.Quadro, new ResultadoDoCircuito(dados.Id, nome, null, problemas, quadro.Origem), problemas,
                (quadro.OutrosAlimentadores ?? []).Select(outro => new ResultadoDoCircuito(outro, nome, null, problemas, quadro.Origem)).ToList());

        // O que o Ampere ainda não calcula vem antes: as conferências do quadro e dos terminais só fariam ruído.
        if (quadro.Impedimento is { } impedimento) problemas.Add(impedimento);
        if (condicoes is null) problemas.Add("o modelo não tem as condições do projeto: rode 'Dimensionar circuitos' no projeto todo antes");
        if (quadro.OrigemAlimentada)
            problemas.Add($"alimentação em cascata (a origem {quadro.Origem} também é alimentada por um circuito do modelo): a soma das quedas em série ainda não é feita pelo Ampere");
        if (quadro.AlimentaQuadros)
            problemas.Add("o quadro alimenta outros quadros: a demanda deles não entra no quadro de cargas dele, e a soma ainda não é feita pelo Ampere");
        if (problemas.Count > 0 || condicoes is null) return Apagado();

        if (QuadroDeCargas(quadro, lidos, perfil, quadros, problemas) is not { } quadroDeCargas) return Apagado();
        var correntes = Correntes(quadroDeCargas, lidos[quadro.QuadroId].Alimentacao?.FaseNeutro, problemas);
        if (problemas.Count > 0) return Apagado();
        if (MaiorQuedaTerminal(quadroDeCargas, condicoes, perfil, catalogos, documento, problemas) is not { } terminal) return Apagado();
        if (Entrada(nome, dados, quadroDeCargas, correntes, terminal, origem, quadro.Origem, condicoes, perfil, problemas) is not { } entrada) return Apagado();

        var calculo = DimensionamentoDeCircuito.Dimensionar(entrada, perfil, catalogos);
        return new ResultadoDoAlimentador(quadro.QuadroId, quadro.Quadro,
            new ResultadoDoCircuito(dados.Id, nome, calculo, [], quadro.Origem, null, quadroDeCargas.Quadro.DemandaVA, entrada), []);
    }

    // O quadro de cargas refeito com os fatores guardados, e só se confere com a memória gravada no quadro e não tem
    // pendência (fora as da identificação das fases, que a corrente do alimentador cobre).
    private static ResultadoDoQuadro? QuadroDeCargas(
        QuadroComAlimentador quadro, IReadOnlyDictionary<long, QuadroLido> lidos, PerfilNormativo perfil, IDocumentoDeQuadros quadros, List<string> problemas)
    {
        if (!lidos.TryGetValue(quadro.QuadroId, out var lido))
        {
            problemas.Add("quadro sem circuitos: nada a alimentar");
            return null;
        }

        var gravada = quadros.LerMemoriaDoQuadro(quadro.QuadroId);
        var montado = QuadroDeCargasDoProjeto.Montar(lido, perfil, quadros.LerFatoresDoQuadro(quadro.QuadroId));
        if (montado.Quadro.Memoria is not { } memoria || montado.Quadro.DemandaVA is null)
        {
            problemas.Add($"quadro de cargas incompleto ({string.Join("; ", montado.Quadro.Problemas)}): rode 'Montar quadro de cargas'");
            return null;
        }

        if (gravada is null)
        {
            problemas.Add("quadro de cargas não montado: rode 'Montar quadro de cargas'");
            return null;
        }

        if (memoria.Hash() != gravada.Trim())
        {
            problemas.Add("quadro de cargas desatualizado (o modelo mudou desde a montagem): rode 'Montar quadro de cargas' de novo");
            return null;
        }

        if (!Quadros.QuadroDeCargas.Esquemas.Contains(montado.Quadro.Esquema) || montado.Quadro.TensaoV <= 0m)
        {
            problemas.Add($"alimentação do quadro {montado.Quadro.Esquema} {NumeroEmTexto.Formatar(montado.Quadro.TensaoV)} V: o alimentador é dimensionado em F+N, 2F, 3F ou 3F+N");
            return null;
        }

        var pendencias = montado.Quadro.Problemas.Except(montado.ProblemasDasFases ?? [], StringComparer.Ordinal).ToList();
        if (pendencias.Count > 0)
        {
            problemas.Add($"quadro de cargas com pendências ({string.Join("; ", pendencias)}): a demanda não cobre o quadro todo; corrija e rode 'Montar quadro de cargas'");
            return null;
        }

        return montado;
    }

    // A maior queda entre os circuitos do quadro (reservas fora), refeitos com as condições da rodada que os dimensionou e
    // conferidos com a memória gravada em cada um: o limite do alimentador não pode se apoiar em seção que não está no modelo.
    private static (decimal Pct, string Circuito)? MaiorQuedaTerminal(
        ResultadoDoQuadro quadro, CondicoesDoProjeto doProjeto, PerfilNormativo perfil, CatalogosDeProduto catalogos, IDocumentoDeAlimentadores documento,
        List<string> problemas)
    {
        var ids = quadro.CircuitosDasLinhas.Where((_, indice) => quadro.Quadro.Linhas[indice].Tipo != TipoDeCarga.Reserva).ToList();
        if (ids.Count == 0)
        {
            problemas.Add("quadro só com circuitos reserva: nada a alimentar");
            return null;
        }

        var condicoes = documento.LerCondicoesDosCircuitos(ids);
        var gravados = documento.LerResultados(ids);
        var calculados = documento.LerCircuitos(ids)
            .Select(dados => DimensionamentoDoProjeto.Calcular(dados, condicoes.GetValueOrDefault(dados.Id) ?? doProjeto, perfil, catalogos))
            .ToList();

        // Sem a proteção decidida (IDR), a seção não vai para o modelo: o limite não pode se apoiar nela.
        var semQueda = calculados.Where(circuito => circuito.Dimensionamento is not { QuedaDeTensaoPct: not null, IdrAvaliado: true }).ToList();
        if (semQueda.Count > 0)
        {
            problemas.Add("circuitos do quadro sem queda de tensão ou proteção calculada: " + string.Join("; ", semQueda.Select(circuito =>
                $"{Numero(circuito)} ({string.Join(" | ", circuito.ProblemasDeDados.Count > 0 ? circuito.ProblemasDeDados : circuito.Dimensionamento?.Problemas ?? [])})")));
            return null;
        }

        var desatualizados = calculados
            .Where(circuito => gravados.GetValueOrDefault(circuito.Id)?.MemoriaCalculoId?.Trim() != ResultadosNoCircuito.De(circuito).MemoriaCalculoId)
            .ToList();
        if (desatualizados.Count > 0)
        {
            problemas.Add($"circuitos do quadro com o dimensionamento desatualizado no modelo: {string.Join(", ", desatualizados.Select(Numero))}; rode 'Dimensionar circuitos'");
            return null;
        }

        if (calculados.Count == 0)
        {
            problemas.Add("quadro sem circuitos terminais calculáveis");
            return null;
        }

        var maior = calculados.MaxBy(circuito => circuito.Dimensionamento!.QuedaDeTensaoPct!.Value)!;
        return (maior.Dimensionamento!.QuedaDeTensaoPct!.Value, Numero(maior));
    }

    private static string Numero(ResultadoDoCircuito circuito) => circuito.Numero ?? $"circuito {circuito.Id}";

    // IB e base da queda em 3F e 3F+N, pelas cargas por fase do quadro, a favor da segurança. F+N e 2F: nulos (o motor
    // faz S / V com a demanda, e a queda com k = 2).
    private static (CorrenteCalculada Corrente, CorrenteDaQuedaDeTensao? Queda)? Correntes(ResultadoDoQuadro quadro, decimal? faseNeutroV, List<string> problemas)
    {
        if (quadro.Quadro.Esquema is not ("3F" or "3F+N")) return null;
        if (quadro.Fases is not { } fases)
        {
            problemas.Add("fases dos circuitos no quadro não identificadas no Revit: sem a corrente de cada fase, o alimentador trifásico não é dimensionado");
            return null;
        }

        // Sem as três fases do quadro (rótulos do sistema de distribuição), uma fase vazia some da conta e IN sai menor.
        if (fases.Fases.Count != 3)
        {
            problemas.Add($"o quadro trifásico tem {fases.Fases.Count} fases conhecidas ({string.Join(", ", fases.Fases.Select(fase => fase.Fase))}): atribua a ele o sistema de distribuição no Revit");
            return null;
        }

        if (fases.MaiorCorrente?.CorrenteA is not { } maior || fases.CorrenteSemFaseA is not { } semFase || fases.Fases.Any(fase => fase.CorrenteFaseNeutroA is null))
        {
            problemas.Add("corrente de alguma fase do quadro não calculada (circuito sem AMP_Fases ou AMP_TensaoCircuitoV): confira os circuitos e rode 'Montar quadro de cargas'");
            return null;
        }

        var semFaseIdentificada = fases.CircuitosSemFase.Count > 0;
        var ib = maior + semFase;
        var valores = fases.Fases.Select(fase => new ValorDoPasso($"I({fase.Fase})", fase.CorrenteA!.Value, "A")).ToList();
        if (semFaseIdentificada) valores.Add(new ValorDoPasso("I(sem fase)", semFase, "A"));
        var corrente = new CorrenteCalculada(
            ib,
            $"IB = máx({string.Join("; ", fases.Fases.Select(fase => $"I({fase.Fase})"))})" + (semFaseIdentificada ? " + I(sem fase)" : string.Empty),
            valores,
            $"corrente de cada fase do {quadro.Nome}: soma das correntes de linha dos circuitos nela, pela {(fases.PelaDemanda ? "demanda" : "potência instalada")} " +
            "(F+N e 2F: S / V; 3F e 3F+N: S / (√3 · V); 2F+N: S / (2 · V fase-neutro)), a favor da segurança" +
            (semFaseIdentificada ? $"; sem fase identificada no Revit: {string.Join(", ", fases.CircuitosSemFase)}, somados à fase de maior corrente" : string.Empty) +
            $"; demanda total {NumeroEmTexto.FormatarParaLeitura(quadro.Quadro.DemandaVA!.Value)} VA; quadro de cargas {quadro.Quadro.Memoria!.Hash()}");
        return (corrente, Queda(quadro, fases, ib, faseNeutroV ?? CargasPorFase.FaseNeutro(quadro.Quadro.Esquema, quadro.Quadro.TensaoV)));
    }

    /// <summary>Referência dos passos que são critério do Ampere, não item da norma.</summary>
    public const string CriterioDoAmpere = "Critério do Ampere, não item da norma (data/DATA_GAPS.md, alimentadores)";

    // A carga mais desfavorecida dá a queda: fase-neutro (3F+N), com a fase de maior corrente e o retorno pelo neutro —
    // ΔV fase-neutro ≤ R · (IB + IN), que com V fase-fase é k = V / VFN (√3 na estrela) e IΔV = IB + IN; entre fases,
    // ΔV ≤ R · (I1 + I2) ≤ 2 · R · IB, k = 2 e IΔV = IB. IN ≤ maior − menor corrente fase-neutro das fases, mais a dos
    // circuitos fase-neutro sem fase identificada: vale com as cargas fase-neutro no mesmo fator de potência (fasores a
    // 120°); com fatores diferentes, IN pode passar disso — premissa declarada na memória, não a favor da segurança. Só
    // cargas trifásicas (equilibradas): nulo, a fórmula do motor (k = √3, IB).
    private static CorrenteDaQuedaDeTensao? Queda(ResultadoDoQuadro quadro, BalancoDasFases fases, decimal ib, decimal? faseNeutroV)
    {
        if (fases.Configuracoes.All(configuracao => configuracao is "3F" or "3F+N")) return null;

        var faseNeutro = quadro.Quadro.Esquema == "3F+N" && fases.Configuracoes.Any(CargasPorFase.LigadoAoNeutro);
        var entreFases = fases.Configuracoes.Any(configuracao => configuracao is "2F" or "2F+N" or "3F" or "3F+N");
        var correntesFn = fases.Fases.Select(fase => fase.CorrenteFaseNeutroA!.Value).ToList();
        var semFaseFn = fases.CorrenteFaseNeutroSemFaseA ?? 0m;
        var neutro = correntesFn.Max() - correntesFn.Min() + semFaseFn;
        var fatorFaseNeutro = faseNeutroV is > 0m ? quadro.Quadro.TensaoV / faseNeutroV.Value : Raiz3;
        var porFaseNeutro = faseNeutro ? fatorFaseNeutro * (ib + neutro) : (decimal?)null;
        var porEntreFases = entreFases ? 2m * ib : (decimal?)null;

        var ibTexto = NumeroEmTexto.FormatarParaLeitura(ib);
        if (porFaseNeutro is { } fn && (porEntreFases is not { } ff || fn >= ff))
        {
            var valores = new List<ValorDoPasso> { new("IB", ib, "A") };
            valores.AddRange(fases.Fases.Select(fase => new ValorDoPasso($"IFN({fase.Fase})", fase.CorrenteFaseNeutroA!.Value, "A")));
            if (semFaseFn > 0m) valores.Add(new ValorDoPasso("IFN(sem fase)", semFaseFn, "A"));
            return new CorrenteDaQuedaDeTensao(
                fatorFaseNeutro,
                ib + neutro,
                "IΔV = IB + IN; IN = máx(IFN) − mín(IFN)" + (semFaseFn > 0m ? " + IFN(sem fase)" : string.Empty),
                valores,
                "carga fase-neutro na fase de maior corrente, com o retorno pelo neutro (IFN: corrente dos circuitos F+N e 2F+N em cada fase). " +
                "Premissa: as cargas fase-neutro no mesmo fator de potência; com fatores diferentes, IN pode passar da diferença entre as fases. " +
                $"k = V / VFN = {NumeroEmTexto.FormatarParaLeitura(fatorFaseNeutro)} dá a queda sobre a tensão fase-neutro" +
                (porEntreFases is { } menor ? $"; entre fases, 2 · IB = {NumeroEmTexto.FormatarParaLeitura(menor)} A, menor que k · IΔV" : string.Empty),
                CriterioDoAmpere);
        }

        return new CorrenteDaQuedaDeTensao(
            2m,
            ib,
            "IΔV = IB",
            [new ValorDoPasso("IB", ib, "A")],
            $"carga entre fases com as correntes desequilibradas: a queda entre duas fases chega a R · (I1 + I2) ≤ 2 · R · IB (k = 2, IB = {ibTexto} A)" +
            (porFaseNeutro is { } fnMenor ? $"; fase-neutro com o retorno pelo neutro, k · (IB + IN) = {NumeroEmTexto.FormatarParaLeitura(fnMenor)} A, menor que 2 · IB" : string.Empty),
            CriterioDoAmpere);
    }

    private static EntradaDeDimensionamento? Entrada(
        string nome, DadosDoAlimentador dados, ResultadoDoQuadro quadro, (CorrenteCalculada Corrente, CorrenteDaQuedaDeTensao? Queda)? correntes,
        (decimal Pct, string Circuito) terminal, string origem, string? origemDoAlimentador, CondicoesDoProjeto condicoes, PerfilNormativo perfil,
        List<string> problemas)
    {
        problemas.AddRange(dados.ProblemasDeLeitura ?? []);
        var (comprimento, origemDoComprimento) = EntradaDoCircuito.Comprimento(dados.ComprimentoM, dados.ComprimentoNoRevit, problemas);
        var metodo = EntradaDoCircuito.Preencher(dados.MetodoDeInstalacao, condicoes.MetodoDeInstalacaoPadrao, "AMP_MetodoInstalacao", problemas);
        var isolacao = EntradaDoCircuito.Preencher(dados.Isolacao, condicoes.IsolacaoPadrao, "AMP_MaterialIsolacao", problemas);
        var decisoes = EntradaDoCircuito.Decisoes(dados.Decisoes ?? new DecisoesDoProjetista(), condicoes, metodo is not null && perfil.Enterrado(metodo), problemas);

        var dadoDoTotal = perfil.QuedaDeTensaoMaximaPct(origem);
        if (!dadoDoTotal.Disponivel) problemas.Add($"limite de queda total ({origem}): {dadoDoTotal.Ausencia}");
        else if (dadoDoTotal.Valor - terminal.Pct <= 0m)
            problemas.Add($"os circuitos do quadro já usam {NumeroEmTexto.FormatarParaLeitura(terminal.Pct)}% ({terminal.Circuito}) dos {NumeroEmTexto.Formatar(dadoDoTotal.Valor)}% " +
                          "de queda total: nada sobra para o alimentador (aumente a seção ou encurte esses circuitos)");
        if (problemas.Count > 0) return null;

        var limite = new LimiteDeQuedaDoCircuito(
            dadoDoTotal.Valor - terminal.Pct,
            dadoDoTotal.Referencia,
            "ΔV%máx = ΔV%total − ΔV%terminal",
            [new ValorDoPasso("ΔV%total", dadoDoTotal.Valor, "%"), new ValorDoPasso("ΔV%terminal", terminal.Pct, "%")],
            $"total: {Origens[origem]}, tomado na origem do alimentador ({origemDoAlimentador ?? "sem nome"}): o trecho antes dela não está no modelo; " +
            $"terminal: a maior queda dos circuitos do {quadro.Nome} ({terminal.Circuito})");
        var demanda = quadro.Quadro.DemandaVA!.Value;
        return new EntradaDeDimensionamento(
            nome,
            TipoDeCarga.TUE,
            demanda,
            quadro.Quadro.Esquema,
            quadro.Quadro.TensaoV,
            comprimento!.Value,
            metodo!,
            isolacao!,
            condicoes.Material,
            decisoes.TemperaturaAmbienteC,
            decisoes.CircuitosAgrupados,
            EntradaDoCircuito.Escolher(dados.TipoDeCondutor, condicoes.TipoDeCondutorPadrao),
            EntradaDoCircuito.Escolher(condicoes.TipoDeEletroduto, null),
            [null],
            decisoes.Idr,
            origemDoComprimento,
            decisoes.SecaoMinimaMm2,
            decisoes.DisjuntorA,
            decisoes.Justificativa,
            decisoes.OrigemDaTemperatura,
            decisoes.OrigemDoAgrupamento,
            correntes is null ? $"demanda do {quadro.Nome}; quadro de cargas {quadro.Quadro.Memoria!.Hash()}" : null,
            limite,
            Alimentador: true,
            CorrenteDeProjeto: correntes?.Corrente,
            CorrenteDaQueda: correntes?.Queda);
    }

    private const decimal Raiz3 = 1.7320508075688772935274463415m;
}
