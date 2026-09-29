namespace Ampere.Core.Parametros;

/// <summary>
///     Parâmetro que já existe no documento, como o adapter o enxerga.
/// </summary>
/// <param name="Nome">Nome do parâmetro no documento.</param>
/// <param name="Guid">GUID, se for compartilhado; <c>null</c> para parâmetro de projeto ou global.</param>
/// <param name="Tipo">Tipo de dado, se for um dos usados pelo Ampere; <c>null</c> para qualquer outro.</param>
/// <param name="Vinculo">Vínculo a categorias; <c>null</c> se o parâmetro existe mas não está vinculado.</param>
public sealed record ParametroExistente(
    string Nome,
    Guid? Guid,
    TipoDeDadoDoParametro? Tipo,
    VinculoExistente? Vinculo);

/// <summary>
///     Vínculo de um parâmetro a categorias do documento.
/// </summary>
/// <param name="PorInstancia"><c>true</c> para parâmetro de instância; <c>false</c> para parâmetro de tipo.</param>
/// <param name="CategoriasAmpere">
///     Categorias vinculadas que o Ampere conhece. Outras categorias do vínculo não aparecem aqui e o adapter as
///     preserva ao ampliar.
/// </param>
public sealed record VinculoExistente(
    bool PorInstancia,
    IReadOnlyCollection<CategoriaEletrica> CategoriasAmpere);
