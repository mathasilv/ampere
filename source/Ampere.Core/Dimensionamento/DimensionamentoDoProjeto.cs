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

    /// <summary>
    ///     Guarda as condições usadas na rodada (<see cref="CondicoesEmJson" />) em cada circuito dela — para conferir a
    ///     memória dele depois — e, numa rodada do projeto todo, também como as condições do projeto, que abrem o diálogo.
    ///     Chamado na mesma transação dos resultados.
    /// </summary>
    void GravarCondicoes(CondicoesDoProjeto condicoes, IReadOnlyCollection<long> circuitos, bool doProjetoTodo);
}

/// <summary>Resultado de um circuito: o dimensionamento, ou os problemas de dados que o impediram.</summary>
/// <param name="Quadro">AMP_Quadro do circuito (para relatórios e resumo).</param>
/// <param name="TipoDeCarga">AMP_TipoCarga do circuito, como lido.</param>
/// <param name="PotenciaVA">Soma das potências dos pontos; nula se algum ponto não tem potência.</param>
/// <param name="Entrada">A entrada dimensionada (nula se faltaram dados): fases, comprimento e tipos, para a lista de materiais.</param>
public sealed record ResultadoDoCircuito(
    long Id,
    string? Numero,
    ResultadoDoDimensionamento? Dimensionamento,
    IReadOnlyList<string> ProblemasDeDados,
    string? Quadro = null,
    string? TipoDeCarga = null,
    decimal? PotenciaVA = null,
    EntradaDeDimensionamento? Entrada = null)
{
    /// <summary>A memória a gravar, se o circuito chegou a ser calculado.</summary>
    public MemoriaDeCalculo? Memoria => Dimensionamento?.Memoria;
}

/// <summary>
///     Caso de uso "Dimensionar circuitos": lê os circuitos, dimensiona cada um contra o perfil e grava todos numa única
///     transação — um único desfazer. Circuito interrompido por falta de dado normativo grava a corrente de projeto, os
///     fatores e o hash da memória (que mostra onde parou); a proteção (seção, disjuntor, IDR, eletroduto) só se o IDR foi
///     decidido por completo. Circuito com dados faltando não tem memória e fica com os resultados apagados — depois do
///     comando, nenhum circuito mostra resultado de uma rodada anterior. As condições usadas ficam em cada circuito e,
///     numa rodada do projeto todo, também como as do projeto — na mesma transação. A rodada só da seleção não troca as
///     condições do projeto.
/// </summary>
public static class DimensionamentoDoProjeto
{
    /// <summary>Nome da transação, que aparece no menu Desfazer do Revit.</summary>
    public const string NomeDaTransacao = "Ampere: dimensionar circuitos";

    /// <param name="doProjetoTodo">A rodada é do projeto todo (não só da seleção): as condições viram as do projeto.</param>
    public static IReadOnlyList<ResultadoDoCircuito> Executar(
        IReadOnlyCollection<long> circuitos,
        CondicoesDoProjeto condicoes,
        PerfilNormativo perfil,
        CatalogosDeProduto catalogos,
        IDocumentoDeDimensionamento documento,
        bool doProjetoTodo = true)
    {
        var resultados = documento.LerCircuitos(circuitos).Select(dados => Calcular(dados, condicoes, perfil, catalogos)).ToList();

        if (resultados.Count > 0)
        {
            documento.EmUmaTransacao(NomeDaTransacao, () =>
            {
                documento.GravarResultados(resultados);
                documento.GravarCondicoes(condicoes, resultados.Select(resultado => resultado.Id).ToList(), doProjetoTodo);
            });
        }

        return resultados;
    }

    /// <summary>
    ///     O cálculo de um circuito, sem gravar nada: o mesmo para o "Dimensionar" e para a conferência das memórias do
    ///     "Verificar projeto".
    /// </summary>
    public static ResultadoDoCircuito Calcular(DadosDoCircuito dados, CondicoesDoProjeto condicoes, PerfilNormativo perfil, CatalogosDeProduto catalogos)
    {
        var montada = EntradaDoCircuito.Montar(dados, condicoes, perfil);
        var potencia = dados.Pontos.Count > 0 && dados.Pontos.All(ponto => ponto.PotenciaVA is not null)
            ? dados.Pontos.Sum(ponto => ponto.PotenciaVA!.Value)
            : (decimal?)null;
        return montada.Entrada is null
            ? new ResultadoDoCircuito(dados.Id, dados.Numero, null, montada.Problemas, dados.Quadro, dados.TipoDeCarga, potencia)
            : new ResultadoDoCircuito(dados.Id, dados.Numero, DimensionamentoDeCircuito.Dimensionar(montada.Entrada, perfil, catalogos), [],
                dados.Quadro, dados.TipoDeCarga, potencia, montada.Entrada);
    }
}
