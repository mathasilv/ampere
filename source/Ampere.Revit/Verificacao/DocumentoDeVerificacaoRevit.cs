using Ampere.Core.Dimensionamento;
using Ampere.Core.Parametros;
using Ampere.Core.Verificacao;
using Ampere.Revit.Dimensionamento;
using Ampere.Revit.Parametros;
using Ampere.Revit.Quadros;
using Autodesk.Revit.DB.Electrical;

namespace Ampere.Revit.Verificacao;

/// <summary>
///     Porta <see cref="IDocumentoDeVerificacao" /> sobre a API do Revit: só leitura.
/// </summary>
/// <remarks>
///     Ponto = família das categorias que recebem AMP_TipoCarga (luminárias, dispositivos elétricos, equipamentos
///     mecânicos) com conector elétrico de força, no próprio documento (vínculos ficam de fora). Os dados dos circuitos do
///     Ampere e as condições guardadas vêm do <see cref="DocumentoDeDimensionamentoRevit" />, para a verificação refazer o
///     cálculo exatamente como o "Dimensionar". Circuitos: os terminais; reservas e espaços do Revit e alimentadores
///     (circuito com quadro ou transformador entre os membros, conferido pelo "Dimensionar alimentadores") ficam de fora.
/// </remarks>
public sealed class DocumentoDeVerificacaoRevit(Document documento) : IDocumentoDeVerificacao
{
    private readonly DocumentoDeDimensionamentoRevit _dimensionamento = new(documento);

    /// <summary>Todos os parâmetros do catálogo já estão no documento?</summary>
    public bool ParametrosInjetados() => _dimensionamento.ParametrosInjetados();

    public IReadOnlyList<PontoVerificado> LerPontos() =>
        PontosDeCarga.Instancias(documento)
            .Select(instancia => new PontoVerificado(
                instancia.Id.Value,
                Texto(instancia, ParametrosAmpere.TipoCarga),
                Texto(instancia, ParametrosAmpere.Local),
                CircuitoDeForca(instancia),
                Texto(instancia, ParametrosAmpere.Aparelho)))
            .OrderBy(ponto => ponto.Id)
            .ToList();

    public IReadOnlyList<CircuitoVerificado> LerCircuitosDeForca() =>
        new FilteredElementCollector(documento)
            .OfCategory(BuiltInCategory.OST_ElectricalCircuit)
            .WhereElementIsNotElementType()
            .OfType<ElectricalSystem>()
            .Where(sistema => sistema.SystemType == ElectricalSystemType.PowerCircuit && sistema.CircuitType == CircuitType.Circuit
                              && !LeituraDoPainel.AlimentaEquipamento(sistema))
            .Select(sistema => new CircuitoVerificado(
                sistema.Id.Value,
                Texto(sistema, ParametrosAmpere.NumeroCircuito),
                Texto(sistema, ParametrosAmpere.Quadro),
                Nome(sistema),
                Texto(sistema, ParametrosAmpere.MemoriaCalculoId)))
            .OrderBy(circuito => circuito.Quadro ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(circuito => circuito.Numero ?? circuito.Nome, StringComparer.Ordinal)
            .ToList();

    public IReadOnlyList<DadosDoCircuito> LerCircuitos(IReadOnlyCollection<long> ids) => _dimensionamento.LerCircuitos(ids);

    public CondicoesDoProjeto? LerCondicoes() => _dimensionamento.LerCondicoes();

    public IReadOnlyDictionary<long, CondicoesDoProjeto> LerCondicoesDosCircuitos(IReadOnlyCollection<long> ids) => _dimensionamento.LerCondicoesDosCircuitos(ids);

    public IReadOnlyDictionary<long, ResultadosNoCircuito> LerResultados(IReadOnlyCollection<long> ids) => _dimensionamento.LerResultados(ids);

    private static long? CircuitoDeForca(FamilyInstance instancia) =>
        instancia.MEPModel?.GetElectricalSystems()?
            .Where(sistema => sistema.SystemType == ElectricalSystemType.PowerCircuit)
            .Select(sistema => (long?)sistema.Id.Value)
            .Min();

    // Como o Revit mostra o circuito fora do Ampere: quadro e número de circuito.
    private static string Nome(ElectricalSystem sistema)
    {
        var nome = $"{sistema.PanelName} {sistema.CircuitNumber}".Trim();
        return nome.Length > 0 ? $"circuito {nome} do Revit" : $"circuito {sistema.Id.Value} do Revit";
    }

    private static string? Texto(Element elemento, DefinicaoDeParametro definicao) =>
        ParametrosAmpere.LerTexto(elemento, definicao) is { Length: > 0 } texto ? texto : null;
}
