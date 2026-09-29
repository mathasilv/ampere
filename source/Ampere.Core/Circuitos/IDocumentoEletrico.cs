using Ampere.Core.Cargas;

namespace Ampere.Core.Circuitos;

/// <summary>
///     Porta para os elementos elétricos de um documento (implementada pelo adapter Revit): classificação de cargas e
///     criação de circuitos, isolando a API do Revit da regra.
/// </summary>
public interface IDocumentoEletrico : IDocumentoTransacional
{
    /// <summary>Separa os elementos que aceitam a classificação Ampere dos que não aceitam (com o motivo).</summary>
    ElementosFiltrados FiltrarClassificaveis(IReadOnlyCollection<long> ids);

    /// <summary>Grava a classificação nos elementos; campo nulo da classificação não é alterado.</summary>
    void Classificar(IReadOnlyCollection<long> ids, ClassificacaoDeCarga classificacao);

    /// <summary>Lê os pontos de carga; elementos que não são carga elétrica voltam como recusados.</summary>
    LeituraDePontos LerPontos(IReadOnlyCollection<long> ids);

    /// <summary>Quadro com os números (AMP_NumeroCircuito) dos circuitos que ele já alimenta.</summary>
    QuadroEletrico LerQuadro(long quadroId);

    /// <summary>Cria os circuitos no quadro e grava a numeração Ampere nos circuitos e nos pontos.</summary>
    void CriarCircuitos(long quadroId, IReadOnlyList<CircuitoPlanejado> circuitos);
}

/// <summary>Elementos aceitos para classificação e recusados, com o motivo.</summary>
public sealed record ElementosFiltrados(IReadOnlyList<long> Aceitos, IReadOnlyList<PontoIgnorado> Recusados);

/// <summary>Pontos de carga lidos e elementos recusados por não serem carga elétrica.</summary>
public sealed record LeituraDePontos(IReadOnlyList<PontoDeCarga> Pontos, IReadOnlyList<PontoIgnorado> Recusados);

/// <summary>Quadro elétrico e os números de circuito que ele já usa.</summary>
public sealed record QuadroEletrico(long Id, string Nome, IReadOnlyList<string> NumerosExistentes);
