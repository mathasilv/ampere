using System.Runtime.CompilerServices;
using System.Text;
using Ampere.Core.Cargas;
using Ampere.Core.Normas;
using Ampere.Core.Quadros;
using Ampere.Core.Relatorios;
using Ampere.Core.Relatorios.Pdf;
using Ampere.Tests.Core.Normas;

namespace Ampere.Tests.Core.Relatorios;

/// <summary>
///     Golden files do quadro de cargas: memória (JSON), relatório (Markdown) e roteiro do PDF comparados byte a byte
///     com os arquivos de Referencias/. Mesmo fluxo de <see cref="ReferenciasDeMemoria_Teste" />: para regenerar,
///     AMPERE_ATUALIZAR_REFERENCIAS=1 e revise o diff antes do commit.
/// </summary>
public class ReferenciasDoQuadro_Teste
{
    private const string VariavelDeAtualizacao = "AMPERE_ATUALIZAR_REFERENCIAS";
    private const string VariavelDeAmostras = "AMPERE_AMOSTRAS_PDF";

    private static readonly PerfilNormativo Ficticio = PerfilNormativo.Carregar(PerfilFicticio.Json);

    private static readonly IReadOnlyList<CircuitoDoQuadro> Circuitos =
    [
        new("IL-01", "Iluminação sala", TipoDeCarga.Iluminacao, 600m),
        new("IL-02", "Iluminação cozinha", TipoDeCarga.Iluminacao, 400m),
        new("TUG-01", "Tomadas sala", TipoDeCarga.TUG, 1000m),
        new("TUE-01", "Chuveiro", TipoDeCarga.TUE, 2000m)
    ];

    [Test]
    [Arguments("quadro-completo")]
    [Arguments("quadro-fator-informado")]
    [Arguments("quadro-2fn-sem-corrente")]
    [Arguments("quadro-3fn-fases")]
    public async Task Memoria_e_relatorio_do_quadro_iguais_aos_de_referencia(string cenario)
    {
        var quadro = Montar(cenario);
        var fases = cenario == "quadro-3fn-fases" ? Fases(quadro) : null;

        await Conferir($"{cenario}.json", quadro.Memoria!.JsonCanonico());
        await Conferir($"{cenario}.md", RelatorioDeMemoria.MarkdownDoQuadro(quadro, fases));
        await Conferir($"{cenario}.pdf.txt", PdfDoRelatorio.Roteiro(ConteudoDoRelatorio.DeQuadro(quadro, fases)));

        if (Environment.GetEnvironmentVariable(VariavelDeAmostras) is { Length: > 0 } pasta)
            await File.WriteAllBytesAsync(Path.Combine(pasta, $"{cenario}.pdf"), RelatorioDeMemoria.PdfDoQuadro(quadro, fases));
    }

    // IL-01 na fase A, IL-02 na B, TUG-01 na C (F+N 127 V) e o chuveiro (2F 220 V) em A e B.
    private static BalancoDasFases Fases(ResultadoDoQuadroDeCargas quadro)
    {
        (string[] Fases, string Configuracao, decimal Tensao)[] circuitos = [(["A"], "F+N", 127m), (["B"], "F+N", 127m), (["C"], "F+N", 127m), (["A", "B"], "2F", 220m)];
        return CargasPorFase.Calcular(["A", "B", "C"], CargasPorFase.FaseNeutro(quadro.Esquema, quadro.TensaoV),
            quadro.Linhas.Select((linha, indice) => new CircuitoNasFases(indice, linha.Numero, linha.PotenciaInstaladaVA, linha.DemandaVA,
                circuitos[indice].Fases, circuitos[indice].Configuracao, circuitos[indice].Tensao)).ToList())!;
    }

    private static ResultadoDoQuadroDeCargas Montar(string cenario) => cenario switch
    {
        "quadro-completo" => QuadroDeCargas.Montar("QD-01", "F+N", 127m, Circuitos, Ficticio),
        "quadro-fator-informado" => QuadroDeCargas.Montar("QD-02", "F+N", 127m, Circuitos, Ficticio,
            new Dictionary<TipoDeCarga, decimal> { [TipoDeCarga.Iluminacao] = 1m }),
        "quadro-2fn-sem-corrente" => QuadroDeCargas.Montar("QD-03", "2F+N", 220m, Circuitos, Ficticio),
        "quadro-3fn-fases" => QuadroDeCargas.Montar("QD-04", "3F+N", 220m, Circuitos, Ficticio, null,
            "sistema de distribuição '220/127 Y' do quadro; corrente média, com as cargas supostas equilibradas entre as fases"),
        _ => throw new ArgumentOutOfRangeException(nameof(cenario), cenario, "cenário sem definição")
    };

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
