using Ampere.Core.Alimentadores;
using Ampere.Core.Dimensionamento;
using Ampere.Revit.Dimensionamento;
using Ampere.Revit.Parametros;
using Ampere.Revit.Quadros;
using Autodesk.Revit.DB.Electrical;

namespace Ampere.Revit.Alimentadores;

/// <summary>
///     Porta <see cref="IDocumentoDeAlimentadores" /> sobre a API do Revit.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Alimentador = o circuito de força de que o quadro é carga (<see cref="LeituraDoPainel.Alimentadores" />: o
///         <c>GetElectricalSystems</c> do quadro também devolve os circuitos que ele alimenta); origem = o equipamento de
///         onde ele sai (<c>BaseEquipment</c>). A origem também alimentada por um circuito = cascata.</item>
///         <item>Quadro que alimenta outros = algum circuito dele tem equipamento elétrico entre os membros.</item>
///         <item>Impedimentos: transformador (o primário está noutra tensão), mais de um circuito alimentador e alimentador
///         com outras cargas além do quadro (a demanda seria só a do quadro).</item>
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
            .Select(painel => (Painel: painel, Circuitos: LeituraDoPainel.CircuitosDoQuadro(painel)))
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

    public IReadOnlyDictionary<long, ResultadosNoCircuito> LerResultados(IReadOnlyCollection<long> ids) => _circuitos.LerResultados(ids);

    private QuadroComAlimentador Ler(FamilyInstance painel, List<ElectricalSystem> circuitos)
    {
        var alimentadores = LeituraDoPainel.Alimentadores(painel);
        var alimentador = alimentadores.FirstOrDefault();
        var origem = alimentador?.BaseEquipment;
        var origemAlimentada = origem is not null && LeituraDoPainel.Alimentadores(origem).Count > 0;
        var alimentaQuadros = circuitos.Any(LeituraDoPainel.AlimentaEquipamento);
        return new QuadroComAlimentador(
            painel.Id.Value,
            LeituraDoPainel.Nome(painel),
            alimentador is null ? null : _circuitos.LerAlimentador(alimentador.Id.Value),
            origem is null ? null : LeituraDoPainel.Nome(origem),
            origemAlimentada,
            alimentaQuadros,
            Impedimento(painel, alimentadores));
    }

    private string? Impedimento(FamilyInstance painel, List<ElectricalSystem> alimentadores)
    {
        if (LeituraDoPainel.Transformador(documento, painel))
            return "transformador: o circuito do primário não é dimensionado pelo Ampere (a demanda do secundário está noutra tensão)";
        if (alimentadores.Count > 1)
            return $"quadro alimentado por mais de um circuito ({string.Join(", ", alimentadores.Select(Numero))}): o Ampere dimensiona um alimentador por quadro";
        if (alimentadores.FirstOrDefault() is not { } alimentador) return null;

        var outros = alimentador.Elements.Cast<Element>().Where(membro => membro.Id != painel.Id).ToList();
        return outros.Count > 0
            ? $"o circuito alimentador {Numero(alimentador)} também alimenta outras cargas ({outros.Count}): a soma das demandas ainda não é feita pelo Ampere"
            : null;
    }

    private static string Numero(ElectricalSystem sistema) =>
        ParametrosAmpere.LerTexto(sistema, ParametrosAmpere.NumeroCircuito) is { Length: > 0 } numero ? numero : $"{sistema.PanelName} {sistema.CircuitNumber}".Trim();
}
