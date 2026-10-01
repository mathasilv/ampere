using Ampere.Core.Dimensionamento;
using Ampere.Core.Parametros;
using Autodesk.Revit.DB.Electrical;

namespace Ampere.Revit.Dimensionamento;

/// <summary>
///     Porta <see cref="IDocumentoDeDimensionamento" /> sobre a API do Revit.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Circuito = <c>ElectricalSystem</c> de força com AMP_NumeroCircuito (criado pelo Ampere); pontos = os membros
///         do sistema, com AMP_PotenciaInstaladaVA, AMP_TensaoCircuitoV, AMP_Fases, AMP_Local e AMP_TipoCarga.</item>
///         <item>Comprimento: AMP_ComprimentoRotaM (LENGTH, unidades internas em pés) quando o projetista o preencheu, e
///         sempre o "Comprimento" nativo do circuito (<c>RBS_ELEC_CIRCUIT_LENGTH_PARAM</c>, o mesmo de
///         <c>ElectricalSystem.Length</c> sem a exceção de comprimento zero) com o modo do caminho — o Core escolhe.</item>
///         <item>Gravação: só os parâmetros de resultado, nunca as entradas (método, isolação, tipo de condutor e
///         comprimento): o padrão do projeto gravado no circuito viraria "decisão do projetista" na rodada seguinte.
///         Valor não calculado apaga o anterior (<see cref="ParametrosAmpere.GravarNumeroOuApagar" />).</item>
///         <item>Proteção (seção, IZ, disjuntor, queda, IDR e eletroduto) só é gravada quando a exigência de IDR foi
///         avaliada: sem isso, IDR vazio ao lado de um disjuntor seria lido como "sem IDR". Fica a corrente de projeto, os
///         fatores e o hash da memória, que mostra onde o cálculo parou.</item>
///     </list>
/// </remarks>
public sealed class DocumentoDeDimensionamentoRevit(Document documento) : IDocumentoDeDimensionamento
{
    public void EmUmaTransacao(string nome, Action acao) => TransacaoRevit.Executar(documento, nome, acao);

    /// <summary>Todos os parâmetros do catálogo já estão no documento (a injeção é tudo ou nada)?</summary>
    public bool ParametrosInjetados() =>
        CatalogoDeParametros.Padrao.Parametros.All(definicao => ParametrosAmpere.Injetado(documento, definicao));

