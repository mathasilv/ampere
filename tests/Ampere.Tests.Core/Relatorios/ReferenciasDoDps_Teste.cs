using System.Runtime.CompilerServices;
using System.Text;
using Ampere.Core.Relatorios;
using Ampere.Core.Relatorios.Pdf;
using Ampere.Core.Surtos;
using Ampere.Tests.Core.Surtos;

namespace Ampere.Tests.Core.Relatorios;

/// <summary>
///     Golden files dos DPS do quadro (o caso TT de <see cref="SelecaoDeDps_Teste" />): memória (JSON), relatório (Markdown)
///     e roteiro do PDF. Mesmo fluxo de <see cref="ReferenciasDeMemoria_Teste" />: para regenerar,
///     AMPERE_ATUALIZAR_REFERENCIAS=1 e revise o diff antes do commit.
/// </summary>
[Property("Fonte", "NBR 5410:2004, 6.3.5.2 e Tabelas 31 e 49")]
public class ReferenciasDoDps_Teste
{
    private const string VariavelDeAtualizacao = "AMPERE_ATUALIZAR_REFERENCIAS";
    private const string VariavelDeAmostras = "AMPERE_AMOSTRAS_PDF";

    [Test]
    public async Task Memoria_e_relatorio_dos_dps_iguais_aos_de_referencia()
    {
        const string Cenario = "dps-quadro-tt";
        var resultado = SelecaoDeDps_Teste.DoQuadroTt();
        var norma = NormaDeDps.NBR5410_2004;

        await Conferir($"{Cenario}.json", resultado.Memoria!.JsonCanonico());
        await Conferir($"{Cenario}.md", RelatorioDeMemoria.MarkdownDoDps(resultado, norma));
        await Conferir($"{Cenario}.pdf.txt", PdfDoRelatorio.Roteiro(ConteudoDoRelatorio.DeDps(resultado, norma)));

        if (Environment.GetEnvironmentVariable(VariavelDeAmostras) is { Length: > 0 } pasta)
            await File.WriteAllBytesAsync(Path.Combine(pasta, $"{Cenario}.pdf"), RelatorioDeMemoria.PdfDoDps(resultado, norma));
    }

    private static async Task Conferir(string arquivo, string atual)
    {
        var caminho = Path.Combine(PastaDasReferencias(), arquivo);
        if (Environment.GetEnvironmentVariable(VariavelDeAtualizacao) == "1")
        {
            await File.WriteAllTextAsync(caminho, atual, new UTF8Encoding(false));
            return;
        }

        await Assert.That(File.Exists(caminho)).IsTrue()
            .Because($"referência {arquivo} ausente: rode os testes com {VariavelDeAtualizacao}=1 e revise o arquivo gerado");
        await Assert.That(atual).IsEqualTo(await File.ReadAllTextAsync(caminho, new UTF8Encoding(false)))
            .Because($"{arquivo} mudou: se a mudança tem justificativa, regenere com {VariavelDeAtualizacao}=1 e revise o diff");
    }

    private static string PastaDasReferencias([CallerFilePath] string arquivoDoTeste = "")
    {
        var pasta = Path.Combine(Path.GetDirectoryName(arquivoDoTeste)!, "Referencias");
        if (!Directory.Exists(pasta)) throw new DirectoryNotFoundException($"Pasta de referências não encontrada: {pasta}");
        return pasta;
    }
}
