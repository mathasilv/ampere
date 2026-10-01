using System.Text;
using System.Text.Json;

namespace Ampere.Core.Dimensionamento;

/// <summary>
///     Condições do projeto em JSON, para ficarem guardadas no modelo junto com os resultados do dimensionamento: quem
///     abrir o projeto depois (outro projetista, outra máquina) reproduz a mesma memória.
/// </summary>
/// <remarks>
///     Texto canônico (campos em ordem fixa, sem espaços, opcionais ausentes omitidos) com <c>versao</c>. Leitura
///     tolerante: texto vazio, inválido, de versão desconhecida ou com valor fora da faixa devolve nulo — o diálogo abre
///     sem as condições salvas, nunca com valores adivinhados.
/// </remarks>
public static class CondicoesEmJson
{
    /// <summary>Versão do formato; mudança incompatível = versão nova, com leitura da anterior.</summary>
    public const int Versao = 1;

    /// <summary>
    ///     GUID do esquema de Extensible Storage em que o adapter guarda este JSON. Congelado (AGENTS.md): os projetos
    ///     guardam dados presos a ele; esquema novo = GUID novo, com leitura do antigo.
    /// </summary>
    public const string GuidDoEsquema = "ee3927c5-6bcf-4343-8649-2dfa88c8b327";

    public static string Escrever(CondicoesDoProjeto condicoes)
    {
        using var fluxo = new MemoryStream();
        using (var escritor = new Utf8JsonWriter(fluxo))
        {
            escritor.WriteStartObject();
            escritor.WriteNumber("versao", Versao);
            escritor.WriteNumber("temperatura_ambiente_c", condicoes.TemperaturaAmbienteC);
            if (condicoes.TemperaturaDoSoloC is { } solo) escritor.WriteNumber("temperatura_do_solo_c", solo);
            escritor.WriteNumber("circuitos_agrupados", condicoes.CircuitosAgrupados);
            if (condicoes.CircuitosAgrupadosNoSolo is { } noSolo) escritor.WriteNumber("circuitos_agrupados_no_solo", noSolo);
            escritor.WriteString("material", condicoes.Material);
            Opcional(escritor, "metodo_de_instalacao_padrao", condicoes.MetodoDeInstalacaoPadrao);
            Opcional(escritor, "isolacao_padrao", condicoes.IsolacaoPadrao);
            Opcional(escritor, "tipo_de_condutor_padrao", condicoes.TipoDeCondutorPadrao);
            Opcional(escritor, "tipo_de_eletroduto", condicoes.TipoDeEletroduto);
            escritor.WriteEndObject();
        }

        return Encoding.UTF8.GetString(fluxo.ToArray());
    }

    public static CondicoesDoProjeto? Ler(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var documento = JsonDocument.Parse(json);
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object) return null;
            if (!raiz.TryGetProperty("versao", out var versao) || versao.ValueKind != JsonValueKind.Number || !versao.TryGetInt32(out var numero) || numero != Versao)
                return null;

            if (!Numero(raiz, "temperatura_ambiente_c", out var temperatura)) return null;
            if (!Numero(raiz, "circuitos_agrupados", out var agrupados) || agrupados < 1 || agrupados > int.MaxValue || agrupados != decimal.Truncate(agrupados))
                return null;
            if (Texto(raiz, "material") is not { } material) return null;
            // Opcional, acrescentado sem mudar a versão: quem não o conhece o ignora; presente, precisa ser número.
            decimal? solo = null;
            if (raiz.TryGetProperty("temperatura_do_solo_c", out _))
            {
                if (!Numero(raiz, "temperatura_do_solo_c", out var lida)) return null;
                solo = lida;
            }

            int? agrupadosNoSolo = null;
            if (raiz.TryGetProperty("circuitos_agrupados_no_solo", out _))
            {
                if (!Numero(raiz, "circuitos_agrupados_no_solo", out var lidos) || lidos < 1 || lidos > int.MaxValue || lidos != decimal.Truncate(lidos)) return null;
                agrupadosNoSolo = (int)lidos;
            }

            return new CondicoesDoProjeto(
                temperatura,
                (int)agrupados,
                material,
                Texto(raiz, "metodo_de_instalacao_padrao"),
                Texto(raiz, "isolacao_padrao"),
                Texto(raiz, "tipo_de_condutor_padrao"),
                Texto(raiz, "tipo_de_eletroduto"),
                solo,
                agrupadosNoSolo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void Opcional(Utf8JsonWriter escritor, string nome, string? valor)
    {
        if (!string.IsNullOrWhiteSpace(valor)) escritor.WriteString(nome, valor);
    }

    private static bool Numero(JsonElement raiz, string nome, out decimal valor)
    {
        valor = 0m;
        return raiz.TryGetProperty(nome, out var elemento) && elemento.ValueKind == JsonValueKind.Number && elemento.TryGetDecimal(out valor);
    }

    // Texto vazio conta como ausente, como nos parâmetros AMP_*.
    private static string? Texto(JsonElement raiz, string nome) =>
        raiz.TryGetProperty(nome, out var elemento) && elemento.ValueKind == JsonValueKind.String && elemento.GetString() is { } texto && !string.IsNullOrWhiteSpace(texto)
            ? texto
            : null;
}
