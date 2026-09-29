using Ampere.Core.Circuitos;

namespace Ampere.Core.Cargas;

/// <summary>
///     Caso de uso "Classificar cargas": valida a classificação, separa os elementos que a aceitam e grava tudo numa
///     única transação — um único desfazer. Classificação inválida não lê nem escreve nada.
/// </summary>
public static class ClassificacaoEmLote
{
    /// <summary>Nome da transação, que aparece no menu Desfazer do Revit.</summary>
    public const string NomeDaTransacao = "Ampere: classificar cargas";

    public static ResultadoDaClassificacao Executar(IReadOnlyCollection<long> ids, ClassificacaoDeCarga classificacao, IDocumentoEletrico documento)
    {
        var problemas = classificacao.Validar();
        if (problemas.Count > 0) return new ResultadoDaClassificacao(0, [], problemas);

        var filtrados = documento.FiltrarClassificaveis(ids);
        if (filtrados.Aceitos.Count > 0)
            documento.EmUmaTransacao(NomeDaTransacao, () => documento.Classificar(filtrados.Aceitos, classificacao));

        return new ResultadoDaClassificacao(filtrados.Aceitos.Count, filtrados.Recusados, []);
    }
}

/// <summary>
///     Resultado da classificação em lote.
/// </summary>
/// <param name="Classificados">Quantos elementos receberam a classificação.</param>
/// <param name="Recusados">Elementos que não aceitam a classificação, com o motivo.</param>
/// <param name="Problemas">Problemas da classificação em si; se houver, nada foi gravado.</param>
public sealed record ResultadoDaClassificacao(int Classificados, IReadOnlyList<PontoIgnorado> Recusados, IReadOnlyList<string> Problemas);
