using System.Text;
using System.IO;
using Ampere.Core.Quadros;
using Ampere.Core.Relatorios;
using Ampere.Relatorios;

namespace Ampere.Quadros;

/// <summary>
///     Grava os relatórios dos quadros montados: para cada quadro completo, a memória (JSON), o relatório (Markdown)
///     e o PDF numa pasta de saída. Falha de gravação não derruba o comando — volta como erro para o resumo.
/// </summary>
internal static class RelatoriosDosQuadros
{
    /// <summary>Grava os relatórios em Documentos\Ampere\{projeto}. Retorna a pasta, quantos quadros saíram e os erros.</summary>
    public static (string? Pasta, int Gerados, IReadOnlyList<string> Erros) Gravar(IReadOnlyList<ResultadoDoQuadro> resultados, string nomeDoProjeto)
    {
        var pasta = PastaDeRelatorios.Caminho(nomeDoProjeto);
        var erros = new List<string>();
        var gerados = 0;
        try
        {
            Directory.CreateDirectory(pasta);
            foreach (var resultado in resultados.Where(resultado => resultado.Quadro.Memoria is not null))
            {
                var memoria = resultado.Quadro.Memoria!;
                var baseNome = $"{PastaDeRelatorios.NomeDeArquivo(resultado.Nome)}-{PastaDeRelatorios.Prefixo(memoria.Hash())}";
                try
                {
                    File.WriteAllText(Path.Combine(pasta, baseNome + ".json"), memoria.JsonCanonico(), new UTF8Encoding(false));
                    File.WriteAllText(Path.Combine(pasta, baseNome + ".md"), RelatorioDeMemoria.MarkdownDoQuadro(resultado.Quadro), new UTF8Encoding(false));
                    File.WriteAllBytes(Path.Combine(pasta, baseNome + ".pdf"), RelatorioDeMemoria.PdfDoQuadro(resultado.Quadro));
                    gerados++;
                }
                catch (Exception excecao) when (excecao is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    erros.Add($"quadro {resultado.Nome}: {excecao.Message}");
                }
            }
        }
        catch (Exception excecao) when (excecao is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return (null, 0, [$"não foi possível criar a pasta {pasta}: {excecao.Message}"]);
        }

        return (gerados > 0 ? pasta : null, gerados, erros);
    }
}
