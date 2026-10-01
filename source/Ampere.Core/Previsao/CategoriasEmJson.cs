using System.Text;
using System.Text.Json;

namespace Ampere.Core.Previsao;

/// <summary>
///     Categoria de cômodo escolhida para cada nome de ambiente, em JSON, para ficar guardada no modelo: quem abrir o
///     projeto depois refaz a mesma previsão sem escolher tudo de novo.
/// </summary>
/// <remarks>
///     Texto canônico (nomes em ordem ordinal, sem espaços, categoria vazia omitida) com <c>versao</c>. Leitura tolerante:
///     texto vazio, inválido ou de versão desconhecida devolve nulo — o diálogo abre sem as escolhas, nunca com escolhas
///     adivinhadas.
/// </remarks>
public static class CategoriasEmJson
{
    /// <summary>Versão do formato; mudança incompatível = versão nova, com leitura da anterior.</summary>
    public const int Versao = 1;

    /// <summary>
    ///     GUID do esquema de Extensible Storage em que o adapter guarda este JSON. Congelado (AGENTS.md): os projetos
    ///     guardam dados presos a ele; esquema novo = GUID novo, com leitura do antigo.
    /// </summary>
    public const string GuidDoEsquema = "cda48f7f-9288-488d-bbca-997e5456bc77";

    public static string Escrever(IReadOnlyDictionary<string, string> categoriaPorNome)
    {
        using var fluxo = new MemoryStream();
        using (var escritor = new Utf8JsonWriter(fluxo))
        {
            escritor.WriteStartObject();
            escritor.WriteNumber("versao", Versao);
            escritor.WriteStartObject("categorias");
            foreach (var (nome, categoria) in categoriaPorNome
                         .Where(par => !string.IsNullOrWhiteSpace(par.Key) && !string.IsNullOrWhiteSpace(par.Value))
                         .Select(par => (Nome: par.Key.Trim(), Categoria: par.Value.Trim()))
                         .OrderBy(par => par.Nome, StringComparer.Ordinal)
                         // O nome do ambiente vale sem diferença de maiúsculas: o primeiro, na ordem ordinal, fica.
                         .DistinctBy(par => par.Nome, StringComparer.OrdinalIgnoreCase))
                escritor.WriteString(nome, categoria);
            escritor.WriteEndObject();
            escritor.WriteEndObject();
        }

        return Encoding.UTF8.GetString(fluxo.ToArray());
    }

    public static IReadOnlyDictionary<string, string>? Ler(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var documento = JsonDocument.Parse(json);
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object) return null;
            if (!raiz.TryGetProperty("versao", out var versao) || versao.ValueKind != JsonValueKind.Number || !versao.TryGetInt32(out var numero) || numero != Versao)
                return null;
            if (!raiz.TryGetProperty("categorias", out var categorias) || categorias.ValueKind != JsonValueKind.Object) return null;

            var resultado = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var propriedade in categorias.EnumerateObject())
            {
                if (propriedade.Value.ValueKind != JsonValueKind.String) return null;
                if (propriedade.Value.GetString() is { } categoria && !string.IsNullOrWhiteSpace(categoria) && !string.IsNullOrWhiteSpace(propriedade.Name))
                    resultado[propriedade.Name.Trim()] = categoria.Trim();
            }

            return resultado;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
