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
}
