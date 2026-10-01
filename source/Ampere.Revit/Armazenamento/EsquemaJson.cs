using Autodesk.Revit.DB.ExtensibleStorage;

namespace Ampere.Revit.Armazenamento;

/// <summary>
///     Esquema de Extensible Storage com um único campo de texto (JSON do Core), acesso público de leitura e escrita — o
///     acesso "Vendor" amarraria os dados ao VendorId do manifesto, que muda ao publicar o add-in.
/// </summary>
/// <param name="Guid">GUID congelado do esquema (vem do Core e é guardado por teste).</param>
internal sealed class EsquemaJson(Guid guid, string nome, string campo, string documentacao)
{
    /// <summary>O esquema, se já existe na sessão (nulo = nenhum documento aberto o usa: nada a ler).</summary>
    public Schema? Existente() => Schema.Lookup(guid);

    public Schema Obter()
    {
        if (Schema.Lookup(guid) is { } existente) return existente;

        var construtor = new SchemaBuilder(guid);
        construtor.SetSchemaName(nome);
        construtor.SetDocumentation(documentacao);
        construtor.SetReadAccessLevel(AccessLevel.Public);
        construtor.SetWriteAccessLevel(AccessLevel.Public);
        construtor.AddSimpleField(campo, typeof(string));
        return construtor.Finish();
    }

    /// <summary>O JSON guardado no elemento, ou nulo se ele não tem.</summary>
    public string? LerDo(Element elemento)
    {
        if (Existente() is not { } esquema) return null;

        var entidade = elemento.GetEntity(esquema);
        return entidade.IsValid() ? entidade.Get<string>(campo) : null;
    }

    /// <summary>Grava o JSON no elemento (dentro de uma transação aberta), se for diferente do guardado.</summary>
    public void GravarEm(Element elemento, string json)
    {
        var esquema = Obter();
        if (elemento.GetEntity(esquema) is { } atual && atual.IsValid() && atual.Get<string>(campo) == json) return;

        var entidade = new Entity(esquema);
        entidade.Set(campo, json);
        elemento.SetEntity(entidade);
    }

    /// <summary>O JSON do projeto, guardado num <see cref="DataStorage" /> do Ampere com este esquema, ou nulo se não há.</summary>
    public string? LerDoProjeto(Document documento) =>
        Existente() is { } esquema && Armazem(documento, esquema) is { } armazem ? LerDo(armazem) : null;

    /// <summary>
    ///     Grava o JSON do projeto (dentro de uma transação aberta) no <see cref="DataStorage" /> deste esquema, criado na
    ///     primeira vez; devolve o motivo se não pôde gravar, ou nulo.
    /// </summary>
    /// <remarks>
    ///     <c>DataStorage</c> próprio, não as Informações do projeto: num modelo compartilhado, elas costumam estar emprestadas
    ///     a quem edita o carimbo. Emprestado a outro, ou alterado no central desde a última sincronização, a gravação é
    ///     pulada e o motivo volta para o resumo — o Revit recusaria o commit e desfaria o resto da transação junto. Texto
    ///     igual ao guardado não é regravado (não pede o elemento à toa).
    /// </remarks>
    public string? GravarNoProjeto(Document documento, string json)
    {
        var armazem = Armazem(documento, Obter());
        if (armazem is null)
        {
            GravarEm(DataStorage.Create(documento), json);
            return null;
        }

        if (LerDo(armazem) == json) return null;
        if (documento.IsWorkshared)
        {
            if (WorksharingUtils.GetCheckoutStatus(documento, armazem.Id, out var dono) == CheckoutStatus.OwnedByOtherUser)
                return $"o armazenamento do Ampere no modelo está emprestado a {dono}";
            if (WorksharingUtils.GetModelUpdatesStatus(documento, armazem.Id) is ModelUpdatesStatus.UpdatedInCentral or ModelUpdatesStatus.DeletedInCentral)
                return "o armazenamento do Ampere foi alterado no modelo central: sincronize (Recarregar o mais recente) e rode de novo";
        }

        GravarEm(armazem, json);
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
