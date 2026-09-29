namespace Ampere.Core.Parametros;

/// <summary>
///     Decide, parâmetro a parâmetro, o que a injeção precisa fazer no documento. Função pura: mesmas entradas,
///     mesmo plano — e aplicar o plano e replanejar resulta em <see cref="PlanoDeInjecao.NadaAFazer"/>.
/// </summary>
/// <remarks>
///     Política: nunca duplicar, nunca remover. Homônimo com outro GUID, GUID com outro nome ou tipo e vínculo por
///     tipo são conflitos, e um único conflito aborta a injeção inteira (<see cref="PlanoDeInjecao.TemConflitos"/>).
///     Categorias só são acrescentadas; as que já estão no vínculo ficam.
/// </remarks>
public static class PlanejadorDeInjecao
{
    public static PlanoDeInjecao Planejar(
        IReadOnlyList<DefinicaoDeParametro> desejados,
        IReadOnlyCollection<ParametroExistente> existentes) =>
        new(desejados.Select(desejado => Decidir(desejado, existentes)).ToList());

    private static AcaoDeInjecao Decidir(DefinicaoDeParametro desejado, IReadOnlyCollection<ParametroExistente> existentes)
    {
        var homonimo = existentes.FirstOrDefault(existente =>
            existente.Guid != desejado.Guid && string.Equals(existente.Nome, desejado.Nome, StringComparison.OrdinalIgnoreCase));
        if (homonimo is not null)
        {
            return new AcaoDeInjecao.Conflito(desejado, homonimo.Guid is { } outroGuid
                ? $"já existe o parâmetro compartilhado '{homonimo.Nome}' com outro GUID ({outroGuid}); vincular o do Ampere criaria um homônimo"
                : $"já existe o parâmetro não compartilhado '{homonimo.Nome}' (de projeto ou global); vincular o do Ampere criaria um homônimo");
        }

        var mesmoGuid = existentes.FirstOrDefault(existente => existente.Guid == desejado.Guid);
        if (mesmoGuid is null) return new AcaoDeInjecao.Criar(desejado);

        if (mesmoGuid.Nome != desejado.Nome)
            return new AcaoDeInjecao.Conflito(desejado, $"o GUID {desejado.Guid} já pertence ao parâmetro '{mesmoGuid.Nome}'");

        if (mesmoGuid.Tipo != desejado.Tipo)
        {
            return new AcaoDeInjecao.Conflito(desejado,
                $"o parâmetro existe no documento com outro tipo de dado ({mesmoGuid.Tipo?.ToString() ?? "não usado pelo Ampere"})");
        }

        if (mesmoGuid.Vinculo is null) return new AcaoDeInjecao.Criar(desejado);

        if (!mesmoGuid.Vinculo.PorInstancia)
            return new AcaoDeInjecao.Conflito(desejado, "vinculado como parâmetro de tipo; o Ampere exige parâmetro de instância");

        var faltantes = desejado.Categorias.Except(mesmoGuid.Vinculo.CategoriasAmpere).ToList();
        return faltantes.Count == 0
            ? new AcaoDeInjecao.JaConforme(desejado)
            : new AcaoDeInjecao.AmpliarCategorias(desejado, faltantes);
    }
}
