using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Ampere.Core.Memoria;

/// <summary>Valor usado num passo, com a unidade.</summary>
public sealed record ValorDoPasso(string Nome, decimal Valor, string Unidade);

/// <summary>
///     Uma linha da memória de cálculo: o que foi calculado, com qual expressão e valores, e o item da norma que o
///     justifica (ou TODO_NORMA).
/// </summary>
/// <param name="Referencia">Item da norma ou TODO_NORMA — obrigatório em toda linha.</param>
/// <param name="Descricao">O que o passo determina (ex.: "Corrente de projeto").</param>
/// <param name="Expressao">Expressão aplicada (ex.: "IB = S / V").</param>
/// <param name="Valores">Valores usados, na ordem da expressão.</param>
/// <param name="Resultado">Resultado; <c>null</c> quando o passo registra que um dado falta e o cálculo parou.</param>
/// <param name="Unidade">Unidade do resultado.</param>
/// <param name="Observacao">Explicação adicional (ex.: por que o cálculo parou).</param>
public sealed record PassoDeCalculo(
    string Referencia,
    string Descricao,
    string Expressao,
    IReadOnlyList<ValorDoPasso> Valores,
    decimal? Resultado,
    string Unidade,
    string? Observacao = null);

/// <summary>
///     Memória de cálculo auditável de um circuito, com hash determinístico: mesmas entradas, mesmo JSON canônico,
///     mesmo hash — em qualquer máquina e runtime.
/// </summary>
/// <remarks>
///     JSON canônico: campos em ordem fixa, sem espaços, números normalizados (10 e 10,00 escrevem "10"), textos em
///     Unicode NFC e escape ASCII. Nada de data/hora nem aleatoriedade (regra 6 do AGENTS.md).
/// </remarks>
public sealed class MemoriaDeCalculo
{
    /// <summary>Versão do esquema do JSON; muda só com migração explícita das memórias existentes.</summary>
    public const string VersaoDoEsquema = "1";

    private static readonly JsonWriterOptions Opcoes = new() { Indented = false, Encoder = JavaScriptEncoder.Default };

    /// <exception cref="MemoriaDeCalculoInvalidaException">Com todos os problemas encontrados.</exception>
    public MemoriaDeCalculo(string circuito, string perfilNorma, IReadOnlyList<PassoDeCalculo> passos)
    {
        var problemas = new List<string>();
        if (string.IsNullOrWhiteSpace(circuito)) problemas.Add("circuito sem nome");
        if (string.IsNullOrWhiteSpace(perfilNorma)) problemas.Add("perfil normativo sem nome");
        if (passos.Count == 0) problemas.Add("memória sem passos");
        for (var indice = 0; indice < passos.Count; indice++)
        {
            var passo = passos[indice];
            var numero = indice + 1;
            if (string.IsNullOrWhiteSpace(passo.Referencia)) problemas.Add($"passo {numero} sem referência (item da norma ou TODO_NORMA)");
            if (string.IsNullOrWhiteSpace(passo.Expressao)) problemas.Add($"passo {numero} sem expressão");
            foreach (var repetido in passo.Valores.GroupBy(valor => valor.Nome, StringComparer.Ordinal).Where(grupo => grupo.Count() > 1))
                problemas.Add($"passo {numero}: valor '{repetido.Key}' repetido");
        }

        if (problemas.Count > 0) throw new MemoriaDeCalculoInvalidaException(problemas);

        Circuito = circuito;
        PerfilNorma = perfilNorma;
        Passos = passos;
    }

    public string Circuito { get; }

    /// <summary>Perfil normativo usado (gravado em AMP_PerfilNorma).</summary>
    public string PerfilNorma { get; }

    public IReadOnlyList<PassoDeCalculo> Passos { get; }

    /// <summary>Forma canônica usada no hash.</summary>
    public string JsonCanonico()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var escritor = new Utf8JsonWriter(buffer, Opcoes))
        {
            escritor.WriteStartObject();
            escritor.WriteString("esquema", VersaoDoEsquema);
            escritor.WriteString("circuito", Texto(Circuito));
            escritor.WriteString("perfilNorma", Texto(PerfilNorma));
            escritor.WriteStartArray("passos");
            foreach (var passo in Passos)
            {
                escritor.WriteStartObject();
                escritor.WriteString("ref", Texto(passo.Referencia));
                escritor.WriteString("descricao", Texto(passo.Descricao));
                escritor.WriteString("expr", Texto(passo.Expressao));
                escritor.WriteStartArray("valores");
                foreach (var valor in passo.Valores)
                {
                    escritor.WriteStartObject();
                    escritor.WriteString("nome", Texto(valor.Nome));
                    escritor.WritePropertyName("valor");
                    escritor.WriteRawValue(Numero(valor.Valor));
                    escritor.WriteString("unidade", Texto(valor.Unidade));
                    escritor.WriteEndObject();
                }

                escritor.WriteEndArray();
                escritor.WritePropertyName("resultado");
                if (passo.Resultado is { } resultado) escritor.WriteRawValue(Numero(resultado));
                else escritor.WriteNullValue();
                escritor.WriteString("unidade", Texto(passo.Unidade));
                if (passo.Observacao is not null) escritor.WriteString("obs", Texto(passo.Observacao));
                escritor.WriteEndObject();
            }

            escritor.WriteEndArray();
            escritor.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>"sha256:" + hash hexadecimal minúsculo do JSON canônico (gravado em AMP_MemoriaCalculoId).</summary>
    public string Hash() => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonCanonico()))).ToLowerInvariant();

    private static string Texto(string texto) => texto.Normalize(NormalizationForm.FormC);

    private static string Numero(decimal valor) =>
        valor == 0m ? "0" : valor.ToString("0.############################", CultureInfo.InvariantCulture);
}

/// <summary>A memória viola alguma regra (ex.: passo sem referência). Traz todos os problemas.</summary>
public sealed class MemoriaDeCalculoInvalidaException(IReadOnlyList<string> problemas)
    : Exception("Memória de cálculo inválida:\n" + string.Join("\n", problemas.Select(problema => "- " + problema)))
{
    public IReadOnlyList<string> Problemas { get; } = problemas;
}
