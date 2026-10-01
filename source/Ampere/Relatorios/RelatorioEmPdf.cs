namespace Ampere.Relatorios;

/// <summary>
///     Geração do PDF que não derruba o comando: o PDFsharp aceita um só resolvedor de fontes por processo, e se outro
///     add-in registrou o dele antes, todo PDF da sessão falha (AGENTS.md). O erro vai uma vez para o resumo e os
///     relatórios seguem sem PDF.
/// </summary>
internal static class RelatorioEmPdf
{
    /// <summary>Os bytes do PDF, ou <c>null</c> com o motivo acrescentado a <paramref name="erros" />.</summary>
    public static byte[]? Gerar(Func<byte[]> gerar, List<string> erros)
    {
        try
        {
            return gerar();
        }
        catch (InvalidOperationException excecao)
        {
            erros.Add($"PDF não gerado nesta sessão do Revit (JSON e Markdown foram gravados): {excecao.Message}");
            return null;
        }
    }
}
