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
public sealed record QuadroComAlimentador(
    long QuadroId,
    string Quadro,
    DadosDoAlimentador? Alimentador,
    string? Origem,
    bool OrigemAlimentada,
    bool AlimentaQuadros);

/// <summary>Porta dos alimentadores (implementada pelo adapter Revit): leitura dos quadros e dos terminais, e gravação.</summary>
public interface IDocumentoDeAlimentadores : IDocumentoDeDimensionamento
{
    /// <summary>Quadros com circuitos, cada um com o circuito que o alimenta.</summary>
    IReadOnlyList<QuadroComAlimentador> LerAlimentadores();

    /// <summary>Condições do projeto (da última rodada completa do "Dimensionar circuitos").</summary>
    CondicoesDoProjeto? LerCondicoes();

    /// <summary>Condições da rodada que dimensionou cada circuito (só os que as têm).</summary>
    IReadOnlyDictionary<long, CondicoesDoProjeto> LerCondicoesDosCircuitos(IReadOnlyCollection<long> ids);
}

/// <summary>O alimentador de um quadro: o cálculo (se houve) e o que impediu.</summary>
/// <param name="Circuito">Resultado do circuito alimentador (nulo se o quadro não tem alimentador no Revit).</param>
/// <param name="Problemas">O que impediu o cálculo (o circuito fica com os resultados anteriores apagados).</param>
public sealed record ResultadoDoAlimentador(long QuadroId, string Quadro, ResultadoDoCircuito? Circuito, IReadOnlyList<string> Problemas);

