using System.Reflection;
using Ampere.Core.Parametros;
using Ampere.Revit.Parametros;
using Autodesk.Revit.DB.Electrical;

namespace Ampere.Tests.Revit.Circuitos;

/// <summary>
///     Projeto elétrico de teste a partir do template PTB da Autodesk: parâmetros Ampere injetados, uma parede e o
///     quadro "QD1" (120/208 V, estrela) na face dela. Luminárias e tomadas da própria Autodesk entram sob demanda.
/// </summary>
/// <remarks>
///     As famílias do template são hospedadas em face (verificado em spike): tudo vai na face externa da parede.
///     O quadro vem sem sistema de distribuição e o Revit recusa circuitos nele até que seja definido.
/// </remarks>
internal sealed class CenarioEletrico
{
    private readonly Reference _face;
    private readonly Level _nivel;
    private double _proximoX = -95;

    private CenarioEletrico(Document documento, Wall parede, Reference face, Level nivel, FamilyInstance quadro)
    {
        Documento = documento;
        Parede = parede;
        _face = face;
        _nivel = nivel;
        Quadro = quadro;
    }

    public Document Documento { get; }

    public Wall Parede { get; }

    public FamilyInstance Quadro { get; }

    public static string CaminhoDoTemplate =>
        typeof(CenarioEletrico).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(atributo => atributo.Key == "RevitTemplateEletrico").Value
        ?? throw new InvalidOperationException("RevitTemplateEletrico não definido no projeto de testes.");

    public static CenarioEletrico Montar(Document documento)
    {
        var injecao = InjecaoDeParametros.Executar(CatalogoDeParametros.Padrao, new ParametrosDoDocumentoRevit(documento));
        if (injecao.TemConflitos) throw new InvalidOperationException("O template tem conflito com os parâmetros Ampere.");

        using var transacao = new Transaction(documento, "Cenário de teste");
        transacao.Start();
        var nivel = new FilteredElementCollector(documento).OfClass(typeof(Level)).Cast<Level>().OrderBy(nivel => nivel.Elevation).First();
        var parede = Wall.Create(documento, Line.CreateBound(new XYZ(-100, 0, 0), new XYZ(100, 0, 0)), nivel.Id, false);
        documento.Regenerate();
        var face = HostObjectUtils.GetSideFaces(parede, ShellLayerType.Exterior).First();

        var quadro = Colocar(documento, face, nivel, BuiltInCategory.OST_ElectricalEquipment,
            simbolo => simbolo.FamilyName.Contains("208V") && simbolo.Name == "225 A", new XYZ(0, 0, 4));
        var sistema = new FilteredElementCollector(documento).OfClass(typeof(DistributionSysType)).First(tipo => tipo.Name == "120/208 Wye");
        quadro.get_Parameter(BuiltInParameter.RBS_FAMILY_CONTENT_DISTRIBUTION_SYSTEM).Set(sistema.Id);
        quadro.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_NAME).Set("QD1");
        transacao.Commit();

        return new CenarioEletrico(documento, parede, face, nivel, quadro);
    }

    public List<long> ColocarLuminarias(int quantidade) =>
        ColocarVarias(quantidade, BuiltInCategory.OST_LightingFixtures, simbolo => simbolo.Name == "600x600 - 120", altura: 8);

    public List<long> ColocarTomadas(int quantidade) =>
        ColocarVarias(quantidade, BuiltInCategory.OST_ElectricalFixtures, simbolo => simbolo.Name == "Padrão", altura: 1);

    private List<long> ColocarVarias(int quantidade, BuiltInCategory categoria, Func<FamilySymbol, bool> filtro, double altura)
    {
        using var transacao = new Transaction(Documento, "Pontos de teste");
        transacao.Start();
        var ids = new List<long>();
        for (var indice = 0; indice < quantidade; indice++)
        {
            ids.Add(Colocar(Documento, _face, _nivel, categoria, filtro, new XYZ(_proximoX, 0, altura)).Id.Value);
            _proximoX += 1.5;
        }

        transacao.Commit();
        return ids;
    }

    private static FamilyInstance Colocar(Document documento, Reference face, Level nivel, BuiltInCategory categoria, Func<FamilySymbol, bool> filtro, XYZ ponto)
    {
        var simbolo = new FilteredElementCollector(documento).OfCategory(categoria).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().First(filtro);
        if (!simbolo.IsActive) simbolo.Activate();
        return simbolo.Family.FamilyPlacementType == FamilyPlacementType.WorkPlaneBased
            ? documento.Create.NewFamilyInstance(face, ponto, XYZ.BasisZ, simbolo)
            : documento.Create.NewFamilyInstance(ponto, simbolo, nivel, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
    }
}
