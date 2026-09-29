using Ampere.Core.Memoria;
using Ampere.Core.Relatorios;

namespace Ampere.Tests.Core.Relatorios;

public class RelatorioDeMemoria_Teste
{
    [Test]
    public async Task Cada_passo_mostra_referencia_expressao_valores_e_resultado()
    {
        var relatorio = RelatorioDeMemoria.Markdown(Memoria(Passo()));

        await Assert.That(relatorio).Contains(
            "### 1. Corrente de projeto\n\n" +
            "- **Referência:** Tabela 36\n" +
            "- **Expressão:** `IB = S / V`\n" +
            "- **Valores:** S = 1270 VA; V = 127 V\n" +
            "- **Resultado:** 10 A\n");
    }

    [Test]
    public async Task Numeros_com_virgula_arredondados_so_para_leitura()
    {
        var passo = new PassoDeCalculo("Tabela 36", "Queda de tensão", "ΔV% = k · ρ · L · IB / (S · V) · 100",
            [new ValorDoPasso("k", 1.7320508075688772935274463415m, string.Empty), new ValorDoPasso("ρ", 0.01724m, "Ω·mm²/m")],
            1.2598425196850393700787401575m, "%");

        var relatorio = RelatorioDeMemoria.Markdown(Memoria(passo));

        await Assert.That(relatorio).Contains("- **Valores:** k = 1,7321; ρ = 0,01724 Ω·mm²/m\n");
        await Assert.That(relatorio).Contains("- **Resultado:** 1,2598%\n");
    }

    [Test]
    public async Task Passo_sem_resultado_marca_onde_o_calculo_parou()
    {
        var parada = new PassoDeCalculo("TODO_NORMA", "Capacidade de condução", "IZ₀ = tabela", [], null, "A",
            "tabela capacidade_de_conducao_a sem dados oficiais (TODO_NORMA)");

        var relatorio = RelatorioDeMemoria.Markdown(Memoria(Passo(), parada));

        await Assert.That(relatorio).Contains(
            "- **Situação:** cálculo interrompido no passo 2 (Capacidade de condução): tabela capacidade_de_conducao_a sem dados oficiais (TODO_NORMA)\n");
        await Assert.That(relatorio).Contains("- **Resultado:** não calculado\n");
    }

    [Test]
    public async Task Calculo_completo_e_referencias_pendentes_no_cabecalho()
    {
        var relatorio = RelatorioDeMemoria.Markdown(Memoria(Passo("TODO_NORMA"), Passo(), Passo("TODO_CATALOGO")));

        await Assert.That(relatorio).Contains("- **Situação:** cálculo completo (3 passos)\n");
        await Assert.That(relatorio).Contains("- **Referências pendentes (TODO_NORMA ou TODO_CATALOGO):** 2 passos (1, 3), sem fonte oficial\n");
    }

    [Test]
    public async Task Sem_pendencias_o_cabecalho_nao_as_menciona()
    {
        var relatorio = RelatorioDeMemoria.Markdown(Memoria(Passo()));

        await Assert.That(relatorio).DoesNotContain("pendentes");
        await Assert.That(relatorio).DoesNotContain("Atenção");
    }

    [Test]
    public async Task Identificador_gravado_que_nao_confere_gera_aviso()
    {
        var memoria = Memoria(Passo());

        var confere = RelatorioDeMemoria.Markdown(memoria, memoria.Hash());
        var naoConfere = RelatorioDeMemoria.Markdown(memoria, "sha256:0000");

        await Assert.That(confere).Contains($"- **Identificador (AMP_MemoriaCalculoId):** `{memoria.Hash()}`\n");
        await Assert.That(confere).DoesNotContain("não confere");
        await Assert.That(naoConfere).Contains("- **Atenção:** o identificador gravado no elemento (sha256:0000) não confere com esta memória\n");
    }

    [Test]
    public async Task Perfil_ficticio_e_sinalizado()
    {
        var relatorio = RelatorioDeMemoria.Markdown(new MemoriaDeCalculo("TUG-01", "FICTICIO-TESTE", [Passo()]));

        await Assert.That(relatorio).Contains("- **Atenção:** perfil fictício, só para testes\n");
    }

    [Test]
    public async Task Texto_vindo_de_dados_nao_vira_marcacao()
    {
        var passo = new PassoDeCalculo("Tabela [36] <nota> *x* _y_ a|b & c\nlinha 2", "Teste", "IB = `x`", [], 1m, "A", "TODO_NORMA e AMP_TipoCarga");

        var relatorio = RelatorioDeMemoria.Markdown(Memoria(passo));

        await Assert.That(relatorio).Contains(@"- **Referência:** Tabela \[36\] \<nota\> \*x\* \_y\_ a\|b \& c linha 2" + "\n");
        await Assert.That(relatorio).Contains("- **Expressão:** `` IB = `x` ``\n");
        await Assert.That(relatorio).Contains("- **Observação:** TODO_NORMA e AMP_TipoCarga\n");
    }

    [Test]
    [Arguments(1, "1 ponto")]
    [Arguments(3, "3 pontos")]
    [Arguments(0, "0 pontos")]
    public async Task Contagem_concorda_em_numero(int pontos, string esperado)
    {
        var passo = new PassoDeCalculo("Tabela 36", "Exigência de IDR", "n = pontos", [], pontos, "pontos");

        await Assert.That(RelatorioDeMemoria.Markdown(Memoria(passo))).Contains($"- **Resultado:** {esperado}\n");
    }

    [Test]
    public async Task Mesma_memoria_gera_o_mesmo_texto_com_quebras_LF()
    {
        var primeiro = RelatorioDeMemoria.Markdown(Memoria(Passo()));
        var segundo = RelatorioDeMemoria.Markdown(Memoria(Passo()));

        await Assert.That(primeiro).IsEqualTo(segundo);
        await Assert.That(primeiro).DoesNotContain("\r");
        await Assert.That(primeiro).EndsWith("- **Resultado:** 10 A\n");
    }

    private static MemoriaDeCalculo Memoria(params PassoDeCalculo[] passos) => new("TUG-01", "NBR5410:2004", passos);

    private static PassoDeCalculo Passo(string referencia = "Tabela 36") =>
        new(referencia, "Corrente de projeto", "IB = S / V", [new ValorDoPasso("S", 1270m, "VA"), new ValorDoPasso("V", 127m, "V")], 10m, "A");
}
