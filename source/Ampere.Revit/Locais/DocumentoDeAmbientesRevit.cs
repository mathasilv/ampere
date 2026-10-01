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
///     de cálculo da família); depois os pontos deslocados de <see cref="BuscaDeAmbiente" />; por fim os Rooms dos
///     modelos vinculados (arquitetura em vínculo, o caso comum), com o ponto levado para as coordenadas do vínculo.
///     O nome é o parâmetro "Nome" do ambiente, sem o número.
/// </remarks>
public sealed class DocumentoDeAmbientesRevit(Document documento) : IDocumentoDeAmbientes
{
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

        var vinculos = BuscaDeAmbiente.Vinculos(documento);
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

    private string? Ambiente(FamilyInstance ponto, IReadOnlyList<VinculoDeAmbientes> vinculos)
    {
        if ((Nome(ponto.Room) ?? Nome(ponto.Space)) is { } direto) return direto;
        if (BuscaDeAmbiente.Origem(ponto) is not { } origem) return null;

        var candidatos = BuscaDeAmbiente.Candidatos(ponto);
        foreach (var candidato in candidatos)
        {
            if ((Nome(documento.GetRoomAtPoint(candidato)) ?? Nome(documento.GetSpaceAtPoint(candidato))) is { } proximo) return proximo;
        }

        foreach (var vinculo in vinculos)
        foreach (var candidato in candidatos.Prepend(origem))
        {
            if (Nome(vinculo.Documento.GetRoomAtPoint(vinculo.Inversa.OfPoint(candidato))) is { } doVinculo) return doVinculo;
        }

        return null;
    }

    private static string? Nome(Element? ambiente) =>
        ambiente?.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() is { } nome && !string.IsNullOrWhiteSpace(nome) ? nome.Trim() : null;
}
