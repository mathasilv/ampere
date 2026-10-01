using System.IO;
using System.Text;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Relatorios;
using Ampere.Relatorios;

namespace Ampere.Dimensionamento;

/// <summary>
///     Grava em Documentos\Ampere\{projeto}\Circuitos a planilha dos circuitos da rodada, a lista de materiais e a memória
///     (JSON), o relatório (Markdown) e o PDF de cada circuito com memória, com o quadro, o número e o início do hash no
///     nome. As planilhas do projeto todo (circuitos.csv, materiais.csv) são refeitas a cada rodada completa; a rodada só
///     da seleção grava as suas à parte (-selecao), para não trocar as do projeto por um pedaço delas. Falha de gravação não derruba o comando — o dimensionamento já está no modelo; volta como
///     erro para o resumo. PDF indisponível (fontes do PDFsharp tomadas por outro add-in) não impede JSON e Markdown.
/// </summary>
internal static class RelatoriosDosCircuitos
{
    public const string Subpasta = "Circuitos";
    public const string NomeDaPlanilha = "circuitos.csv";
    public const string NomeDaPlanilhaDaSelecao = "circuitos-selecao.csv";
    public const string NomeDosMateriais = "materiais.csv";
    public const string NomeDosMateriaisDaSelecao = "materiais-selecao.csv";

    public static GravacaoDosRelatorios Gravar(IReadOnlyList<ResultadoDoCircuito> resultados, string nomeDoProjeto, bool daSelecao)
    {
        var nomeDaPlanilha = daSelecao ? NomeDaPlanilhaDaSelecao : NomeDaPlanilha;
        var nomeDosMateriais = daSelecao ? NomeDosMateriaisDaSelecao : NomeDosMateriais;
        if (resultados.Count == 0) return new GravacaoDosRelatorios(null, 0, null, null, []);

        var pasta = PastaDeRelatorios.Caminho(nomeDoProjeto, Subpasta);
        try
        {
            Directory.CreateDirectory(pasta);
        }
        catch (Exception excecao) when (excecao is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return new GravacaoDosRelatorios(null, 0, null, null, [$"não foi possível criar a pasta {pasta}: {excecao.Message}"]);
        }

        var erros = new List<string>();
        var planilha = GravarCsv(pasta, nomeDaPlanilha, PlanilhaDeCircuitos.Csv(resultados), erros);
        var lista = ListaDeMateriais.Montar(resultados);
        var materiais = GravarCsv(pasta, nomeDosMateriais, lista.Csv(), erros);

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

        return new GravacaoDosRelatorios(gerados > 0 || planilha is not null || materiais is not null ? pasta : null, gerados, planilha,
            materiais is null ? null : new ArquivoDeMateriais(materiais, lista.Itens.Count, lista.ForaDaLista.Count), erros);
    }

    // Com BOM: o Excel só reconhece os acentos de um CSV em UTF-8 se ele começar com a marca.
    private static string? GravarCsv(string pasta, string nome, string conteudo, List<string> erros)
    {
        var caminho = Path.Combine(pasta, nome);
        try
        {
            File.WriteAllText(caminho, conteudo, new UTF8Encoding(true));
            return caminho;
        }
        catch (Exception excecao) when (excecao is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            erros.Add($"planilha {nome} (aberta no Excel?): {excecao.Message}");
            return null;
        }
    }
}

/// <summary>A lista de materiais gravada: o arquivo, quantos itens e quantos circuitos ficaram fora.</summary>
internal sealed record ArquivoDeMateriais(string Caminho, int Itens, int CircuitosFora);

/// <summary>O que foi gravado em disco: a pasta (nula se nada foi gravado), as memórias, as planilhas (nulas se não gravadas) e os erros.</summary>
internal sealed record GravacaoDosRelatorios(string? Pasta, int Gerados, string? Planilha, ArquivoDeMateriais? Materiais, IReadOnlyList<string> Erros);
