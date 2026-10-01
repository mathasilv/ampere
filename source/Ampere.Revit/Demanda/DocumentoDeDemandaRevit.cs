using Ampere.Core.Demanda;
using Ampere.Core.Parametros;
using Ampere.Revit.Parametros;

namespace Ampere.Revit.Demanda;

/// <summary>
///     Porta <see cref="IDocumentoDeDemanda" /> sobre a API do Revit: só leitura. Os pontos são os de
///     <see cref="PontosDeCarga" />, com AMP_TipoCarga, AMP_Aparelho e AMP_PotenciaInstaladaVA (em unidades internas,
///     convertidas para VA).
/// </summary>
public sealed class DocumentoDeDemandaRevit(Document documento) : IDocumentoDeDemanda
{
    public IReadOnlyList<PontoDeDemanda> LerPontos() =>
        PontosDeCarga.Instancias(documento)
            .Select(instancia => new PontoDeDemanda(
                instancia.Id.Value,
                Texto(instancia, ParametrosAmpere.TipoCarga),
                Texto(instancia, ParametrosAmpere.Aparelho),
                ParametrosAmpere.Ler(instancia, ParametrosAmpere.PotenciaInstaladaVA) is { HasValue: true } potencia
                    ? Math.Round((decimal)UnitUtils.ConvertFromInternalUnits(potencia.AsDouble(), UnitTypeId.VoltAmperes), 6, MidpointRounding.AwayFromZero)
                    : null))
            .OrderBy(ponto => ponto.Id)
            .ToList();

    private static string? Texto(Element elemento, DefinicaoDeParametro definicao) =>
        ParametrosAmpere.LerTexto(elemento, definicao) is { Length: > 0 } texto ? texto : null;
}
