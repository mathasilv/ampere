namespace Ampere.Core.Parametros;

/// <summary>
///     Definição de um parâmetro compartilhado Ampere (AMP_*), como declarada no catálogo.
/// </summary>
/// <param name="Guid">GUID congelado do parâmetro — nunca muda.</param>
/// <param name="Nome">Nome do parâmetro, sempre com o prefixo <c>AMP_</c>.</param>
/// <param name="Tipo">Tipo de dado.</param>
/// <param name="Descricao">Descrição exibida no Revit.</param>
/// <param name="Categorias">Categorias às quais o parâmetro é vinculado por instância, na ordem do catálogo.</param>
public sealed record DefinicaoDeParametro(
    Guid Guid,
    string Nome,
    TipoDeDadoDoParametro Tipo,
    string Descricao,
    IReadOnlyList<CategoriaEletrica> Categorias);
