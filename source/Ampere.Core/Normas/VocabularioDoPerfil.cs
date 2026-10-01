namespace Ampere.Core.Normas;

/// <summary>
///     Textos que as tabelas do perfil reconhecem — o que os diálogos oferecem para o projetista escolher, em vez de
///     digitar um nome que o motor não encontraria.
/// </summary>
/// <param name="MetodosDeInstalacao">Métodos da tabela de capacidade de condução (AMP_MetodoInstalacao).</param>
/// <param name="Isolacoes">Isolações da tabela de capacidade de condução (AMP_MaterialIsolacao).</param>
/// <param name="Materiais">Materiais de condutor da tabela de capacidade de condução.</param>
/// <param name="Locais">Locais da tabela de proteção diferencial (AMP_Local).</param>
public sealed record VocabularioDoPerfil(
    IReadOnlyList<string> MetodosDeInstalacao,
    IReadOnlyList<string> Isolacoes,
    IReadOnlyList<string> Materiais,
    IReadOnlyList<string> Locais);
