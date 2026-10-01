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
    ///     Guarda no documento as condições do projeto usadas na rodada (<see cref="CondicoesEmJson" />), para quem abrir o
    ///     projeto depois reproduzir as mesmas memórias. Chamado na mesma transação dos resultados.
    /// </summary>
    void GravarCondicoes(CondicoesDoProjeto condicoes);
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
///     comando, nenhum circuito mostra resultado de uma rodada anterior. As condições do projeto usadas ficam no documento,
///     na mesma transação.
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
                var potencia = dados.Pontos.Count > 0 && dados.Pontos.All(ponto => ponto.PotenciaVA is not null)
                    ? dados.Pontos.Sum(ponto => ponto.PotenciaVA!.Value)
                    : (decimal?)null;
                return montada.Entrada is null
                    ? new ResultadoDoCircuito(dados.Id, dados.Numero, null, montada.Problemas, dados.Quadro, dados.TipoDeCarga, potencia)
                    : new ResultadoDoCircuito(dados.Id, dados.Numero, DimensionamentoDeCircuito.Dimensionar(montada.Entrada, perfil, catalogos), [],
                        dados.Quadro, dados.TipoDeCarga, potencia, montada.Entrada);
            })
            .ToList();

        if (resultados.Count > 0)
        {
            documento.EmUmaTransacao(NomeDaTransacao, () =>
            {
                documento.GravarResultados(resultados);
                documento.GravarCondicoes(condicoes);
            });
        }

        return resultados;
    }
}
