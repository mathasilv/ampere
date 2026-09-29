namespace Ampere.Core.Parametros;

/// <summary>
///     Ações necessárias para deixar os parâmetros do catálogo conformes no documento, uma por parâmetro, na ordem
///     do catálogo.
/// </summary>
public sealed record PlanoDeInjecao(IReadOnlyList<AcaoDeInjecao> Acoes)
{
    /// <summary>Há conflito: nada deve ser aplicado.</summary>
    public bool TemConflitos => Acoes.Any(acao => acao is AcaoDeInjecao.Conflito);

    /// <summary>Tudo já está conforme: aplicar não mudaria nada.</summary>
    public bool NadaAFazer => Acoes.All(acao => acao is AcaoDeInjecao.JaConforme);
}
