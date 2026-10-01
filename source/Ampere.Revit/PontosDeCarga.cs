using Ampere.Core.Parametros;
using Ampere.Revit.Parametros;

namespace Ampere.Revit;

/// <summary>
///     Pontos de carga do documento: famílias das categorias que recebem AMP_TipoCarga nos pontos (luminárias, dispositivos
///     elétricos, equipamentos mecânicos) com conector elétrico de força, no próprio documento (vínculos ficam de fora).
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
            .Where(instancia => ConectorDeForca.De(instancia) is not null);
}
