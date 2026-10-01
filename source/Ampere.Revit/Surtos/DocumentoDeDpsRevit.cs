using Ampere.Core.Surtos;
using Ampere.Revit.Quadros;

namespace Ampere.Revit.Surtos;

/// <summary>
///     Porta <see cref="IDocumentoDeDps" /> sobre a API do Revit: os quadros com as tensões do sistema de distribuição. Só
///     leitura; a seleção dos DPS não grava nada no modelo.
/// </summary>
/// <remarks>
///     Entra o equipamento elétrico com sistema de distribuição ou com circuitos de força (quadro sem sistema aparece para o
///     Core dizer o que falta); transformador entra pelo secundário, como no quadro de cargas.
/// </remarks>
public sealed class DocumentoDeDpsRevit(Document documento) : IDocumentoDeDps
{
    public IReadOnlyList<QuadroParaDps> LerQuadros()
    {
        var rotulos = LeituraDoPainel.RotulosDasFases(documento);
        return new FilteredElementCollector(documento)
            .OfCategory(BuiltInCategory.OST_ElectricalEquipment)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Select(painel => (Painel: painel, Quadro: LeituraDoPainel.ParaDps(documento, painel, rotulos)))
            .Where(par => par.Quadro.Esquema is not null || LeituraDoPainel.CircuitosDeForca(par.Painel).Count > 0)
            .Select(par => par.Quadro)
            .OrderBy(quadro => quadro.Nome, StringComparer.CurrentCulture)
            .ToList();
    }
}
