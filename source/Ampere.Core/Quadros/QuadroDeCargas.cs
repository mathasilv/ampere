using Ampere.Core.Cargas;
using Ampere.Core.Memoria;
using Ampere.Core.Normas;

namespace Ampere.Core.Quadros;

/// <summary>Circuito que entra no quadro de cargas (especificação §3, F1.4).</summary>
/// <param name="Numero">Número do circuito no quadro (ex.: "TUG-01").</param>
/// <param name="Descricao">Descrição livre, para a linha do quadro.</param>
/// <param name="Tipo">Tipo de carga — determina o fator de demanda aplicado.</param>
/// <param name="PotenciaInstaladaVA">Potência instalada do circuito, em VA (≥ 0).</param>
public sealed record CircuitoDoQuadro(string Numero, string? Descricao, TipoDeCarga Tipo, decimal PotenciaInstaladaVA);

/// <summary>Linha do quadro: o circuito com o fator aplicado e a sua demanda — ou o fator que faltou.</summary>
public sealed record LinhaDoQuadroDeCargas(
    string Numero,
    string? Descricao,
    TipoDeCarga Tipo,
    decimal PotenciaInstaladaVA,
    decimal? Fator,
    string? FonteDoFator,
    decimal? DemandaVA);

/// <summary>Subtotal por tipo de carga: potência instalada, fator único e demanda do grupo.</summary>
public sealed record SubtotalDoQuadro(TipoDeCarga Tipo, decimal PotenciaInstaladaVA, decimal Fator, string FonteDoFator, decimal DemandaVA);

/// <summary>Quadro de cargas montado, com a memória de cálculo quando completo.</summary>
/// <param name="DemandaVA">Demanda total; <c>null</c> enquanto qualquer tipo estiver sem fator — soma parcial engana.</param>
/// <param name="CorrenteDeDemandaA">Corrente de demanda do quadro; <c>null</c> sem demanda ou sem esquema calculável.</param>
/// <param name="Memoria">Memória auditável; <c>null</c> quando o quadro está incompleto.</param>
public sealed record ResultadoDoQuadroDeCargas(
    string Nome,
    string Esquema,
    decimal TensaoV,
    IReadOnlyList<LinhaDoQuadroDeCargas> Linhas,
    IReadOnlyList<SubtotalDoQuadro> Subtotais,
    decimal PotenciaInstaladaVA,
    decimal? DemandaVA,
    decimal? CorrenteDeDemandaA,
    MemoriaDeCalculo? Memoria,
    IReadOnlyList<string> Problemas);

/// <summary>
///     Caso de uso "Montar quadro de cargas" (especificação §3, F1.4): demanda por tipo de carga e corrente de
///     demanda do quadro.
/// </summary>
/// <remarks>
///     O fator de demanda vem da tabela <c>fator_de_demanda_por_tipo</c> do perfil ou é informado pelo projetista
///     (o valor informado vence, e a memória registra a fonte). Sem fator em lugar nenhum o quadro fica incompleto
///     com o motivo nos problemas — o motor para, nunca aproxima (AGENTS.md, regra de domínio).
///     Corrente de demanda: I = D / V em "F+N" e "2F", I = D / (√3 · V) em "3F" e "3F+N"; os demais esquemas não são
///     calculados (o aviso vai para a memória e para os problemas).
/// </remarks>
public static class QuadroDeCargas
{
    private const decimal Raiz3 = 1.7320508075688772935274463415m;

    /// <summary>Fonte do fator registrada na memória quando o valor veio do projetista, não do perfil.</summary>
    public const string FonteInformadaPeloProjetista = "valor informado pelo projetista";

    /// <summary>Esquemas cuja corrente de demanda é calculável.</summary>
    public static readonly IReadOnlySet<string> Esquemas = new HashSet<string>(["F+N", "2F", "3F", "3F+N"], StringComparer.Ordinal);

