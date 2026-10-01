namespace Ampere.Core.Quadros;

/// <summary>Carga de uma fase do quadro.</summary>
/// <param name="DemandaVA">Nula se o quadro está sem demanda (falta fator para algum tipo).</param>
/// <param name="CorrenteA">Corrente da fase pela demanda (ou, sem ela, pela potência instalada) e a tensão fase-neutro; nula sem neutro.</param>
public sealed record CargaDaFase(string Fase, decimal PotenciaInstaladaVA, decimal? DemandaVA, decimal? CorrenteA);

/// <summary>
///     Distribuição das cargas do quadro nas fases: a carga de cada fase, a mais carregada e o desequilíbrio.
/// </summary>
/// <param name="PelaDemanda">O desequilíbrio e as correntes usam a demanda (<c>true</c>) ou, sem ela, a potência instalada.</param>
/// <param name="DesequilibrioPct">(maior − menor) / maior × 100, entre as fases do quadro.</param>
/// <param name="CircuitosSemFase">Circuitos cujas fases no quadro não foram identificadas: ficam fora da conta.</param>
public sealed record BalancoDasFases(
    IReadOnlyList<CargaDaFase> Fases,
    bool PelaDemanda,
    string FaseMaisCarregada,
    decimal DesequilibrioPct,
    IReadOnlyList<string> CircuitosSemFase);

/// <summary>Circuito do quadro com as fases que ocupa (rótulos do Revit, ex.: A, B, C).</summary>
public sealed record CircuitoNasFases(string Numero, decimal PotenciaInstaladaVA, decimal? DemandaVA, IReadOnlyList<string>? Fases);

/// <summary>
///     Cargas por fase do quadro: a carga de cada circuito é dividida igualmente entre as fases que ele ocupa (circuito
///     de 2 ou 3 fases com carga equilibrada). Indicador para o projetista distribuir os circuitos — a NBR 5410 não fixa
///     limite de desequilíbrio, e o Ampere não o trata como critério de norma nem o põe na memória de cálculo.
/// </summary>
public static class CargasPorFase
{
    private const decimal Raiz3 = 1.7320508075688772935274463415m;

    /// <summary>Nulo se o quadro não tem fases conhecidas (sem alimentação e sem circuito com fase identificada).</summary>
    /// <param name="fasesDoQuadro">Rótulos das fases do quadro; nulo = os que os circuitos ocupam.</param>
    /// <param name="faseNeutroV">Tensão fase-neutro, para a corrente de cada fase; nula sem neutro.</param>
    public static BalancoDasFases? Calcular(IReadOnlyList<string>? fasesDoQuadro, decimal? faseNeutroV, IReadOnlyList<CircuitoNasFases> circuitos)
    {
        var comFase = circuitos.Where(circuito => circuito.Fases is { Count: > 0 }).ToList();
        var fases = (fasesDoQuadro is { Count: > 0 } ? fasesDoQuadro : comFase.SelectMany(circuito => circuito.Fases!))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        if (fases.Count == 0) return null;

        var semFase = circuitos.Where(circuito => circuito.Fases is not { Count: > 0 } || circuito.Fases.Any(fase => !fases.Contains(fase, StringComparer.Ordinal)))
            .Select(circuito => circuito.Numero)
            .ToList();
        var contados = comFase.Where(circuito => !semFase.Contains(circuito.Numero, StringComparer.Ordinal)).ToList();
        var pelaDemanda = circuitos.Count > 0 && circuitos.All(circuito => circuito.DemandaVA is not null);

        var cargas = fases.Select(fase =>
            {
                var daFase = contados.Where(circuito => circuito.Fases!.Contains(fase, StringComparer.Ordinal)).ToList();
                var instalada = daFase.Sum(circuito => circuito.PotenciaInstaladaVA / circuito.Fases!.Count);
                decimal? demanda = pelaDemanda ? daFase.Sum(circuito => circuito.DemandaVA!.Value / circuito.Fases!.Count) : null;
                decimal? corrente = faseNeutroV is > 0m ? (demanda ?? instalada) / faseNeutroV.Value : null;
                return new CargaDaFase(fase, instalada, demanda, corrente);
            })
            .ToList();

        decimal Base(CargaDaFase carga) => pelaDemanda ? carga.DemandaVA!.Value : carga.PotenciaInstaladaVA;
        var maior = cargas.MaxBy(Base)!;
        var menor = cargas.Min(Base);
        var desequilibrio = Base(maior) > 0m ? (Base(maior) - menor) / Base(maior) * 100m : 0m;
        return new BalancoDasFases(cargas, pelaDemanda, maior.Fase, desequilibrio, semFase);
    }

    /// <summary>Tensão fase-neutro da alimentação do quadro; nula se não há neutro.</summary>
    public static decimal? FaseNeutro(string esquema, decimal tensaoV) => esquema switch
    {
        "F+N" => tensaoV,
        "3F+N" => tensaoV / Raiz3,
        "2F+N" => tensaoV / 2m,
        _ => null
    };
}
