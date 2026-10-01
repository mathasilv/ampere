using Ampere.Core.Cargas;
using Ampere.Core.Parametros;
using Ampere.Core.Previsao;
using Ampere.Revit.Armazenamento;
using Ampere.Revit.Locais;
using Ampere.Revit.Parametros;
using Autodesk.Revit.DB.Electrical;

namespace Ampere.Revit.Previsao;

/// <summary>
///     Porta <see cref="IDocumentoDePrevisao" /> sobre a API do Revit: os cômodos com área, perímetro e os pontos de carga
///     de cada um, e as categorias guardadas por Extensible Storage num <c>DataStorage</c> do Ampere.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Cômodos: os Rooms colocados do modelo e dos vínculos carregados (um por instância de vínculo: o
///         apartamento-tipo repetido conta em cada posição); sem nenhum Room, os Spaces do modelo. Nunca os dois juntos —
///         o mesmo cômodo contaria duas vezes.</item>
///         <item>Pontos: os de <see cref="PontosDeCarga" /> com AMP_TipoCarga, no cômodo achado pela mesma busca dos "Locais
///         pelos ambientes" (<see cref="BuscaDeAmbiente" />), mas só entre os cômodos listados; os outros vão como fora dos
///         cômodos. Circuito do ponto: o circuito de força de que ele é membro (o de menor Id, se houver mais de um), com o
///         nome "quadro-número" do Ampere ou, fora do Ampere, o do Revit.</item>
///         <item>Área em m² com 4 casas e perímetro em m com 3: o resíduo da conversão de pés não pode virar um ponto de
///         tomada a mais ("a cada 5 m, ou fração").</item>
///     </list>
/// </remarks>
public sealed class DocumentoDePrevisaoRevit(Document documento) : IDocumentoDePrevisao
{
    private static readonly EsquemaJson Esquema = new(new Guid(CategoriasEmJson.GuidDoEsquema), "AmpereCategoriasDosComodos", "CategoriasJson",
        "Ampere: categoria de cômodo da previsão de cargas (NBR 5410, 9.5.2) por nome de ambiente (JSON versionado).");

    public void EmUmaTransacao(string nome, Action acao) => TransacaoRevit.Executar(documento, nome, acao);

    public IReadOnlyDictionary<string, string> LerCategorias() =>
        CategoriasEmJson.Ler(Esquema.LerDoProjeto(documento)) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string? GravarCategorias(IReadOnlyDictionary<string, string> categoriaPorNome) =>
        Esquema.GravarNoProjeto(documento, CategoriasEmJson.Escrever(categoriaPorNome));

    public LeituraDaPrevisao Ler()
    {
        var vinculos = BuscaDeAmbiente.Vinculos(documento);
        var ambientes = Ambientes(documento, 0, BuiltInCategory.OST_Rooms)
            .Concat(vinculos.SelectMany(vinculo => Ambientes(vinculo.Documento, vinculo.Instancia, BuiltInCategory.OST_Rooms)))
            .ToList();
        var espacos = ambientes.Count == 0;
        if (espacos) ambientes = Ambientes(documento, 0, BuiltInCategory.OST_MEPSpaces).ToList();
        var listados = ambientes.Select(ambiente => ambiente.Chave).ToHashSet(StringComparer.Ordinal);

        var pontos = new Dictionary<string, List<PontoDoComodo>>(StringComparer.Ordinal);
        var fora = new List<PontoDoComodo>();
        var circuitos = new Dictionary<long, string>();
        foreach (var instancia in PontosDeCarga.Instancias(documento))
        {
            if (!CodigosDeTipoDeCarga.TryLer(ParametrosAmpere.LerTexto(instancia, ParametrosAmpere.TipoCarga), out var tipo)) continue;

            var circuito = Circuito(instancia);
            if (circuito is not null && !circuitos.ContainsKey(circuito.Id.Value)) circuitos[circuito.Id.Value] = Nome(circuito);
            var ponto = new PontoDoComodo(instancia.Id.Value, tipo,
                ParametrosAmpere.Ler(instancia, ParametrosAmpere.PotenciaInstaladaVA) is { HasValue: true } potencia
                    ? Math.Round((decimal)UnitUtils.ConvertFromInternalUnits(potencia.AsDouble(), UnitTypeId.VoltAmperes), 6, MidpointRounding.AwayFromZero)
                    : null,
                circuito?.Id.Value,
                ParametrosAmpere.Ler(instancia, ParametrosAmpere.TensaoCircuitoV) is { HasValue: true } tensao ? (decimal)tensao.AsDouble() : null,
                Texto(instancia, ParametrosAmpere.Fases));

            if (Chave(instancia, espacos, vinculos, listados) is not { } chave)
            {
                fora.Add(ponto);
                continue;
            }

            if (!pontos.TryGetValue(chave, out var doComodo)) pontos[chave] = doComodo = [];
            doComodo.Add(ponto);
        }

        var comodos = ambientes
            .Select(ambiente => new ComodoDoProjeto(ambiente.Chave, ambiente.Nome, ambiente.Numero, ambiente.Pavimento, ambiente.AreaM2, ambiente.PerimetroM,
                pontos.TryGetValue(ambiente.Chave, out var doComodo) ? doComodo.OrderBy(ponto => ponto.Id).ToList() : []))
            .ToList();
        return new LeituraDaPrevisao(comodos, fora.OrderBy(ponto => ponto.Id).ToList(), circuitos);
    }

