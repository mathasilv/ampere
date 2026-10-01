using System.IO;
using System.Text;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Relatorios;
using Ampere.Relatorios;

namespace Ampere.Dimensionamento;

/// <summary>
///     Grava em Documentos\Ampere\{projeto}\Circuitos a planilha de todos os circuitos (circuitos.csv, refeita a cada
///     rodada) e a memória (JSON), o relatório (Markdown) e o PDF de cada circuito com memória, com o quadro, o número e o
///     início do hash no nome. Falha de gravação não derruba o comando — o dimensionamento já está no modelo; volta como
///     erro para o resumo. PDF indisponível (fontes do PDFsharp tomadas por outro add-in) não impede JSON e Markdown.
/// </summary>
internal static class RelatoriosDosCircuitos
{
    public const string Subpasta = "Circuitos";
    public const string NomeDaPlanilha = "circuitos.csv";

    /// <summary>A pasta (nula se nada foi gravado), quantas memórias saíram, a planilha (nula se não foi gravada) e os erros.</summary>
    public static (string? Pasta, int Gerados, string? Planilha, IReadOnlyList<string> Erros) Gravar(
        IReadOnlyList<ResultadoDoCircuito> resultados, string nomeDoProjeto)
    {
        if (resultados.Count == 0) return (null, 0, null, []);

        var pasta = PastaDeRelatorios.Caminho(nomeDoProjeto, Subpasta);
        try
        {
            Directory.CreateDirectory(pasta);
        }
        catch (Exception excecao) when (excecao is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return (null, 0, null, [$"não foi possível criar a pasta {pasta}: {excecao.Message}"]);
        }

        var erros = new List<string>();
        string? planilha = Path.Combine(pasta, NomeDaPlanilha);
        try
        {
            // Com BOM: o Excel só reconhece os acentos de um CSV em UTF-8 se ele começar com a marca.
            File.WriteAllText(planilha, PlanilhaDeCircuitos.Csv(resultados), new UTF8Encoding(true));
        }
        catch (Exception excecao) when (excecao is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            erros.Add($"planilha {NomeDaPlanilha} (aberta no Excel?): {excecao.Message}");
            planilha = null;
        }

        var comMemoria = resultados.Where(resultado => resultado.Memoria is not null).ToList();
        var gerados = 0;
        var pdfDisponivel = true;
        foreach (var resultado in comMemoria)
        {
            var memoria = resultado.Memoria!;
            var hash = memoria.Hash();
            var identificacao = string.IsNullOrWhiteSpace(resultado.Quadro) ? memoria.Circuito : $"{resultado.Quadro}-{memoria.Circuito}";
            var baseNome = $"{PastaDeRelatorios.NomeDeArquivo(identificacao)}-{PastaDeRelatorios.Prefixo(hash)}";
            var pdf = pdfDisponivel ? RelatorioEmPdf.Gerar(() => RelatorioDeMemoria.Pdf(memoria, hash), erros) : null;
            pdfDisponivel = pdf is not null;
            try
            {
                File.WriteAllText(Path.Combine(pasta, baseNome + ".json"), memoria.JsonCanonico(), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(pasta, baseNome + ".md"), RelatorioDeMemoria.Markdown(memoria, hash), new UTF8Encoding(false));
                if (pdf is not null) File.WriteAllBytes(Path.Combine(pasta, baseNome + ".pdf"), pdf);
                gerados++;
            }
            catch (Exception excecao) when (excecao is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                erros.Add($"circuito {identificacao}: {excecao.Message}");
            }
        }

        return (gerados > 0 || planilha is not null ? pasta : null, gerados, planilha, erros);
    }
}
