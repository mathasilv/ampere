using Ampere.Core.Quadros;
using Autodesk.Revit.DB.Electrical;

namespace Ampere.Revit.Quadros;

/// <summary>
///     Leitura do painel compartilhada pelo quadro de cargas e pelo diagrama unifilar: rótulos das fases, fases de cada
///     circuito no quadro e a alimentação pelo sistema de distribuição.
/// </summary>
internal static class LeituraDoPainel
{
    // Rótulos das fases nas configurações elétricas do projeto (padrão A, B, C; no Brasil, às vezes R, S, T).
    public static string[] RotulosDasFases(Document documento)
    {
        var configuracao = ElectricalSetting.GetElectricalSettings(documento);
        return new[] { configuracao.CircuitNamePhaseA, configuracao.CircuitNamePhaseB, configuracao.CircuitNamePhaseC }
            .Select(rotulo => rotulo?.Trim() ?? string.Empty)
            .Where(rotulo => rotulo.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    // Fases do circuito pelo rótulo que o Revit dá a ele no quadro (ex.: "A", "A,B" ou "AB"), conferido com o número de
    // polos: rótulo que não bate fica sem fase (o Core lista o circuito como não identificado), nunca adivinhado.
    public static IReadOnlyList<string>? FasesNoQuadro(ElectricalSystem sistema, string[] rotulos)
    {
        string? rotulo;
        int polos;
        try
        {
            rotulo = sistema.PhaseLabel;
            polos = sistema.PolesNumber;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(rotulo) || rotulos.Length < 3) return null;
        var fases = RotuloDeFases.Ler(rotulo, rotulos);
        return fases?.Count == polos ? fases : null;
    }

    // Sistema de distribuição atribuído ao quadro: fases, fios e tensões. Tensão pelo parâmetro do VoltageType, em unidades
    // internas convertidas (como as demais leituras do adapter). Sem sistema, o Core usa o esquema comum dos circuitos.
    // Transformador: os circuitos saem do secundário. 3 fases e 4 fios (Y, ou delta com neutro): 3F+N; a tensão
    // fase-neutro é a fase-terra do Revit (no delta com neutro, a das duas fases do meio do enrolamento — a "perna alta"
    // não é tratada à parte). Monofásico de 3 fios: 2F+N, também com a fase-terra do Revit.
    public static AlimentacaoDoQuadro? Alimentacao(Document documento, FamilyInstance painel, string[] rotulos)
    {
        var sistema = Sistema(documento, painel, BuiltInParameter.RBS_FAMILY_CONTENT_SECONDARY_DISTRIBSYS)
                      ?? Sistema(documento, painel, BuiltInParameter.RBS_FAMILY_CONTENT_DISTRIBUTION_SYSTEM);
        if (sistema is null) return null;

        var linha = Volts(sistema.VoltageLineToLine);
        var terra = Volts(sistema.VoltageLineToGround);
        var (esquema, tensao) = sistema.ElectricalPhase switch
        {
            ElectricalPhase.ThreePhase => (sistema.NumWires >= 4 ? "3F+N" : "3F", linha),
            ElectricalPhase.SinglePhase => sistema.NumWires >= 3 ? ("2F+N", linha) : ("F+N", terra ?? linha),
            _ => ((string?)null, (decimal?)null)
        };
        if (esquema is null || tensao is not > 0m) return null;

        var quantas = esquema.StartsWith("3F", StringComparison.Ordinal) ? 3 : esquema.StartsWith("2F", StringComparison.Ordinal) ? 2 : 1;
        IReadOnlyList<string>? fases = rotulos.Length >= quantas ? rotulos.Take(quantas).ToList() : null;
        var faseNeutro = esquema.EndsWith("+N", StringComparison.Ordinal) && esquema != "F+N" ? terra : null;
        return new AlimentacaoDoQuadro(esquema, tensao.Value, $"sistema de distribuição '{sistema.Name}' do quadro", fases, faseNeutro);
    }

    private static DistributionSysType? Sistema(Document documento, FamilyInstance painel, BuiltInParameter parametro) =>
        painel.get_Parameter(parametro)?.AsElementId() is { } id && documento.GetElement(id) is DistributionSysType sistema ? sistema : null;

    private static decimal? Volts(VoltageType? tensao) =>
        tensao?.get_Parameter(BuiltInParameter.RBS_VOLTAGETYPE_VOLTAGE_PARAM) is { HasValue: true } parametro
            ? Math.Round((decimal)UnitUtils.ConvertFromInternalUnits(parametro.AsDouble(), UnitTypeId.Volts), 3, MidpointRounding.AwayFromZero)
            : null;

    /// <summary>
    ///     Circuitos de força que o quadro alimenta, sem reserva nem espaço do Revit (<c>CircuitType</c> Spare e Space: sem
    ///     cargas, ficam fora do quadro de cargas).
    /// </summary>
    public static List<ElectricalSystem> CircuitosDoQuadro(FamilyInstance painel) =>
        painel.MEPModel?.GetAssignedElectricalSystems()?
            .Where(sistema => sistema.SystemType == ElectricalSystemType.PowerCircuit && sistema.CircuitType == CircuitType.Circuit)
            .ToList() ?? [];

    /// <summary>
    ///     Circuitos de força de que o equipamento é carga, em ordem de id. O <c>GetElectricalSystems</c> de um quadro também
    ///     devolve os circuitos que ele alimenta: ficam só os que saem de outro equipamento (circuito sem quadro de origem
    ///     não é alimentador) e o têm entre os membros.
    /// </summary>
    public static List<ElectricalSystem> Alimentadores(FamilyInstance equipamento) =>
        equipamento.MEPModel?.GetElectricalSystems()?
            .Where(sistema => sistema.SystemType == ElectricalSystemType.PowerCircuit
                              && sistema.BaseEquipment is { } origem && origem.Id != equipamento.Id
                              && sistema.Elements.Cast<Element>().Any(membro => membro.Id == equipamento.Id))
            .OrderBy(sistema => sistema.Id.Value)
            .ToList() ?? [];

    /// <summary>
    ///     Circuito que alimenta equipamento de distribuição (quadro ou transformador): é alimentador, não circuito terminal.
    ///     Seccionadora e outro equipamento elétrico que não distribui continuam como carga do circuito terminal.
    /// </summary>
    public static bool AlimentaEquipamento(ElectricalSystem sistema) => sistema.Elements.Cast<Element>().Any(Distribui);

    // Quadro, painel, QGBT ou transformador pelo tipo da família no Revit; sem ele, quem já tem circuitos de força.
    private static bool Distribui(Element membro)
    {
        if (membro is not FamilyInstance { Category.BuiltInCategory: BuiltInCategory.OST_ElectricalEquipment } instancia) return false;
        if (instancia.Symbol?.Family?.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE) is { HasValue: true } tipo)
        {
            return (PartType)tipo.AsInteger() is PartType.PanelBoard or PartType.SwitchBoard or PartType.OtherPanel or PartType.Transformer
                   || CircuitosDoQuadro(instancia).Count > 0;
        }

        return CircuitosDoQuadro(instancia).Count > 0;
    }

    /// <summary>Todos os circuitos de força do quadro, com as reservas e os espaços do Revit (o unifilar os desenha).</summary>
    public static List<ElectricalSystem> CircuitosDeForca(FamilyInstance painel) =>
        painel.MEPModel?.GetAssignedElectricalSystems()?
            .Where(sistema => sistema.SystemType == ElectricalSystemType.PowerCircuit)
            .ToList() ?? [];

    /// <summary>Equipamento com sistema de distribuição secundário: transformador (os circuitos saem do secundário).</summary>
    public static bool Transformador(Document documento, FamilyInstance equipamento) =>
        Sistema(documento, equipamento, BuiltInParameter.RBS_FAMILY_CONTENT_SECONDARY_DISTRIBSYS) is not null;

    /// <summary>Nome do quadro: o "Nome do painel" do Revit ou, vazio, o nome do elemento.</summary>
    public static string Nome(FamilyInstance painel) =>
        painel.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_NAME)?.AsString() is { Length: > 0 } nome ? nome : painel.Name;
}
