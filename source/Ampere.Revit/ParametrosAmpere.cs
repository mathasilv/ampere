using Ampere.Core.Parametros;

namespace Ampere.Revit;

/// <summary>
///     Leitura e escrita dos parâmetros AMP_* por GUID — os GUIDs vêm do catálogo, nunca repetidos no código.
/// </summary>
internal static class ParametrosAmpere
{
    public static readonly DefinicaoDeParametro NumeroCircuito = Definicao("AMP_NumeroCircuito");
    public static readonly DefinicaoDeParametro TipoCarga = Definicao("AMP_TipoCarga");
    public static readonly DefinicaoDeParametro TensaoCircuitoV = Definicao("AMP_TensaoCircuitoV");
    public static readonly DefinicaoDeParametro Fases = Definicao("AMP_Fases");
    public static readonly DefinicaoDeParametro PotenciaInstaladaVA = Definicao("AMP_PotenciaInstaladaVA");
    public static readonly DefinicaoDeParametro FatorPotencia = Definicao("AMP_FatorPotencia");
    public static readonly DefinicaoDeParametro Quadro = Definicao("AMP_Quadro");
    public static readonly DefinicaoDeParametro FatorDemanda = Definicao("AMP_FatorDemanda");
    public static readonly DefinicaoDeParametro MemoriaCalculoId = Definicao("AMP_MemoriaCalculoId");

    /// <summary>O parâmetro existe no documento (a injeção já foi feita)?</summary>
    public static bool Injetado(Document documento, DefinicaoDeParametro definicao) =>
        SharedParameterElement.Lookup(documento, definicao.Guid) is not null;

    public static Parameter? Ler(Element elemento, DefinicaoDeParametro definicao) => elemento.get_Parameter(definicao.Guid);

    public static string? LerTexto(Element elemento, DefinicaoDeParametro definicao) =>
        Ler(elemento, definicao) is { HasValue: true } parametro ? parametro.AsString() : null;

    public static void GravarTexto(Element elemento, DefinicaoDeParametro definicao, string valor)
    {
        if (!Exigir(elemento, definicao).Set(valor)) throw Recusado(elemento, definicao);
    }

    /// <summary>Grava um valor já em unidades internas do Revit.</summary>
    public static void GravarNumero(Element elemento, DefinicaoDeParametro definicao, double valorInterno)
    {
        if (!Exigir(elemento, definicao).Set(valorInterno)) throw Recusado(elemento, definicao);
    }

    private static Parameter Exigir(Element elemento, DefinicaoDeParametro definicao) =>
        Ler(elemento, definicao) ?? throw new InvalidOperationException(
            $"{definicao.Nome} não existe em {elemento.Id} ({elemento.Category?.Name}). Rode 'Injetar parâmetros'.");

    private static InvalidOperationException Recusado(Element elemento, DefinicaoDeParametro definicao) =>
        new($"O Revit recusou gravar {definicao.Nome} em {elemento.Id} ({elemento.Category?.Name}); o elemento pode estar em grupo ou vínculo.");

    private static DefinicaoDeParametro Definicao(string nome) =>
        CatalogoDeParametros.Padrao.Parametros.Single(parametro => parametro.Nome == nome);
}
