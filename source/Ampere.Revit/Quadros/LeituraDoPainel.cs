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

    /// <summary>Nome do quadro: o "Nome do painel" do Revit ou, vazio, o nome do elemento.</summary>
    public static string Nome(FamilyInstance painel) =>
        painel.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_NAME)?.AsString() is { Length: > 0 } nome ? nome : painel.Name;
}
