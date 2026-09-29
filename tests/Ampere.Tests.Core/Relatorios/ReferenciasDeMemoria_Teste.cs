using System.Runtime.CompilerServices;
using System.Text;
using Ampere.Core.Cargas;
using Ampere.Core.Catalogos;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Normas;
using Ampere.Core.Relatorios;
using Ampere.Tests.Core.Catalogos;
using Ampere.Tests.Core.Normas;

namespace Ampere.Tests.Core.Relatorios;

/// <summary>
///     Golden files: memória (JSON) e relatório (Markdown) de circuitos típicos, comparados byte a byte com os arquivos de
///     Referencias/. Mudança de resultado só entra com justificativa: para regenerar, rode os testes com
///     AMPERE_ATUALIZAR_REFERENCIAS=1 e revise o diff antes do commit.
/// </summary>
public class ReferenciasDeMemoria_Teste
{
    private const string VariavelDeAtualizacao = "AMPERE_ATUALIZAR_REFERENCIAS";

    private static readonly PerfilNormativo Ficticio = PerfilNormativo.Carregar(PerfilFicticio.Json);

    private static readonly CatalogosDeProduto CatalogosDeTeste = new(
        CatalogoDeCondutores.Carregar(CatalogosFicticios.Condutores),
        CatalogoDeEletrodutos.Carregar(CatalogosFicticios.Eletrodutos));

    [Test]
    [Arguments("tug-simples")]
    [Arguments("tug-queda-e-idr")]
    [Arguments("trifasico-idr-dispensado")]
    [Arguments("parado-perfil-oficial")]
    [Arguments("parado-catalogo-vazio")]
    public async Task Memoria_e_relatorio_iguais_aos_de_referencia(string cenario)
    {
        var memoria = Dimensionar(cenario).Memoria!;

        await Conferir($"{cenario}.json", memoria.JsonCanonico());
        await Conferir($"{cenario}.md", RelatorioDeMemoria.Markdown(memoria));
    }

    private static ResultadoDoDimensionamento Dimensionar(string cenario) => cenario switch
    {
        "tug-simples" => DimensionamentoDeCircuito.Dimensionar(
            Entrada("TUG-01", TipoDeCarga.TUG, 1270m, "F+N", 127m, 10m, ["LOCAL-SECO"]), Ficticio, CatalogosDeTeste),
        "tug-queda-e-idr" => DimensionamentoDeCircuito.Dimensionar(
            Entrada("TUG-02", TipoDeCarga.TUG, 1270m, "F+N", 127m, 60m, ["LOCAL-SECO", "LOCAL-MOLHADO"]), Ficticio, CatalogosDeTeste),
        "trifasico-idr-dispensado" => DimensionamentoDeCircuito.Dimensionar(
            Entrada("TUE-01", TipoDeCarga.TUE, 6600m, "3F+N", 220m, 15m, ["LOCAL-MOLHADO"], DecisaoDeIdr.Dispensado("motivo de teste")),
            Ficticio, CatalogosDeTeste),
        "parado-perfil-oficial" => DimensionamentoDeCircuito.Dimensionar(
            Entrada("IL-01", TipoDeCarga.Iluminacao, 200m, "F+N", 127m, 8m, ["LOCAL-SECO"]), PerfilNormativo.NBR5410_2004, CatalogosDeTeste),
        "parado-catalogo-vazio" => DimensionamentoDeCircuito.Dimensionar(
            Entrada("TUG-03", TipoDeCarga.TUG, 1270m, "F+N", 127m, 10m, ["LOCAL-SECO"]), Ficticio, CatalogosDeProduto.Padrao),
        _ => throw new ArgumentOutOfRangeException(nameof(cenario), cenario, "cenário sem definição")
    };

    private static EntradaDeDimensionamento Entrada(
        string circuito, TipoDeCarga tipo, decimal potenciaVA, string fases, decimal tensaoV, decimal comprimentoM,
        IReadOnlyList<string?> locais, DecisaoDeIdr? idr = null) =>
        new(circuito, tipo, potenciaVA, fases, tensaoV, comprimentoM, "B1", "PVC", "Cobre", 30m, 1,
            CatalogosFicticios.TipoDeCondutor, CatalogosFicticios.TipoDeEletroduto, locais, idr);

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

    // Pasta ao lado deste arquivo de teste, no código-fonte (não na saída do build): é ela que vai para o Git.
    private static string PastaDasReferencias([CallerFilePath] string arquivoDoTeste = "")
    {
        var pasta = Path.Combine(Path.GetDirectoryName(arquivoDoTeste)!, "Referencias");
        if (!Directory.Exists(pasta)) throw new DirectoryNotFoundException($"Pasta de referências não encontrada: {pasta}");
        return pasta;
    }
}