/// <summary>
///     Caso de uso "Dimensionar alimentadores": o circuito que alimenta cada quadro, pelo mesmo motor dos circuitos
///     terminais, com a demanda do quadro e a queda de tensão que sobra.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Potência: a demanda do quadro de cargas, refeito com os fatores guardados na montagem e conferido pelo hash
///         gravado no quadro (quadro desatualizado ou incompleto para o cálculo). Em 3F e 3F+N com a corrente de todas as
///         fases conhecida, S = √3 · V · I da fase de maior corrente: IB é a corrente dessa fase, não a média.</item>
///         <item>Queda de tensão: o alimentador fica com o que sobra do limite total da instalação (ponto de entrega,
///         transformador ou gerador próprio) depois da maior queda dos circuitos terminais do quadro, refeitos com as
///         condições da rodada que os dimensionou.</item>
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
        ["gerador"] = "instalação alimentada por gerador próprio"
    };

    public static IReadOnlyList<ResultadoDoAlimentador> Executar(
        string origemDaInstalacao, PerfilNormativo perfil, CatalogosDeProduto catalogos, IDocumentoDeAlimentadores documento, IDocumentoDeQuadros quadros)
    {
        if (!Origens.ContainsKey(origemDaInstalacao))
            throw new ArgumentOutOfRangeException(nameof(origemDaInstalacao), origemDaInstalacao, "origem da instalação desconhecida");

        var condicoes = documento.LerCondicoes();
        var lidos = quadros.LerQuadrosComCircuitos().ToDictionary(quadro => quadro.Id);
        var resultados = documento.LerAlimentadores()
            .Select(quadro => Alimentador(quadro, origemDaInstalacao, condicoes, lidos, perfil, catalogos, documento, quadros))
            .ToList();

        var paraGravar = resultados.Select(resultado => resultado.Circuito).OfType<ResultadoDoCircuito>().ToList();
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

    private static ResultadoDoAlimentador Alimentador(
        QuadroComAlimentador quadro, string origem, CondicoesDoProjeto? condicoes, IReadOnlyDictionary<long, QuadroLido> lidos, PerfilNormativo perfil,
        CatalogosDeProduto catalogos, IDocumentoDeAlimentadores documento, IDocumentoDeQuadros quadros)
    {
        if (quadro.Alimentador is not { } dados)
            return new ResultadoDoAlimentador(quadro.QuadroId, quadro.Quadro, null,
                ["sem circuito alimentador no Revit: ligue o quadro ao quadro de onde ele sai (ou à medição)"]);

        var nome = $"Alimentador {quadro.Quadro}";
        var problemas = new List<string>();
        if (condicoes is null) problemas.Add("o modelo não tem as condições do projeto: rode 'Dimensionar circuitos' no projeto todo antes");
        if (quadro.OrigemAlimentada)
            problemas.Add($"alimentação em cascata (a origem {quadro.Origem} também é alimentada por um circuito do modelo): a soma das quedas em série ainda não é feita pelo Ampere");
        if (quadro.AlimentaQuadros)
            problemas.Add("o quadro alimenta outros quadros: a demanda deles não entra no quadro de cargas dele, e a soma ainda não é feita pelo Ampere");

        var quadroDeCargas = QuadroDeCargas(quadro, lidos, perfil, quadros, problemas);
        var terminal = quadroDeCargas is null || condicoes is null ? null : MaiorQuedaTerminal(quadroDeCargas, condicoes, perfil, catalogos, documento, problemas);
        if (problemas.Count > 0 || quadroDeCargas is null || terminal is null || condicoes is null)
            return new ResultadoDoAlimentador(quadro.QuadroId, quadro.Quadro, new ResultadoDoCircuito(dados.Id, nome, null, problemas, quadro.Origem), problemas);

        var entrada = Entrada(nome, dados, quadroDeCargas, terminal.Value, origem, condicoes, perfil, problemas);
        if (entrada is null)
            return new ResultadoDoAlimentador(quadro.QuadroId, quadro.Quadro, new ResultadoDoCircuito(dados.Id, nome, null, problemas, quadro.Origem), problemas);

        var calculo = DimensionamentoDeCircuito.Dimensionar(entrada, perfil, catalogos);
        return new ResultadoDoAlimentador(quadro.QuadroId, quadro.Quadro,
            new ResultadoDoCircuito(dados.Id, nome, calculo, [], quadro.Origem, null, quadroDeCargas.Quadro.DemandaVA, entrada), []);
    }

    // O quadro de cargas refeito com os fatores guardados, e só se confere com a memória gravada no quadro.
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

        return montado;
    }

    // A maior queda entre os circuitos do quadro, refeitos com as condições da rodada que os dimensionou.
    private static (decimal Pct, string Circuito)? MaiorQuedaTerminal(
        ResultadoDoQuadro quadro, CondicoesDoProjeto doProjeto, PerfilNormativo perfil, CatalogosDeProduto catalogos, IDocumentoDeAlimentadores documento,
        List<string> problemas)
    {
        var ids = quadro.CircuitosDasLinhas.Concat(quadro.ForaDoQuadro.Select(circuito => circuito.Id)).Distinct().ToList();
        var condicoes = documento.LerCondicoesDosCircuitos(ids);
        var calculados = documento.LerCircuitos(ids)
            .Select(dados => DimensionamentoDoProjeto.Calcular(dados, condicoes.GetValueOrDefault(dados.Id) ?? doProjeto, perfil, catalogos))
            .ToList();

        var semQueda = calculados.Where(circuito => circuito.Dimensionamento?.QuedaDeTensaoPct is null).ToList();
        if (semQueda.Count > 0)
        {
            problemas.Add("circuitos do quadro sem queda de tensão calculada: " + string.Join("; ", semQueda.Select(circuito =>
                $"{circuito.Numero ?? $"circuito {circuito.Id}"} ({string.Join(" | ", circuito.ProblemasDeDados.Count > 0 ? circuito.ProblemasDeDados : circuito.Dimensionamento?.Problemas ?? [])})")));
            return null;
        }

        if (calculados.Count == 0)
        {
            problemas.Add("quadro sem circuitos terminais calculáveis");
            return null;
        }

        var maior = calculados.MaxBy(circuito => circuito.Dimensionamento!.QuedaDeTensaoPct!.Value)!;
        return (maior.Dimensionamento!.QuedaDeTensaoPct!.Value, maior.Numero ?? $"circuito {maior.Id}");
    }

    private static EntradaDeDimensionamento? Entrada(
        string nome, DadosDoAlimentador dados, ResultadoDoQuadro quadro, (decimal Pct, string Circuito) terminal, string origem, CondicoesDoProjeto condicoes,
        PerfilNormativo perfil, List<string> problemas)
    {
        problemas.AddRange(dados.ProblemasDeLeitura ?? []);
        var (comprimento, origemDoComprimento) = EntradaDoCircuito.Comprimento(dados.ComprimentoM, dados.ComprimentoNoRevit, problemas);
        var metodo = EntradaDoCircuito.Preencher(dados.MetodoDeInstalacao, condicoes.MetodoDeInstalacaoPadrao, "AMP_MetodoInstalacao", problemas);
        var isolacao = EntradaDoCircuito.Preencher(dados.Isolacao, condicoes.IsolacaoPadrao, "AMP_MaterialIsolacao", problemas);
        var decisoes = EntradaDoCircuito.Decisoes(dados.Decisoes ?? new DecisoesDoProjetista(), condicoes, problemas);

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
            $"total: {Origens[origem]}; terminal: a maior queda dos circuitos do {quadro.Nome} ({terminal.Circuito})");
        var (potencia, origemDaPotencia) = Potencia(quadro);
        return new EntradaDeDimensionamento(
            nome,
            TipoDeCarga.TUE,
            potencia,
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
            origemDaPotencia,
            limite,
            Alimentador: true);
    }

    // Em 3F e 3F+N com a corrente de todas as fases conhecida, a fase de maior corrente manda: S = √3 · V · I dela dá
    // IB = I dela (a soma das correntes de linha dos circuitos, a favor da segurança), não a média.
    private static (decimal Potencia, string Origem) Potencia(ResultadoDoQuadro quadro)
    {
        var demanda = quadro.Quadro.DemandaVA!.Value;
        var memoria = quadro.Quadro.Memoria!.Hash();
        var tresFases = quadro.Quadro.Esquema is "3F" or "3F+N";
        if (tresFases && quadro.Fases is { CircuitosSemFase.Count: 0, MaiorCorrente: { CorrenteA: { } corrente } maior } fases)
        {
            return (Raiz3 * quadro.Quadro.TensaoV * corrente,
                $"√3 · V · I da fase de maior corrente do {quadro.Nome} ({maior.Fase}: {NumeroEmTexto.FormatarParaLeitura(corrente)} A, soma das correntes de linha " +
                $"dos circuitos pela {(fases.PelaDemanda ? "demanda" : "potência instalada")}; demanda total {NumeroEmTexto.FormatarParaLeitura(demanda)} VA), " +
                $"para IB ser a corrente dessa fase; quadro de cargas {memoria}");
        }

        var equilibrada = tresFases ? "; corrente de cada fase desconhecida: corrente equilibrada" : string.Empty;
        return (demanda, $"demanda do {quadro.Nome}; quadro de cargas {memoria}{equilibrada}");
    }

    private const decimal Raiz3 = 1.7320508075688772935274463415m;
}