    /// <summary>Circuitos de força criados pelo Ampere (com AMP_NumeroCircuito), por quadro e número.</summary>
    public IReadOnlyList<long> ListarCircuitos() =>
        new FilteredElementCollector(documento)
            .OfCategory(BuiltInCategory.OST_ElectricalCircuit)
            .WhereElementIsNotElementType()
            .OfType<ElectricalSystem>()
            .Where(sistema => sistema.SystemType == ElectricalSystemType.PowerCircuit)
            .Select(sistema => (Sistema: sistema, Numero: ParametrosAmpere.LerTexto(sistema, ParametrosAmpere.NumeroCircuito)))
            .Where(par => !string.IsNullOrWhiteSpace(par.Numero))
            .OrderBy(par => ParametrosAmpere.LerTexto(par.Sistema, ParametrosAmpere.Quadro) ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(par => par.Numero, StringComparer.Ordinal)
            .Select(par => par.Sistema.Id.Value)
            .ToList();

    public IReadOnlyList<DadosDoCircuito> LerCircuitos(IReadOnlyCollection<long> ids) =>
        ids.Select(id =>
            {
                var sistema = Sistema(id);
                return new DadosDoCircuito(
                    id,
                    Texto(sistema, ParametrosAmpere.NumeroCircuito),
                    Texto(sistema, ParametrosAmpere.TipoCarga),
                    ComprimentoInformadoM(sistema),
                    Texto(sistema, ParametrosAmpere.MetodoInstalacao),
                    Texto(sistema, ParametrosAmpere.MaterialIsolacao),
                    sistema.Elements.Cast<Element>().Select(LerPonto).ToList(),
                    TipoDeCondutor: Texto(sistema, ParametrosAmpere.TipoCondutor),
                    ComprimentoNoRevit: ComprimentoNoRevit(sistema),
                    Quadro: Texto(sistema, ParametrosAmpere.Quadro));
            })
            .ToList();

    public void GravarResultados(IReadOnlyList<ResultadoDoCircuito> resultados)
    {
        foreach (var resultado in resultados)
        {
            var sistema = Sistema(resultado.Id);
            var calculo = resultado.Memoria is null ? null : resultado.Dimensionamento;
            var protecao = calculo is { IdrAvaliado: true } ? calculo : null;

            Corrente(sistema, ParametrosAmpere.CorrenteProjetoA, calculo?.CorrenteDeProjetoA);
            Numero(sistema, ParametrosAmpere.FCA, calculo?.FCA);
            Numero(sistema, ParametrosAmpere.FCT, calculo?.FCT);
            Numero(sistema, ParametrosAmpere.BitolaCondutorMm2, protecao?.SecaoMm2);
            Corrente(sistema, ParametrosAmpere.CapacidadeConducaoA, protecao?.CapacidadeDeConducaoA);
            Numero(sistema, ParametrosAmpere.DisjuntorNominalA, protecao?.DisjuntorA);
            Numero(sistema, ParametrosAmpere.IdrNominalA, protecao?.IdrNominalA);
            Numero(sistema, ParametrosAmpere.IdrSensibilidadeMa, protecao?.IdrSensibilidadeMa);
            Numero(sistema, ParametrosAmpere.QuedaTensaoPct, protecao?.QuedaDeTensaoPct);
            ParametrosAmpere.GravarTextoOuApagar(sistema, ParametrosAmpere.EletrodutoTipo, protecao?.Eletroduto);
            Numero(sistema, ParametrosAmpere.OcupacaoEletrodutoPct, protecao?.OcupacaoDoEletrodutoPct);
            ParametrosAmpere.GravarTextoOuApagar(sistema, ParametrosAmpere.PerfilNorma, calculo?.PerfilNorma);
            ParametrosAmpere.GravarTextoOuApagar(sistema, ParametrosAmpere.MemoriaCalculoId, resultado.Memoria?.Hash());
        }
    }

    private ElectricalSystem Sistema(long id) =>
        documento.GetElement(new ElementId(id)) as ElectricalSystem
        ?? throw new InvalidOperationException($"O elemento {id} não é um circuito elétrico do documento.");

    private static DadosDoPonto LerPonto(Element ponto) =>
        new(ponto.Id.Value,
            ParametrosAmpere.Ler(ponto, ParametrosAmpere.PotenciaInstaladaVA) is { HasValue: true } potencia
                ? (decimal)UnitUtils.ConvertFromInternalUnits(potencia.AsDouble(), UnitTypeId.VoltAmperes)
                : null,
            ParametrosAmpere.Ler(ponto, ParametrosAmpere.TensaoCircuitoV) is { HasValue: true } tensao ? (decimal)tensao.AsDouble() : null,
            Texto(ponto, ParametrosAmpere.Fases),
            Texto(ponto, ParametrosAmpere.Local),
            Texto(ponto, ParametrosAmpere.TipoCarga));

    private static decimal? ComprimentoInformadoM(ElectricalSystem sistema) =>
        ParametrosAmpere.Ler(sistema, ParametrosAmpere.ComprimentoRotaM) is { HasValue: true } comprimento
            ? (decimal)UnitUtils.ConvertFromInternalUnits(comprimento.AsDouble(), UnitTypeId.Meters)
            : null;

    private static ComprimentoDoRevit? ComprimentoNoRevit(ElectricalSystem sistema)
    {
        if (sistema.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_LENGTH_PARAM) is not { HasValue: true } comprimento) return null;

        var caminho = sistema.CircuitPathMode switch
        {
            ElectricalCircuitPathMode.FarthestDevice => "caminho do quadro ao ponto mais distante",
            ElectricalCircuitPathMode.AllDevices => "caminho do quadro passando por todos os pontos",
            ElectricalCircuitPathMode.Custom => "caminho editado no Revit",
            var outro => $"caminho '{outro}'"
        };
        return new ComprimentoDoRevit((decimal)UnitUtils.ConvertFromInternalUnits(comprimento.AsDouble(), UnitTypeId.Meters), caminho);
    }

    // Texto vazio é tratado como parâmetro vazio, como nos demais adapters.
    private static string? Texto(Element elemento, DefinicaoDeParametro definicao) =>
        ParametrosAmpere.LerTexto(elemento, definicao) is { Length: > 0 } texto ? texto : null;

    private static void Numero(Element elemento, DefinicaoDeParametro definicao, decimal? valor) =>
        ParametrosAmpere.GravarNumeroOuApagar(elemento, definicao, (double?)valor);

    private static void Corrente(Element elemento, DefinicaoDeParametro definicao, decimal? amperes) =>
        ParametrosAmpere.GravarNumeroOuApagar(elemento, definicao,
            amperes is { } valor ? UnitUtils.ConvertToInternalUnits((double)valor, UnitTypeId.Amperes) : null);
}
