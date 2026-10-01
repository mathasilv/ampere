using System.Runtime.CompilerServices;
using System.Text;
using Ampere.Core.Demanda;
using Ampere.Core.Relatorios;
using Ampere.Core.Relatorios.Pdf;
using Ampere.Tests.Core.Demanda;

namespace Ampere.Tests.Core.Relatorios;

/// <summary>
///     Golden files da demanda da entrada (a casa de <see cref="DemandaDaEntrada_Teste" />): memória (JSON), relatório
///     (Markdown) e roteiro do PDF. Mesmo fluxo de <see cref="ReferenciasDeMemoria_Teste" />: para regenerar,
///     AMPERE_ATUALIZAR_REFERENCIAS=1 e revise o diff antes do commit.
/// </summary>
[Property("Fonte", "CELG CT 04/18")]
public class ReferenciasDaDemanda_Teste
{
    private const string VariavelDeAtualizacao = "AMPERE_ATUALIZAR_REFERENCIAS";
    private const string VariavelDeAmostras = "AMPERE_AMOSTRAS_PDF";

    [Test]
    public async Task Memoria_e_relatorio_da_demanda_iguais_aos_de_referencia()
    {
        const string Cenario = "demanda-casa-celg";
        var resultado = DemandaDaEntrada_Teste.DaCasa();
        var perfil = PerfilDeDemanda.CelgCt04_18;

        await Conferir($"{Cenario}.json", resultado.Memoria!.JsonCanonico());
        await Conferir($"{Cenario}.md", RelatorioDeMemoria.MarkdownDaDemanda(resultado, perfil));
        await Conferir($"{Cenario}.pdf.txt", PdfDoRelatorio.Roteiro(ConteudoDoRelatorio.DeDemanda(resultado, perfil)));

        if (Environment.GetEnvironmentVariable(VariavelDeAmostras) is { Length: > 0 } pasta)
            await File.WriteAllBytesAsync(Path.Combine(pasta, $"{Cenario}.pdf"), RelatorioDeMemoria.PdfDaDemanda(resultado, perfil));
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
