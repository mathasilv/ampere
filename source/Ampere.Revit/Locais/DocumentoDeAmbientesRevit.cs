using Ampere.Core.Cargas;
using Ampere.Core.Locais;
using Ampere.Core.Parametros;
using Ampere.Revit.Parametros;

namespace Ampere.Revit.Locais;

/// <summary>
///     Porta <see cref="IDocumentoDeAmbientes" /> sobre a API do Revit: o ambiente de cada ponto classificado.
/// </summary>
/// <remarks>
///     Ordem de busca do ambiente, parando no primeiro que achar: Room e Space do próprio elemento (o Revit usa o ponto
///     de cálculo da família); depois um ponto 10 cm para dentro do ambiente, na normal da face hospedeira (tomada na
///     parede, luminária no forro), e 10 cm abaixo do ponto (luminária acima do limite do ambiente); por fim os Rooms dos
///     modelos vinculados (arquitetura em vínculo, o caso comum), com o ponto levado para as coordenadas do vínculo.
///     O nome é o parâmetro "Nome" do ambiente, sem o número.
/// </remarks>
public sealed class DocumentoDeAmbientesRevit(Document documento) : IDocumentoDeAmbientes
{
    private static readonly double Deslocamento = UnitUtils.ConvertToInternalUnits(0.10, UnitTypeId.Meters);

    private static readonly BuiltInCategory[] CategoriasDosPontos = CatalogoDeParametros.Padrao.Parametros
        .Single(parametro => parametro.Nome == ParametrosAmpere.Local.Nome).Categorias
        .Select(MapeamentoRevit.Categoria)
        .ToArray();

    public void EmUmaTransacao(string nome, Action acao) => TransacaoRevit.Executar(documento, nome, acao);

    public IReadOnlyList<PontoNoAmbiente> LerPontos(IReadOnlyCollection<long> ids)
    {
        var elementos = ids.Count > 0
            ? ids.Select(id => documento.GetElement(new ElementId(id))).OfType<FamilyInstance>()
            : new FilteredElementCollector(documento)
                .WherePasses(new ElementMulticategoryFilter(CategoriasDosPontos))
                .WhereElementIsNotElementType()
                .OfType<FamilyInstance>();

        var vinculos = Vinculos();
        return elementos
            .Where(ponto => CodigosDeTipoDeCarga.TryLer(ParametrosAmpere.LerTexto(ponto, ParametrosAmpere.TipoCarga), out _))
            .Where(ponto => ParametrosAmpere.Ler(ponto, ParametrosAmpere.Local) is not null)
            .Select(ponto => new PontoNoAmbiente(
                ponto.Id.Value,
                Ambiente(ponto, vinculos),
                ParametrosAmpere.LerTexto(ponto, ParametrosAmpere.Local) is { Length: > 0 } local ? local : null,
                !ParametrosAmpere.Ler(ponto, ParametrosAmpere.Local)!.IsReadOnly))
            .ToList();
    }

    public void GravarLocais(IReadOnlyList<LocalParaGravar> locais)
    {
        foreach (var local in locais)
        {
            var ponto = documento.GetElement(new ElementId(local.Ponto))
                        ?? throw new InvalidOperationException($"O ponto {local.Ponto} não existe no documento.");
            ParametrosAmpere.GravarTexto(ponto, ParametrosAmpere.Local, local.Local);
        }
    }

    private string? Ambiente(FamilyInstance ponto, IReadOnlyList<(Document Documento, Transform Inversa)> vinculos)
    {
        if ((Nome(ponto.Room) ?? Nome(ponto.Space)) is { } direto) return direto;
        if (ponto.Location is not LocationPoint { Point: var origem }) return null;

        XYZ[] candidatos = [origem + ponto.GetTotalTransform().BasisZ * Deslocamento, origem - XYZ.BasisZ * Deslocamento];
        foreach (var candidato in candidatos)
        {
            if ((Nome(documento.GetRoomAtPoint(candidato)) ?? Nome(documento.GetSpaceAtPoint(candidato))) is { } proximo) return proximo;
        }

        foreach (var (vinculado, inversa) in vinculos)
        foreach (var candidato in candidatos.Prepend(origem))
        {
            if (Nome(vinculado.GetRoomAtPoint(inversa.OfPoint(candidato))) is { } doVinculo) return doVinculo;
        }

        return null;
    }

    private List<(Document Documento, Transform Inversa)> Vinculos() =>
        new FilteredElementCollector(documento)
            .OfCategory(BuiltInCategory.OST_RvtLinks)
            .OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>()
            .Select(vinculo => (Documento: vinculo.GetLinkDocument(), Inversa: vinculo.GetTotalTransform().Inverse))
            .Where(par => par.Documento is not null)
            .ToList();

    private static string? Nome(Element? ambiente) =>
        ambiente?.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() is { } nome && !string.IsNullOrWhiteSpace(nome) ? nome.Trim() : null;
}
