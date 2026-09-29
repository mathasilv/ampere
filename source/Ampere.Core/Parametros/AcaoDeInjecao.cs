namespace Ampere.Core.Parametros;

/// <summary>
///     O que fazer com um parâmetro do catálogo para deixá-lo conforme no documento.
/// </summary>
public abstract record AcaoDeInjecao(DefinicaoDeParametro Definicao)
{
    /// <summary>Vincular o parâmetro às categorias do catálogo (criando-o no documento, se preciso).</summary>
    public sealed record Criar(DefinicaoDeParametro Definicao) : AcaoDeInjecao(Definicao);

    /// <summary>Nada a fazer: o parâmetro já está vinculado por instância a todas as categorias do catálogo.</summary>
    public sealed record JaConforme(DefinicaoDeParametro Definicao) : AcaoDeInjecao(Definicao);

    /// <summary>Acrescentar categorias ao vínculo existente, preservando as que já estão lá.</summary>
    public sealed record AmpliarCategorias(DefinicaoDeParametro Definicao, IReadOnlyList<CategoriaEletrica> Faltantes)
        : AcaoDeInjecao(Definicao);

    /// <summary>O documento tem algo incompatível com o parâmetro; a injeção inteira deve ser abortada.</summary>
    public sealed record Conflito(DefinicaoDeParametro Definicao, string Motivo) : AcaoDeInjecao(Definicao);
}
