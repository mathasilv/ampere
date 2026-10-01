using Ampere.Core.Alimentadores;
using Ampere.Core.Dimensionamento;
using Ampere.Revit.Dimensionamento;
using Autodesk.Revit.DB.Electrical;

namespace Ampere.Revit.Alimentadores;

/// <summary>
///     Porta <see cref="IDocumentoDeAlimentadores" /> sobre a API do Revit.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Alimentador = o circuito de força de que o quadro é carga (<c>MEPModel.GetElectricalSystems</c>); origem =
///         o equipamento de onde ele sai (<c>BaseEquipment</c>). A origem também alimentada por um circuito = cascata.</item>
///         <item>Quadro que alimenta outros = algum circuito dele tem equipamento elétrico entre os membros.</item>
///         <item>Leitura e gravação dos AMP_* pelo <see cref="DocumentoDeDimensionamentoRevit" />: o alimentador é um circuito
///         como os terminais (mesmos parâmetros de entrada e de resultado, mesma limpeza do que deixou de valer).</item>
///     </list>
/// </remarks>
public sealed class DocumentoDeAlimentadoresRevit(Document documento) : IDocumentoDeAlimentadores
{
    private readonly DocumentoDeDimensionamentoRevit _circuitos = new(documento);

    /// <summary>Todos os parâmetros do catálogo já estão no documento?</summary>
    public bool ParametrosInjetados() => _circuitos.ParametrosInjetados();

    public void EmUmaTransacao(string nome, Action acao) => _circuitos.EmUmaTransacao(nome, acao);

    public IReadOnlyList<QuadroComAlimentador> LerAlimentadores() =>
        new FilteredElementCollector(documento)
            .OfCategory(BuiltInCategory.OST_ElectricalEquipment)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Select(painel => (Painel: painel, Circuitos: DeForca(painel.MEPModel?.GetAssignedElectricalSystems())))
            .Where(par => par.Circuitos.Count > 0)
            .Select(par => Ler(par.Painel, par.Circuitos))
            .OrderBy(quadro => quadro.Quadro, StringComparer.Ordinal)
            .ToList();

    public CondicoesDoProjeto? LerCondicoes() => _circuitos.LerCondicoes();

    public IReadOnlyDictionary<long, CondicoesDoProjeto> LerCondicoesDosCircuitos(IReadOnlyCollection<long> ids) => _circuitos.LerCondicoesDosCircuitos(ids);

    public IReadOnlyList<DadosDoCircuito> LerCircuitos(IReadOnlyCollection<long> ids) => _circuitos.LerCircuitos(ids);

    public void GravarResultados(IReadOnlyList<ResultadoDoCircuito> resultados) => _circuitos.GravarResultados(resultados);

    public void GravarCondicoes(CondicoesDoProjeto condicoes, IReadOnlyCollection<long> circuitos, bool doProjetoTodo) =>
        _circuitos.GravarCondicoes(condicoes, circuitos, doProjetoTodo);

    private QuadroComAlimentador Ler(FamilyInstance painel, List<ElectricalSystem> circuitos)
    {
        var alimentador = DeForca(painel.MEPModel?.GetElectricalSystems()).MinBy(sistema => sistema.Id.Value);
        var origem = alimentador?.BaseEquipment;
        var origemAlimentada = origem is not null && DeForca(origem.MEPModel?.GetElectricalSystems()).Count > 0;
        var alimentaQuadros = circuitos.Any(circuito => circuito.Elements.Cast<Element>()
            .Any(membro => membro.Category?.BuiltInCategory == BuiltInCategory.OST_ElectricalEquipment));
        return new QuadroComAlimentador(
            painel.Id.Value,
            Nome(painel),
            alimentador is null ? null : _circuitos.LerAlimentador(alimentador.Id.Value),
            origem is null ? null : Nome(origem),
            origemAlimentada,
            alimentaQuadros);
    }

    private static List<ElectricalSystem> DeForca(ISet<ElectricalSystem>? sistemas) =>
        sistemas?.Where(sistema => sistema.SystemType == ElectricalSystemType.PowerCircuit).ToList() ?? [];

    private static string Nome(FamilyInstance painel) =>
        painel.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_NAME)?.AsString() is { Length: > 0 } nome ? nome : painel.Name;
}
