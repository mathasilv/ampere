using System.Text;

namespace Ampere.Core.Relatorios;

/// <summary>
///     CSV para o Excel em português: separador ';', linhas com CRLF e campo com ';', aspas ou quebra de linha entre
///     aspas (RFC 4180). A marca UTF-8 (BOM) fica com quem grava o arquivo.
/// </summary>
internal static class CsvEmPortugues
{
    public const char Separador = ';';

    public static void Linha(StringBuilder texto, IEnumerable<string?> campos)
    {
        texto.Append(string.Join(Separador, campos.Select(Campo)));
        texto.Append("\r\n");
    }

    private static string Campo(string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return string.Empty;
        return valor.IndexOfAny([Separador, '"', '\r', '\n']) >= 0 ? $"\"{valor.Replace("\"", "\"\"")}\"" : valor;
    }
}
