using System.IO;
using System.Text;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Relatorios;
using Ampere.Relatorios;

namespace Ampere.Dimensionamento;

/// <summary>
///     Grava a memória (JSON), o relatório (Markdown) e o PDF de cada circuito com memória em
///     Documentos\Ampere\{projeto}\Circuitos, com o quadro, o número e o início do hash no nome. Falha de gravação não
///     derruba o comando — volta como erro para o resumo.
/// </summary>
internal static class RelatoriosDosCircuitos
{
    public const string Subpasta = "Circuitos";

    /// <summary>Retorna a pasta (nula se nada foi gravado), quantos circuitos saíram e os erros.</summary>
    public static (string? Pasta, int Gerados, IReadOnlyList<string> Erros) Gravar(IReadOnlyList<ResultadoDoCircuito> resultados, string nomeDoProjeto)
    {
        var comMemoria = resultados.Where(resultado => resultado.Memoria is not null).ToList();
        if (comMemoria.Count == 0) return (null, 0, []);

        var pasta = PastaDeRelatorios.Caminho(nomeDoProjeto, Subpasta);
        try
        {
            Directory.CreateDirectory(pasta);
        }
        catch (Exception excecao) when (excecao is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return (null, 0, [$"não foi possível criar a pasta {pasta}: {excecao.Message}"]);
        }

        var erros = new List<string>();
        var gerados = 0;
        foreach (var resultado in comMemoria)
        {
            var memoria = resultado.Memoria!;
            var hash = memoria.Hash();
            var identificacao = string.IsNullOrWhiteSpace(resultado.Quadro) ? memoria.Circuito : $"{resultado.Quadro}-{memoria.Circuito}";
            var baseNome = $"{PastaDeRelatorios.NomeDeArquivo(identificacao)}-{PastaDeRelatorios.Prefixo(hash)}";
            try
            {
                File.WriteAllText(Path.Combine(pasta, baseNome + ".json"), memoria.JsonCanonico(), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(pasta, baseNome + ".md"), RelatorioDeMemoria.Markdown(memoria, hash), new UTF8Encoding(false));
                File.WriteAllBytes(Path.Combine(pasta, baseNome + ".pdf"), RelatorioDeMemoria.Pdf(memoria, hash));
                gerados++;
            }
            catch (Exception excecao) when (excecao is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                erros.Add($"circuito {identificacao}: {excecao.Message}");
            }
        }

        return (gerados > 0 ? pasta : null, gerados, erros);
    }
}
