namespace Ampere.Revit.Locais;

/// <summary>
///     Onde procurar o ambiente (Room ou Space) de um ponto de carga, compartilhado pelos "Locais pelos ambientes" e pela
///     "Previsão de cargas".
/// </summary>
/// <remarks>
///     Além do ponto de cálculo da própria família (o <c>Room</c>/<c>Space</c> do elemento), um ponto 10 cm para dentro
///     do ambiente, na normal da face hospedeira (tomada na parede, luminária no forro) ou na frente da família (hospedada
///     em parede sem ser por face), e 10 cm abaixo do ponto (luminária acima do limite do ambiente). Rooms de vínculo são
///     procurados com o ponto levado para as coordenadas do vínculo.
/// </remarks>
internal static class BuscaDeAmbiente
{
    private static readonly double Deslocamento = UnitUtils.ConvertToInternalUnits(0.10, UnitTypeId.Meters);

    /// <summary>Pontos de busca deslocados, na ordem; vazio se a família não tem ponto de inserção.</summary>
    public static XYZ[] Candidatos(FamilyInstance ponto) =>
        ponto.Location is LocationPoint { Point: var origem }
            ?
            [
                origem + ponto.GetTotalTransform().BasisZ * Deslocamento,
                origem + ponto.FacingOrientation * Deslocamento,
                origem - XYZ.BasisZ * Deslocamento
            ]
            : [];

    /// <summary>Origem do ponto (o ponto de inserção), se houver.</summary>
    public static XYZ? Origem(FamilyInstance ponto) => ponto.Location is LocationPoint { Point: var origem } ? origem : null;

    /// <summary>Vínculos carregados: o documento, a transformação para as coordenadas dele e o Id da instância.</summary>
    public static List<VinculoDeAmbientes> Vinculos(Document documento) =>
        new FilteredElementCollector(documento)
            .OfCategory(BuiltInCategory.OST_RvtLinks)
            .OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>()
            .Select(vinculo => new VinculoDeAmbientes(vinculo.GetLinkDocument(), vinculo.GetTotalTransform().Inverse, vinculo.Id.Value))
            .Where(vinculo => vinculo.Documento is not null)
            .ToList();
}

/// <param name="Inversa">Leva um ponto do modelo para as coordenadas do vínculo.</param>
internal sealed record VinculoDeAmbientes(Document Documento, Transform Inversa, long Instancia);