    // O circuito de força de que o ponto é membro; o de menor Id, se ele tiver mais de um conector ligado.
    private static ElectricalSystem? Circuito(FamilyInstance ponto) =>
        ponto.MEPModel?.GetElectricalSystems()?
            .Where(sistema => sistema.SystemType == ElectricalSystemType.PowerCircuit)
            .OrderBy(sistema => sistema.Id.Value)
            .FirstOrDefault();

    private static string Nome(ElectricalSystem circuito) =>
        ParametrosAmpere.LerTexto(circuito, ParametrosAmpere.NumeroCircuito) is { Length: > 0 } numero
            ? string.IsNullOrWhiteSpace(circuito.PanelName) ? numero : $"{circuito.PanelName}-{numero}"
            : $"{circuito.PanelName} {circuito.CircuitNumber}".Trim();

    private static string? Texto(Element elemento, DefinicaoDeParametro definicao) =>
        ParametrosAmpere.LerTexto(elemento, definicao) is { Length: > 0 } texto ? texto : null;

    // O cômodo do ponto, entre os listados: o do próprio elemento, os pontos deslocados no modelo e, sem eles, nos vínculos.
    private string? Chave(FamilyInstance ponto, bool espacos, IReadOnlyList<VinculoDeAmbientes> vinculos, HashSet<string> listados)
    {
        string? Listado(Element? ambiente, long vinculo) =>
            ambiente is null ? null : listados.Contains(ChaveDe(vinculo, ambiente)) ? ChaveDe(vinculo, ambiente) : null;

        Element? NoModelo(XYZ ponto1) => espacos ? documento.GetSpaceAtPoint(ponto1) : documento.GetRoomAtPoint(ponto1);

        if (Listado(espacos ? ponto.Space : ponto.Room, 0) is { } direto) return direto;
        var candidatos = BuscaDeAmbiente.Candidatos(ponto);
        foreach (var candidato in candidatos)
        {
            if (Listado(NoModelo(candidato), 0) is { } proximo) return proximo;
        }

        if (espacos || BuscaDeAmbiente.Origem(ponto) is not { } origem) return null;
        foreach (var vinculo in vinculos)
        foreach (var candidato in candidatos.Prepend(origem))
        {
            if (Listado(vinculo.Documento.GetRoomAtPoint(vinculo.Inversa.OfPoint(candidato)), vinculo.Instancia) is { } doVinculo) return doVinculo;
        }

        return null;
    }

    private static IEnumerable<Ambiente> Ambientes(Document origem, long vinculo, BuiltInCategory categoria) =>
        new FilteredElementCollector(origem)
            .OfCategory(categoria)
            .WhereElementIsNotElementType()
            .OfType<SpatialElement>()
            .Where(ambiente => ambiente.Location is not null)
            .Select(ambiente => new Ambiente(
                ChaveDe(vinculo, ambiente),
                Texto(ambiente, BuiltInParameter.ROOM_NAME) ?? ambiente.Name,
                Texto(ambiente, BuiltInParameter.ROOM_NUMBER),
                ambiente.Level?.Name,
                Math.Round((decimal)UnitUtils.ConvertFromInternalUnits(ambiente.Area, UnitTypeId.SquareMeters), 4, MidpointRounding.AwayFromZero),
                Math.Round((decimal)UnitUtils.ConvertFromInternalUnits(ambiente.Perimeter, UnitTypeId.Meters), 3, MidpointRounding.AwayFromZero)));

    private static string ChaveDe(long vinculo, Element ambiente) => $"{vinculo}:{ambiente.Id.Value}";

    private static string? Texto(Element elemento, BuiltInParameter parametro) =>
        elemento.get_Parameter(parametro)?.AsString() is { } texto && !string.IsNullOrWhiteSpace(texto) ? texto.Trim() : null;

    private sealed record Ambiente(string Chave, string Nome, string? Numero, string? Pavimento, decimal AreaM2, decimal PerimetroM);
}
