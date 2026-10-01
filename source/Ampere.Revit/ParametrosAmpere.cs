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
    public static readonly DefinicaoDeParametro Local = Definicao("AMP_Local");
    public static readonly DefinicaoDeParametro ComprimentoRotaM = Definicao("AMP_ComprimentoRotaM");
    public static readonly DefinicaoDeParametro MetodoInstalacao = Definicao("AMP_MetodoInstalacao");
    public static readonly DefinicaoDeParametro MaterialIsolacao = Definicao("AMP_MaterialIsolacao");
    public static readonly DefinicaoDeParametro TipoCondutor = Definicao("AMP_TipoCondutor");
    public static readonly DefinicaoDeParametro CorrenteProjetoA = Definicao("AMP_CorrenteProjetoA");
    public static readonly DefinicaoDeParametro BitolaCondutorMm2 = Definicao("AMP_BitolaCondutorMm2");
    public static readonly DefinicaoDeParametro CapacidadeConducaoA = Definicao("AMP_CapacidadeConducaoA");
    public static readonly DefinicaoDeParametro FCA = Definicao("AMP_FCA");
    public static readonly DefinicaoDeParametro FCT = Definicao("AMP_FCT");
    public static readonly DefinicaoDeParametro DisjuntorNominalA = Definicao("AMP_DisjuntorNominalA");
    public static readonly DefinicaoDeParametro IdrNominalA = Definicao("AMP_IDR_NominalA");
    public static readonly DefinicaoDeParametro IdrSensibilidadeMa = Definicao("AMP_IDR_SensibilidadeMa");
    public static readonly DefinicaoDeParametro QuedaTensaoPct = Definicao("AMP_QuedaTensaoPct");
    public static readonly DefinicaoDeParametro EletrodutoTipo = Definicao("AMP_EletrodutoTipo");
    public static readonly DefinicaoDeParametro OcupacaoEletrodutoPct = Definicao("AMP_OcupacaoEletrodutoPct");
    public static readonly DefinicaoDeParametro PerfilNorma = Definicao("AMP_PerfilNorma");

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

    /// <summary>
    ///     Grava o valor (já em unidades internas) ou, se nulo, apaga o anterior. O Revit não devolve um parâmetro
    ///     compartilhado numérico ao estado "sem valor" (<c>ClearValue</c> só vale com HideWhenNoValue, que a injeção não
    ///     usa): o valor anterior vira 0 — nunca um resultado de outra rodada com cara de atual. Parâmetro que nunca teve
    ///     valor continua vazio.
    /// </summary>
    public static void GravarNumeroOuApagar(Element elemento, DefinicaoDeParametro definicao, double? valorInterno)
    {
        var parametro = Exigir(elemento, definicao);
        if (valorInterno is null && !parametro.HasValue) return;
        if (!parametro.Set(valorInterno ?? 0d)) throw Recusado(elemento, definicao);
    }

    /// <summary>Grava o texto ou, se nulo, apaga o anterior (texto vazio).</summary>
    public static void GravarTextoOuApagar(Element elemento, DefinicaoDeParametro definicao, string? valor)
    {
        var parametro = Exigir(elemento, definicao);
        if (valor is null && string.IsNullOrEmpty(parametro.AsString())) return;
        if (!parametro.Set(valor ?? string.Empty)) throw Recusado(elemento, definicao);
    }

    private static Parameter Exigir(Element elemento, DefinicaoDeParametro definicao) =>
        Ler(elemento, definicao) ?? throw new InvalidOperationException(
            $"{definicao.Nome} não existe em {elemento.Id} ({elemento.Category?.Name}). Rode 'Injetar parâmetros'.");

    private static InvalidOperationException Recusado(Element elemento, DefinicaoDeParametro definicao) =>
        new($"O Revit recusou gravar {definicao.Nome} em {elemento.Id} ({elemento.Category?.Name}); o elemento pode estar em grupo ou vínculo.");

    private static DefinicaoDeParametro Definicao(string nome) =>
        CatalogoDeParametros.Padrao.Parametros.Single(parametro => parametro.Nome == nome);
}
