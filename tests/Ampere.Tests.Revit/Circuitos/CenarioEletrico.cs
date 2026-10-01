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
///     Os pontos vão lado a lado a cada 1,5 pé; passando do fim da parede, recomeçam numa fileira 1,5 pé acima.
/// </remarks>
internal sealed class CenarioEletrico
{
    private const double InicioX = -95;
    private const double FimX = 95;
    private const double Passo = 1.5;

    private readonly Reference _face;
    private readonly Level _nivel;
    private double _proximoX = InicioX;
    private int _fileira;

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

    /// <summary>Outro quadro igual ao QD1 (120/208 Wye), com o nome dado, ao lado dele na parede.</summary>
    public FamilyInstance ColocarQuadro(string nome, double x)
    {
        using var transacao = new Transaction(Documento, "Quadro de teste");
        transacao.Start();
        var quadro = Colocar(Documento, _face, _nivel, BuiltInCategory.OST_ElectricalEquipment,
            simbolo => simbolo.FamilyName.Contains("208V") && simbolo.Name == "225 A", new XYZ(x, 0, 4));
        var sistema = new FilteredElementCollector(Documento).OfClass(typeof(DistributionSysType)).First(tipo => tipo.Name == "120/208 Wye");
        quadro.get_Parameter(BuiltInParameter.RBS_FAMILY_CONTENT_DISTRIBUTION_SYSTEM).Set(sistema.Id);
        quadro.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_NAME).Set(nome);
        transacao.Commit();
        return quadro;
    }

    /// <summary>
    ///     Fecha um retângulo em volta da parede do cenário e põe um Room de cada lado, "Cozinha" (y negativo) e "Sala". As
    ///     tomadas vão numa das faces da parede — qual, depende do lado externo dela —, então caem todas no mesmo ambiente.
    /// </summary>
    public (Element Cozinha, Element Sala) CriarDoisAmbientes()
    {
        using var transacao = new Transaction(Documento, "Ambientes do teste");
        transacao.Start();
        XYZ[] cantos = [new(-100, -20, 0), new(100, -20, 0), new(100, 20, 0), new(-100, 20, 0)];
        for (var indice = 0; indice < cantos.Length; indice++)
            Wall.Create(Documento, Line.CreateBound(cantos[indice], cantos[(indice + 1) % cantos.Length]), _nivel.Id, false);
        Documento.Regenerate();

        var cozinha = Documento.Create.NewRoom(_nivel, new UV(0, -10));
        cozinha.get_Parameter(BuiltInParameter.ROOM_NAME).Set("Cozinha");
        var sala = Documento.Create.NewRoom(_nivel, new UV(0, 10));
        sala.get_Parameter(BuiltInParameter.ROOM_NAME).Set("Sala");
        transacao.Commit();
        return (cozinha, sala);
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
            ids.Add(Colocar(Documento, _face, _nivel, categoria, filtro, new XYZ(_proximoX, 0, altura + _fileira * Passo)).Id.Value);
            _proximoX += Passo;
            if (_proximoX <= FimX) continue;

            _proximoX = InicioX;
            _fileira++;
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
