namespace Ampere.Core.Parametros;

/// <summary>
///     O catálogo de parâmetros viola alguma regra. Traz todos os problemas encontrados, não só o primeiro.
/// </summary>
public sealed class CatalogoDeParametrosInvalidoException(IReadOnlyList<string> problemas)
    : Exception("Catálogo de parâmetros inválido:\n" + string.Join("\n", problemas.Select(problema => "- " + problema)))
{
    /// <summary>Problemas encontrados, um por item.</summary>
    public IReadOnlyList<string> Problemas { get; } = problemas;
}
