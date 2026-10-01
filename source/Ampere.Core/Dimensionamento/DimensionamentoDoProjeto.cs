using Ampere.Core.Catalogos;
using Ampere.Core.Memoria;
using Ampere.Core.Normas;

namespace Ampere.Core.Dimensionamento;

/// <summary>
///     Porta para os circuitos de um documento no dimensionamento (implementada pelo adapter Revit).
/// </summary>
public interface IDocumentoDeDimensionamento : IDocumentoTransacional
{
    /// <summary>Valores AMP_* dos circuitos e dos seus pontos.</summary>
    IReadOnlyList<DadosDoCircuito> LerCircuitos(IReadOnlyCollection<long> ids);

    /// <summary>
    ///     Grava os resultados (AMP_* e o hash da memória) nos circuitos. Valor não calculado apaga o da rodada anterior —
    ///     inclusive todos os resultados de circuito sem memória (dados faltando ou entrada inválida).
    /// </summary>
    void GravarResultados(IReadOnlyList<ResultadoDoCircuito> resultados);
}

/// <summary>Resultado de um circuito: o dimensionamento, ou os problemas de dados que o impediram.</summary>
/// <param name="Quadro">AMP_Quadro do circuito (para relatórios e resumo).</param>
public sealed record ResultadoDoCircuito(
    long Id,
    string? Numero,
    ResultadoDoDimensionamento? Dimensionamento,
    IReadOnlyList<string> ProblemasDeDados,
    string? Quadro = null)
{
    /// <summary>A memória a gravar, se o circuito chegou a ser calculado.</summary>
    public MemoriaDeCalculo? Memoria => Dimensionamento?.Memoria;
}

/// <summary>
///     Caso de uso "Dimensionar circuitos": lê os circuitos, dimensiona cada um contra o perfil e grava todos numa única
///     transação — um único desfazer. Circuito interrompido por falta de dado normativo é gravado até onde chegou, e a
///     memória mostra onde parou; circuito com dados faltando não tem memória e fica com os resultados apagados — depois
///     do comando, nenhum circuito mostra resultado de uma rodada anterior.
/// </summary>
public static class DimensionamentoDoProjeto
{
    /// <summary>Nome da transação, que aparece no menu Desfazer do Revit.</summary>
    public const string NomeDaTransacao = "Ampere: dimensionar circuitos";

    public static IReadOnlyList<ResultadoDoCircuito> Executar(
        IReadOnlyCollection<long> circuitos,
        CondicoesDoProjeto condicoes,
        PerfilNormativo perfil,
        CatalogosDeProduto catalogos,
        IDocumentoDeDimensionamento documento)
    {
        var resultados = documento.LerCircuitos(circuitos)
            .Select(dados =>
            {
                var montada = EntradaDoCircuito.Montar(dados, condicoes);
                return montada.Entrada is null
                    ? new ResultadoDoCircuito(dados.Id, dados.Numero, null, montada.Problemas, dados.Quadro)
                    : new ResultadoDoCircuito(dados.Id, dados.Numero, DimensionamentoDeCircuito.Dimensionar(montada.Entrada, perfil, catalogos), [], dados.Quadro);
            })
            .ToList();

        if (resultados.Count > 0) documento.EmUmaTransacao(NomeDaTransacao, () => documento.GravarResultados(resultados));

        return resultados;
    }
}
