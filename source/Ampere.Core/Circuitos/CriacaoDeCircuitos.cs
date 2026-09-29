using Ampere.Core.Cargas;

namespace Ampere.Core.Circuitos;

/// <summary>
///     Caso de uso "Criar circuitos": lê os pontos e o quadro, planeja e cria todos os circuitos numa única transação
///     — um único desfazer. Sem circuito a criar, não abre transação.
/// </summary>
public static class CriacaoDeCircuitos
{
    /// <summary>Nome da transação, que aparece no menu Desfazer do Revit.</summary>
    public const string NomeDaTransacao = "Ampere: criar circuitos";

    /// <returns>O plano executado; os ignorados incluem os recusados na leitura, em ordem de Id.</returns>
    /// <exception cref="ArgumentException">Alguma regra de agrupamento é inválida (nada é escrito).</exception>
    public static PlanoDeCircuitos Executar(
        IReadOnlyCollection<long> ids,
        long quadroId,
        IReadOnlyDictionary<TipoDeCarga, RegraDeAgrupamento> regras,
        ConfiguracaoDeNumeracao numeracao,
        IDocumentoEletrico documento)
    {
        var leitura = documento.LerPontos(ids);
        var quadro = documento.LerQuadro(quadroId);
        var plano = PlanejadorDeCircuitos.Planejar(leitura.Pontos, regras, numeracao, quadro.NumerosExistentes);

        if (plano.Circuitos.Count > 0)
            documento.EmUmaTransacao(NomeDaTransacao, () => documento.CriarCircuitos(quadroId, plano.Circuitos));

        return plano with { Ignorados = leitura.Recusados.Concat(plano.Ignorados).OrderBy(ignorado => ignorado.Id).ToList() };
    }
}
