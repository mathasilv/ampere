using Ampere.Core.Normas;

namespace Ampere.Core.Dimensionamento;

/// <summary>
///     Porta para os circuitos de um documento no dimensionamento (implementada pelo adapter Revit).
/// </summary>
public interface IDocumentoDeDimensionamento : IDocumentoTransacional
{
    /// <summary>Valores AMP_* dos circuitos e dos seus pontos.</summary>
    IReadOnlyList<DadosDoCircuito> LerCircuitos(IReadOnlyCollection<long> ids);

    /// <summary>Grava os resultados (AMP_* e o hash da memória) nos circuitos.</summary>
    void GravarResultados(IReadOnlyList<ResultadoDoCircuito> resultados);
}

/// <summary>Resultado de um circuito: o dimensionamento, ou os problemas de dados que o impediram.</summary>
public sealed record ResultadoDoCircuito(
    long Id,
    string? Numero,
    ResultadoDoDimensionamento? Dimensionamento,
    IReadOnlyList<string> ProblemasDeDados);

/// <summary>
///     Caso de uso "Dimensionar circuitos": lê os circuitos, dimensiona cada um contra o perfil e grava tudo o que tem
///     memória numa única transação — um único desfazer. Circuito com dados faltando não é gravado; circuito
///     interrompido por falta de dado normativo é gravado, e a memória mostra onde parou.
/// </summary>
public static class DimensionamentoDoProjeto
{
    /// <summary>Nome da transação, que aparece no menu Desfazer do Revit.</summary>
    public const string NomeDaTransacao = "Ampere: dimensionar circuitos";

    public static IReadOnlyList<ResultadoDoCircuito> Executar(
        IReadOnlyCollection<long> circuitos,
        CondicoesDoProjeto condicoes,
        PerfilNormativo perfil,
        IDocumentoDeDimensionamento documento)
    {
        var resultados = documento.LerCircuitos(circuitos)
            .Select(dados =>
            {
                var montada = EntradaDoCircuito.Montar(dados, condicoes);
                return montada.Entrada is null
                    ? new ResultadoDoCircuito(dados.Id, dados.Numero, null, montada.Problemas)
                    : new ResultadoDoCircuito(dados.Id, dados.Numero, DimensionamentoDeCircuito.Dimensionar(montada.Entrada, perfil), []);
            })
            .ToList();

        var gravaveis = resultados.Where(resultado => resultado.Dimensionamento?.Memoria is not null).ToList();
        if (gravaveis.Count > 0) documento.EmUmaTransacao(NomeDaTransacao, () => documento.GravarResultados(gravaveis));

        return resultados;
    }
}
