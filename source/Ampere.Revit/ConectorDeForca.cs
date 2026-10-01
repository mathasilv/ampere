using Autodesk.Revit.DB.Electrical;

namespace Ampere.Revit;

/// <summary>Conector elétrico de força de uma família: é o que faz dela uma carga (ou um quadro) para o Revit.</summary>
internal static class ConectorDeForca
{
    public static Connector? De(FamilyInstance instancia)
    {
        if (instancia.MEPModel?.ConnectorManager?.Connectors is not { } conectores) return null;
        foreach (Connector conector in conectores)
        {
            if (conector.Domain == Domain.DomainElectrical && conector.ElectricalSystemType == ElectricalSystemType.PowerCircuit)
                return conector;
        }

        return null;
    }
}
