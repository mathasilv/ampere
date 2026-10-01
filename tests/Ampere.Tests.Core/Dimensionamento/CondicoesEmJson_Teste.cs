using Ampere.Core.Dimensionamento;

namespace Ampere.Tests.Core.Dimensionamento;

public class CondicoesEmJson_Teste
{
    private static readonly CondicoesDoProjeto Completas = new(35.5m, 3, "Cobre", "B1", "PVC", "Cabo de teste", "Eletroduto de teste");

    [Test]
    public async Task Texto_canonico_com_versao_e_sem_os_opcionais_vazios()
    {
        var json = CondicoesEmJson.Escrever(new CondicoesDoProjeto(30m, 1, "Cobre", null, " "));

        await Assert.That(json).IsEqualTo("""{"versao":1,"temperatura_ambiente_c":30,"circuitos_agrupados":1,"material":"Cobre"}""");
    }

    [Test]
    public async Task Ida_e_volta_preserva_as_condicoes()
    {
        await Assert.That(CondicoesEmJson.Ler(CondicoesEmJson.Escrever(Completas))).IsEqualTo(Completas);
        await Assert.That(CondicoesEmJson.Ler(CondicoesEmJson.Escrever(Completas with { Material = "Alumínio" }))!.Material).IsEqualTo("Alumínio");
    }

    [Test]
    public async Task Mesmas_condicoes_dao_o_mesmo_texto()
    {
        await Assert.That(CondicoesEmJson.Escrever(Completas)).IsEqualTo(CondicoesEmJson.Escrever(Completas with { }));
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("não é JSON")]
    [Arguments("[]")]
    [Arguments("""{"temperatura_ambiente_c":30,"circuitos_agrupados":1,"material":"Cobre"}""")]
    [Arguments("""{"versao":2,"temperatura_ambiente_c":30,"circuitos_agrupados":1,"material":"Cobre"}""")]
    [Arguments("""{"versao":1,"circuitos_agrupados":1,"material":"Cobre"}""")]
    [Arguments("""{"versao":1,"temperatura_ambiente_c":"30","circuitos_agrupados":1,"material":"Cobre"}""")]
    [Arguments("""{"versao":1,"temperatura_ambiente_c":30,"circuitos_agrupados":0,"material":"Cobre"}""")]
    [Arguments("""{"versao":1,"temperatura_ambiente_c":30,"circuitos_agrupados":1.5,"material":"Cobre"}""")]
    [Arguments("""{"versao":1,"temperatura_ambiente_c":30,"circuitos_agrupados":1,"material":" "}""")]
    public async Task Texto_vazio_invalido_ou_de_outra_versao_nao_vira_condicao(string? json)
    {
        await Assert.That(CondicoesEmJson.Ler(json)).IsNull();
    }

    [Test]
    public async Task Campos_desconhecidos_sao_ignorados()
    {
        var condicoes = CondicoesEmJson.Ler("""{"versao":1,"temperatura_ambiente_c":30,"circuitos_agrupados":2,"material":"Cobre","futuro":true}""");

        await Assert.That(condicoes).IsEqualTo(new CondicoesDoProjeto(30m, 2, "Cobre"));
    }
}
