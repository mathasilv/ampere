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
    private static readonly Guid GuidDoEsquema = new(CondicoesEmJson.GuidDoEsquema);
    private const string NomeDoEsquema = "AmpereCondicoesDoProjeto";
    private const string Campo = "CondicoesJson";

    /// <summary>O JSON das condições do projeto, ou nulo se o documento não as tem.</summary>
    public static string? Ler(Document documento) =>
        Schema.Lookup(GuidDoEsquema) is { } esquema ? Armazem(documento, esquema)?.GetEntity(esquema).Get<string>(Campo) : null;

    /// <summary>O JSON guardado no elemento (circuito), ou nulo se ele não tem.</summary>
    public static string? LerDo(Element elemento)
    {
        if (Schema.Lookup(GuidDoEsquema) is not { } esquema) return null;

        var entidade = elemento.GetEntity(esquema);
        return entidade.IsValid() ? entidade.Get<string>(Campo) : null;
    }

    /// <summary>Grava o JSON no elemento (dentro de uma transação aberta), se for diferente do guardado.</summary>
    public static void GravarEm(Element elemento, string json)
    {
        var esquema = Esquema();
        if (elemento.GetEntity(esquema) is { } atual && atual.IsValid() && atual.Get<string>(Campo) == json) return;

        elemento.SetEntity(Entidade(esquema, json));
    }

    /// <summary>Grava o JSON do projeto (dentro de uma transação aberta); devolve o motivo se não pôde gravar, ou nulo.</summary>
    public static string? Gravar(Document documento, string json)
    {
        var esquema = Esquema();
        var armazem = Armazem(documento, esquema);
        if (armazem is null)
        {
            DataStorage.Create(documento).SetEntity(Entidade(esquema, json));
            return null;
        }

        if (armazem.GetEntity(esquema).Get<string>(Campo) == json) return null;
        if (documento.IsWorkshared)
        {
            if (WorksharingUtils.GetCheckoutStatus(documento, armazem.Id, out var dono) == CheckoutStatus.OwnedByOtherUser)
                return $"o armazenamento do Ampere no modelo está emprestado a {dono}";
            if (WorksharingUtils.GetModelUpdatesStatus(documento, armazem.Id) is ModelUpdatesStatus.UpdatedInCentral or ModelUpdatesStatus.DeletedInCentral)
                return "o armazenamento do Ampere foi alterado no modelo central: sincronize (Recarregar o mais recente) e rode de novo";
        }

        armazem.SetEntity(Entidade(esquema, json));
        return null;
    }

    private static Entity Entidade(Schema esquema, string json)
    {
        var entidade = new Entity(esquema);
        entidade.Set(Campo, json);
        return entidade;
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

    private static Schema Esquema()
    {
        if (Schema.Lookup(GuidDoEsquema) is { } existente) return existente;

        var construtor = new SchemaBuilder(GuidDoEsquema);
        construtor.SetSchemaName(NomeDoEsquema);
        construtor.SetDocumentation("Ampere: condições do projeto usadas no dimensionamento (JSON versionado).");
        construtor.SetReadAccessLevel(AccessLevel.Public);
        construtor.SetWriteAccessLevel(AccessLevel.Public);
        construtor.AddSimpleField(Campo, typeof(string));
        return construtor.Finish();
    }
}
