using Ampere.Core.Parametros;
using Ampere.Revit.Parametros;

namespace Ampere.Revit;

/// <summary>
///     Pontos de carga do documento: famílias das categorias que recebem AMP_TipoCarga nos pontos (luminárias, dispositivos
///     elétricos, equipamentos mecânicos) com conector elétrico de força, no próprio documento (vínculos ficam de fora). Ficam
///     fora os demolidos e os das opções de projeto que não são a principal: a carga de cada alternativa não se soma.
/// </summary>
internal static class PontosDeCarga
{
    private static readonly BuiltInCategory[] Categorias = CatalogoDeParametros.Padrao.Parametros
        .Single(parametro => parametro.Nome == ParametrosAmpere.Local.Nome).Categorias
        .Select(MapeamentoRevit.Categoria)
        .ToArray();

    public static IEnumerable<FamilyInstance> Instancias(Document documento) =>
        new FilteredElementCollector(documento)
            .WherePasses(new ElementMulticategoryFilter(Categorias))
            .WhereElementIsNotElementType()
            .OfType<FamilyInstance>()
            .Where(instancia => instancia.DemolishedPhaseId == ElementId.InvalidElementId)
            .Where(instancia => instancia.DesignOption is null || instancia.DesignOption.IsPrimary)
            .Where(instancia => ConectorDeForca.De(instancia) is not null);
}
