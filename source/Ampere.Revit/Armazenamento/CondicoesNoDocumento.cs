using Ampere.Core.Dimensionamento;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace Ampere.Revit.Armazenamento;

/// <summary>
///     Condições do projeto (JSON do Core) guardadas por Extensible Storage: em cada circuito, as da rodada que o
///     dimensionou (para conferir a memória dele depois); num <see cref="DataStorage" /> do Ampere, as do projeto (da última
///     rodada completa, que abrem o diálogo). Invisível no painel de propriedades, para não virar campo editável à mão.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item><c>DataStorage</c> próprio, não as Informações do projeto: num modelo compartilhado, elas costumam estar
///         emprestadas a quem edita o carimbo.</item>
///         <item>Acesso público de leitura e escrita: o acesso "Vendor" amarraria os dados ao VendorId do manifesto, que
///         muda ao publicar o add-in.</item>
///         <item>O armazenamento do projeto é um só para todos os usuários de um modelo compartilhado: emprestado a outro,
///         ou alterado no central desde a última sincronização, a gravação é pulada e o motivo volta para o resumo — o
///         Revit recusaria o commit e desfaria os resultados do dimensionamento junto. Texto igual ao guardado não é
///         regravado (não pede o elemento à toa).</item>
///         <item>No circuito, as condições vão junto com os resultados que o comando já grava nele: nenhum elemento novo é
///         pedido ao central.</item>
///     </list>
/// </remarks>
internal static class CondicoesNoDocumento
{
    private static readonly EsquemaJson Esquema = new(new Guid(CondicoesEmJson.GuidDoEsquema), "AmpereCondicoesDoProjeto", "CondicoesJson",
        "Ampere: condições do projeto usadas no dimensionamento (JSON versionado).");

    /// <summary>O JSON das condições do projeto, ou nulo se o documento não as tem.</summary>
    public static string? Ler(Document documento) =>
        Esquema.Existente() is { } esquema && Armazem(documento, esquema) is { } armazem ? Esquema.LerDo(armazem) : null;

    /// <summary>O JSON guardado no elemento (circuito), ou nulo se ele não tem.</summary>
    public static string? LerDo(Element elemento) => Esquema.LerDo(elemento);

    /// <summary>Grava o JSON no elemento (dentro de uma transação aberta), se for diferente do guardado.</summary>
    public static void GravarEm(Element elemento, string json) => Esquema.GravarEm(elemento, json);

    /// <summary>Grava o JSON do projeto (dentro de uma transação aberta); devolve o motivo se não pôde gravar, ou nulo.</summary>
    public static string? Gravar(Document documento, string json)
    {
        var armazem = Armazem(documento, Esquema.Obter());
        if (armazem is null)
        {
            Esquema.GravarEm(DataStorage.Create(documento), json);
            return null;
        }

        if (Esquema.LerDo(armazem) == json) return null;
        if (documento.IsWorkshared)
        {
            if (WorksharingUtils.GetCheckoutStatus(documento, armazem.Id, out var dono) == CheckoutStatus.OwnedByOtherUser)
                return $"o armazenamento do Ampere no modelo está emprestado a {dono}";
            if (WorksharingUtils.GetModelUpdatesStatus(documento, armazem.Id) is ModelUpdatesStatus.UpdatedInCentral or ModelUpdatesStatus.DeletedInCentral)
                return "o armazenamento do Ampere foi alterado no modelo central: sincronize (Recarregar o mais recente) e rode de novo";
        }

        Esquema.GravarEm(armazem, json);
        return null;
    }

    // DataStorage não tem categoria: o filtro de classe é o filtro rápido possível aqui. O de menor Id vence, se dois
    // usuários de um modelo compartilhado criaram o seu antes de sincronizar.
    private static DataStorage? Armazem(Document documento, Schema esquema) =>
        new FilteredElementCollector(documento)
            .OfClass(typeof(DataStorage))
            .Cast<DataStorage>()
            .Where(armazem => armazem.GetEntity(esquema).IsValid())
            .OrderBy(armazem => armazem.Id.Value)
            .FirstOrDefault();
}
