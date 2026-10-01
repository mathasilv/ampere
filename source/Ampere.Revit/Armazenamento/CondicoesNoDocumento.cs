using Autodesk.Revit.DB.ExtensibleStorage;

namespace Ampere.Revit.Armazenamento;

/// <summary>
///     Condições do projeto (JSON do Core) guardadas no documento por Extensible Storage, num <see cref="DataStorage" />
///     do Ampere — invisível no painel de propriedades, para não virar campo editável à mão.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item><c>DataStorage</c> próprio, não as Informações do projeto: num modelo compartilhado, elas costumam estar
///         emprestadas a quem edita o carimbo.</item>
///         <item>Acesso público de leitura e escrita: o acesso "Vendor" amarraria os dados ao VendorId do manifesto, que
///         muda ao publicar o add-in.</item>
///         <item>Elemento emprestado a outro usuário (modelo compartilhado): a gravação é pulada e o motivo volta para o
///         resumo — o dimensionamento não pode parar por isso.</item>
///     </list>
/// </remarks>
internal static class CondicoesNoDocumento
{
    // Congelado: dados já gravados nos projetos ficam presos a este GUID. Esquema novo = GUID novo + leitura do antigo.
    private static readonly Guid GuidDoEsquema = new("ee3927c5-6bcf-4343-8649-2dfa88c8b327");
    private const string NomeDoEsquema = "AmpereCondicoesDoProjeto";
    private const string Campo = "CondicoesJson";

    /// <summary>O JSON guardado, ou nulo se o documento não tem condições do Ampere.</summary>
    public static string? Ler(Document documento)
    {
        if (Schema.Lookup(GuidDoEsquema) is not { } esquema) return null;

        return Armazem(documento, esquema)?.GetEntity(esquema).Get<string>(Campo);
    }

    /// <summary>Grava o JSON (dentro de uma transação aberta); devolve o motivo se não pôde gravar, ou nulo.</summary>
    public static string? Gravar(Document documento, string json)
    {
        var esquema = Esquema();
        var armazem = Armazem(documento, esquema);
        if (armazem is not null && documento.IsWorkshared
                                && WorksharingUtils.GetCheckoutStatus(documento, armazem.Id, out var dono) == CheckoutStatus.OwnedByOtherUser)
            return $"o armazenamento do Ampere no modelo está emprestado a {dono}";

        var entidade = new Entity(esquema);
        entidade.Set(Campo, json);
        (armazem ?? DataStorage.Create(documento)).SetEntity(entidade);
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
