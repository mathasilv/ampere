using System.Text;
using System.Text.Json;
using Ampere.Core.Cargas;

namespace Ampere.Core.Quadros;

/// <summary>
///     Fatores de demanda informados na montagem do quadro de cargas, em JSON, para ficarem guardados no quadro junto com
///     o hash da memória: quem refaz o quadro depois (o dimensionamento do alimentador) usa os mesmos e confere o hash.
/// </summary>
/// <remarks>
///     Texto canônico (tipos na ordem do enum, sem espaços) com <c>versao</c>. Leitura tolerante: texto vazio, inválido,
///     de versão desconhecida, com tipo desconhecido ou fator fora de (0, 1] devolve nulo — nunca um fator adivinhado.
/// </remarks>
public static class FatoresEmJson
{
    public const int Versao = 1;

    /// <summary>
    ///     GUID do esquema de Extensible Storage em que o adapter guarda este JSON no quadro. Congelado (AGENTS.md): os
    ///     projetos guardam dados presos a ele.
    /// </summary>
    public const string GuidDoEsquema = "7957990d-8c18-437e-ba7e-e508a8c9f418";

    public static string Escrever(IReadOnlyDictionary<TipoDeCarga, decimal>? fatores)
    {
        using var fluxo = new MemoryStream();
        using (var escritor = new Utf8JsonWriter(fluxo))
        {
            escritor.WriteStartObject();
            escritor.WriteNumber("versao", Versao);
            escritor.WriteStartObject("fatores");
            foreach (var (tipo, fator) in (fatores ?? new Dictionary<TipoDeCarga, decimal>()).OrderBy(par => par.Key))
                escritor.WriteNumber(tipo.ToString(), fator);
            escritor.WriteEndObject();
            escritor.WriteEndObject();
        }

        return Encoding.UTF8.GetString(fluxo.ToArray());
    }

    public static IReadOnlyDictionary<TipoDeCarga, decimal>? Ler(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var documento = JsonDocument.Parse(json);
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object
                || !raiz.TryGetProperty("versao", out var versao) || versao.ValueKind != JsonValueKind.Number || !versao.TryGetInt32(out var numero) || numero != Versao
                || !raiz.TryGetProperty("fatores", out var lista) || lista.ValueKind != JsonValueKind.Object)
                return null;

            var fatores = new Dictionary<TipoDeCarga, decimal>();
            foreach (var propriedade in lista.EnumerateObject())
            {
                if (!Enum.TryParse<TipoDeCarga>(propriedade.Name, ignoreCase: false, out var tipo) || !Enum.IsDefined(tipo)
                    || propriedade.Value.ValueKind != JsonValueKind.Number || !propriedade.Value.TryGetDecimal(out var fator) || fator <= 0m || fator > 1m)
                    return null;
                fatores[tipo] = fator;
            }

            return fatores;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
