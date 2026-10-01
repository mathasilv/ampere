namespace Ampere.Core.Quadros;

/// <summary>Carga de uma fase do quadro.</summary>
/// <param name="DemandaVA">Nula se o quadro está sem demanda (falta fator para algum tipo).</param>
/// <param name="CorrenteA">
///     Soma das correntes de linha dos circuitos na fase, pela demanda (ou, sem ela, pela potência instalada); nula se a
///     de algum circuito não pôde ser calculada.
/// </param>
public sealed record CargaDaFase(string Fase, decimal PotenciaInstaladaVA, decimal? DemandaVA, decimal? CorrenteA);

/// <summary>
///     Distribuição das cargas do quadro nas fases: a carga de cada fase, a mais carregada e o desequilíbrio.
/// </summary>
/// <param name="PelaDemanda">O desequilíbrio e as correntes usam a demanda (<c>true</c>) ou, sem ela, a potência instalada.</param>
/// <param name="FaseMaisCarregada">A de maior potência (base do desequilíbrio).</param>
/// <param name="DesequilibrioPct">(maior − menor) / maior × 100 das potências das fases do quadro.</param>
/// <param name="MaiorCorrente">A fase de maior corrente; nula se a corrente de alguma fase não pôde ser calculada.</param>
/// <param name="CircuitosSemFase">Circuitos cujas fases no quadro não foram identificadas: ficam fora da conta.</param>
public sealed record BalancoDasFases(
    IReadOnlyList<CargaDaFase> Fases,
    bool PelaDemanda,
    string FaseMaisCarregada,
    decimal DesequilibrioPct,
    CargaDaFase? MaiorCorrente,
    IReadOnlyList<string> CircuitosSemFase);

/// <summary>Circuito do quadro com as fases que ocupa (rótulos do Revit, ex.: A, B, C), a configuração e a tensão dele.</summary>
/// <param name="Configuracao">AMP_Fases do circuito (F+N, 2F, 2F+N, 3F ou 3F+N).</param>
/// <param name="TensaoV">AMP_TensaoCircuitoV: fase-neutro em F+N, fase-fase nas demais.</param>
public sealed record CircuitoNasFases(
    long Id,
    string Numero,
    decimal PotenciaInstaladaVA,
    decimal? DemandaVA,
    IReadOnlyList<string>? Fases,
    string? Configuracao = null,
    decimal? TensaoV = null);

/// <summary>
///     Cargas por fase do quadro. Potência: a de cada circuito dividida igualmente entre as fases que ele ocupa. Corrente:
///     a soma (aritmética, a favor da segurança) das correntes de linha dos circuitos na fase — S / V em F+N e 2F,
///     S / (√3 · V) em 3F e 3F+N, S / (2 · V fase-neutro) em 2F+N. Indicador para o projetista distribuir os circuitos: a
///     NBR 5410 não fixa limite de desequilíbrio, e o Ampere não o trata como critério de norma.
/// </summary>
public static class CargasPorFase
{
    private const decimal Raiz3 = 1.7320508075688772935274463415m;

    /// <summary>
    ///     Nulo se não há o que distribuir: quadro de uma fase, ou nenhum circuito com fase identificada (a lista dos não
    ///     identificados vira problema do quadro).
    /// </summary>
    /// <param name="fasesDoQuadro">Rótulos das fases do quadro; nulo = os que os circuitos ocupam.</param>
    /// <param name="faseNeutroV">Tensão fase-neutro da alimentação, para os circuitos 2F+N; nula se desconhecida.</param>
    public static BalancoDasFases? Calcular(IReadOnlyList<string>? fasesDoQuadro, decimal? faseNeutroV, IReadOnlyList<CircuitoNasFases> circuitos)
    {
        var fases = (fasesDoQuadro is { Count: > 0 } ? fasesDoQuadro : circuitos.SelectMany(circuito => circuito.Fases ?? []))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        var contados = circuitos.Where(circuito => Identificado(circuito, fases)).ToList();
        if (fases.Count < 2 || contados.Count == 0) return null;

        var semFase = circuitos.Where(circuito => !contados.Contains(circuito)).Select(circuito => circuito.Numero).ToList();
        var pelaDemanda = circuitos.Count > 0 && circuitos.All(circuito => circuito.DemandaVA is not null);

        var cargas = fases.Select(fase =>
            {
                var daFase = contados.Where(circuito => circuito.Fases!.Contains(fase, StringComparer.Ordinal)).ToList();
                var instalada = daFase.Sum(circuito => circuito.PotenciaInstaladaVA / circuito.Fases!.Count);
                decimal? demanda = pelaDemanda ? daFase.Sum(circuito => circuito.DemandaVA!.Value / circuito.Fases!.Count) : null;
                var correntes = daFase.Select(circuito => CorrenteDeLinha(circuito, pelaDemanda ? circuito.DemandaVA!.Value : circuito.PotenciaInstaladaVA, faseNeutroV)).ToList();
                decimal? corrente = correntes.All(valor => valor is not null) ? correntes.Sum(valor => valor!.Value) : null;
                return new CargaDaFase(fase, instalada, demanda, corrente);
            })
            .ToList();

        decimal Base(CargaDaFase carga) => pelaDemanda ? carga.DemandaVA!.Value : carga.PotenciaInstaladaVA;
        var maior = cargas.MaxBy(Base)!;
        var menor = cargas.Min(Base);
        var desequilibrio = Base(maior) > 0m ? (Base(maior) - menor) / Base(maior) * 100m : 0m;
        var maiorCorrente = cargas.All(carga => carga.CorrenteA is not null) ? cargas.MaxBy(carga => carga.CorrenteA!.Value) : null;
        return new BalancoDasFases(cargas, pelaDemanda, maior.Fase, desequilibrio, maiorCorrente, semFase);
    }

    /// <summary>Tensão fase-neutro derivada da alimentação, quando o Revit não a informa; nula se não dá para saber.</summary>
    public static decimal? FaseNeutro(string esquema, decimal tensaoV) => esquema switch
    {
        "F+N" => tensaoV,
        "3F+N" => tensaoV / Raiz3,
        _ => null
    };

    // Fases identificadas, todas do quadro, e quantas a configuração do circuito pede (quando informada).
    private static bool Identificado(CircuitoNasFases circuito, List<string> fases) =>
        circuito.Fases is { Count: > 0 } doCircuito
        && doCircuito.All(fase => fases.Contains(fase, StringComparer.Ordinal))
        && (Polos(circuito.Configuracao) is not { } polos || polos == doCircuito.Count);

    private static int? Polos(string? configuracao) => configuracao?.Trim() switch
    {
        "F+N" => 1,
        "2F" or "2F+N" => 2,
        "3F" or "3F+N" => 3,
        _ => null
    };

    private static decimal? CorrenteDeLinha(CircuitoNasFases circuito, decimal potenciaVA, decimal? faseNeutroV) =>
        (circuito.Configuracao?.Trim(), circuito.TensaoV) switch
        {
            ("F+N" or "2F", > 0m and var tensao) => potenciaVA / tensao,
            ("3F" or "3F+N", > 0m and var tensao) => potenciaVA / (Raiz3 * tensao),
            ("2F+N", _) when faseNeutroV is > 0m => potenciaVA / (2m * faseNeutroV.Value),
            _ => null
        };
}