    public static ResultadoDoQuadroDeCargas Montar(
        string nome,
        string esquema,
        decimal tensaoV,
        IReadOnlyList<CircuitoDoQuadro> circuitos,
        PerfilNormativo perfil,
        IReadOnlyDictionary<TipoDeCarga, decimal>? fatoresInformados = null)
    {
        var problemas = new List<string>();
        if (string.IsNullOrWhiteSpace(nome)) problemas.Add("quadro sem nome");
        if (circuitos.Count == 0) problemas.Add("quadro sem circuitos");
        if (!Esquemas.Contains(esquema)) problemas.Add($"esquema desconhecido '{esquema}' (use F+N, 2F, 3F ou 3F+N)");
        if (tensaoV <= 0) problemas.Add($"tensão inválida ({NumeroEmTexto.Formatar(tensaoV)} V)");
        foreach (var duplicado in circuitos.GroupBy(circuito => circuito.Numero.Trim(), StringComparer.Ordinal).Where(grupo => grupo.Count() > 1))
            problemas.Add($"número de circuito repetido no quadro: '{duplicado.Key}'");

        var linhas = new List<LinhaDoQuadroDeCargas>();
        var potenciaInstalada = 0m;
        foreach (var circuito in circuitos)
        {
            if (circuito.PotenciaInstaladaVA < 0)
            {
                problemas.Add($"circuito '{circuito.Numero}': potência instalada negativa");
                linhas.Add(new LinhaDoQuadroDeCargas(circuito.Numero, circuito.Descricao, circuito.Tipo, circuito.PotenciaInstaladaVA, null, null, null));
                continue;
            }

            potenciaInstalada += circuito.PotenciaInstaladaVA;
            linhas.Add(new LinhaDoQuadroDeCargas(circuito.Numero, circuito.Descricao, circuito.Tipo, circuito.PotenciaInstaladaVA, null, null, null));
        }

        // Um fator por tipo, resolvido uma vez: informado vence o perfil (mesma precedência do IDR pela decisão do projetista).
        var fatorPorTipo = new Dictionary<TipoDeCarga, (decimal Fator, string Fonte)>();
        foreach (var tipo in circuitos.Select(circuito => circuito.Tipo).Distinct())
        {
            if (fatoresInformados is not null && fatoresInformados.TryGetValue(tipo, out var informado))
            {
                if (informado <= 0 || informado > 1m)
                {
                    problemas.Add($"fator de demanda informado para {tipo} fora do intervalo (0, 1]");
                    continue;
                }

                fatorPorTipo[tipo] = (informado, FonteInformadaPeloProjetista);
                continue;
            }

            var dado = perfil.FatorDeDemanda(tipo.ToString());
            if (dado.Disponivel) fatorPorTipo[tipo] = (dado.Valor, dado.Referencia);
            else problemas.Add($"sem fator de demanda para {tipo}: {dado.Ausencia}");
        }

        var subtotais = new List<SubtotalDoQuadro>();
        foreach (var tipo in circuitos.Select(circuito => circuito.Tipo).Distinct())
        {
            if (!fatorPorTipo.TryGetValue(tipo, out var resolvido)) continue;

            var potenciaDoTipo = linhas.Where(linha => linha.Tipo == tipo).Sum(linha => linha.PotenciaInstaladaVA);
            subtotais.Add(new SubtotalDoQuadro(tipo, potenciaDoTipo, resolvido.Fator, resolvido.Fonte, potenciaDoTipo * resolvido.Fator));
        }

        // A demanda de cada linha só existe com o fator do seu tipo — reescreve as linhas resolvidas.
        linhas = linhas.Select(linha => fatorPorTipo.TryGetValue(linha.Tipo, out var resolvido)
            ? linha with { Fator = resolvido.Fator, FonteDoFator = resolvido.Fonte, DemandaVA = linha.PotenciaInstaladaVA * resolvido.Fator }
            : linha).ToList();

        var completa = circuitos.Count > 0
                       && fatorPorTipo.Count == circuitos.Select(circuito => circuito.Tipo).Distinct().Count()
                       && circuitos.All(circuito => circuito.PotenciaInstaladaVA >= 0);
        decimal? demandaTotal = completa ? subtotais.Sum(subtotal => subtotal.DemandaVA) : null;

        decimal? corrente = null;
        var correnteCalculavel = demandaTotal is not null && tensaoV > 0;
        if (correnteCalculavel && (esquema == "F+N" || esquema == "2F")) corrente = demandaTotal / tensaoV;
        if (correnteCalculavel && (esquema == "3F" || esquema == "3F+N")) corrente = demandaTotal / (Raiz3 * tensaoV);

        var passos = new List<PassoDeCalculo>();
        if (demandaTotal is not null)
        {
            passos.AddRange(subtotais.Select(subtotal => new PassoDeCalculo(
                subtotal.FonteDoFator,
                $"Demanda do tipo {CodigosDeTipoDeCarga.Codigo(subtotal.Tipo)}",
                "D = ΣPI × fd",
                [new ValorDoPasso($"PI_{subtotal.Tipo}", subtotal.PotenciaInstaladaVA, "VA"), new ValorDoPasso("fd", subtotal.Fator, string.Empty)],
                subtotal.DemandaVA,
                "VA")));
            passos.Add(new PassoDeCalculo(
                perfil.ReferenciaDaRegra(RegraNormativa.DemandaDoQuadro),
                "Demanda total do quadro",
                "D_total = Σ D_tipo",
                subtotais.Select(subtotal => new ValorDoPasso($"D_{subtotal.Tipo}", subtotal.DemandaVA, "VA")).ToList(),
                demandaTotal,
                "VA"));
            if (corrente is not null)
            {
                passos.Add(new PassoDeCalculo(
                    perfil.ReferenciaDaRegra(RegraNormativa.CorrenteDeProjeto),
                    "Corrente de demanda do quadro",
                    esquema is "3F" or "3F+N" ? "I = D_total / (√3 × V)" : "I = D_total / V",
                    [new ValorDoPasso("D_total", demandaTotal.Value, "VA"), new ValorDoPasso("V", tensaoV, "V")],
                    corrente,
                    "A"));
            }
            else
            {
                passos.Add(new PassoDeCalculo(
                    perfil.ReferenciaDaRegra(RegraNormativa.CorrenteDeProjeto),
                    "Corrente de demanda do quadro",
                    "I = D_total / V",
                    [new ValorDoPasso("D_total", demandaTotal.Value, "VA")],
                    null,
                    "A",
                    $"esquema '{esquema}': corrente não calculada" + (tensaoV <= 0 ? " (tensão inválida)" : string.Empty)));
                problemas.Add($"corrente de demanda não calculada (esquema '{esquema}'" + (tensaoV <= 0 ? ", tensão inválida" : string.Empty) + ")");
            }
        }

        var memoria = passos.Count > 0 ? new MemoriaDeCalculo(nome.Trim(), perfil.Nome, passos) : null;

        return new ResultadoDoQuadroDeCargas(nome.Trim(), esquema, tensaoV, linhas, subtotais, potenciaInstalada, demandaTotal, corrente, memoria, problemas);
    }
}
