using Ampere.Core.Dimensionamento;

namespace Ampere.Tests.Core.Dimensionamento;

public class CondicoesEmJson_Teste
{
    private static readonly CondicoesDoProjeto Completas = new(35.5m, 3, "Cobre", "B1", "PVC", "Cabo de teste", "Eletroduto de teste", 25m, 2);

    [Test]
    public async Task GUID_do_esquema_de_armazenamento_esta_congelado()
    {
        // Os projetos guardam as condições presas a este GUID: trocá-lo deixaria os dados gravados órfãos (AGENTS.md).
        await Assert.That(Guid.Parse(CondicoesEmJson.GuidDoEsquema)).IsEqualTo(Guid.Parse("ee3927c5-6bcf-4343-8649-2dfa88c8b327"));
    }

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
    [Arguments("""{"versao":1,"temperatura_ambiente_c":30,"temperatura_do_solo_c":"20","circuitos_agrupados":1,"material":"Cobre"}""")]
    [Arguments("""{"versao":1,"temperatura_ambiente_c":30,"circuitos_agrupados":1,"circuitos_agrupados_no_solo":0,"material":"Cobre"}""")]
    [Arguments("""{"versao":1,"temperatura_ambiente_c":30,"circuitos_agrupados":1,"circuitos_agrupados_no_solo":1.5,"material":"Cobre"}""")]
    [Arguments("""{"versao":1,"temperatura_ambiente_c":30,"circuitos_agrupados":1,"corrente_de_curto_circuito_ka":0,"material":"Cobre"}""")]
    [Arguments("""{"versao":1,"temperatura_ambiente_c":30,"circuitos_agrupados":1,"corrente_de_curto_circuito_ka":"5","material":"Cobre"}""")]
    public async Task Texto_vazio_invalido_ou_de_outra_versao_nao_vira_condicao(string? json)
    {
        await Assert.That(CondicoesEmJson.Ler(json)).IsNull();
    }

    [Test]
    public async Task Temperatura_do_solo_e_opcional_sem_mudar_a_versao()
    {
        var json = CondicoesEmJson.Escrever(new CondicoesDoProjeto(30m, 1, "Cobre", TemperaturaDoSoloC: 20m, CircuitosAgrupadosNoSolo: 3));

        await Assert.That(json).IsEqualTo(
            """{"versao":1,"temperatura_ambiente_c":30,"temperatura_do_solo_c":20,"circuitos_agrupados":1,"circuitos_agrupados_no_solo":3,"material":"Cobre"}""");
        await Assert.That(CondicoesEmJson.Ler(json)!.TemperaturaDoSoloC).IsEqualTo(20m);
        await Assert.That(CondicoesEmJson.Ler(json)!.CircuitosAgrupadosNoSolo).IsEqualTo(3);
    }

    [Test]
    public async Task Corrente_de_curto_circuito_e_opcional_sem_mudar_a_versao()
    {
        var json = CondicoesEmJson.Escrever(new CondicoesDoProjeto(30m, 1, "Cobre", CorrenteDeCurtoCircuitoKa: 4.5m));

        await Assert.That(json).IsEqualTo("""{"versao":1,"temperatura_ambiente_c":30,"circuitos_agrupados":1,"corrente_de_curto_circuito_ka":4.5,"material":"Cobre"}""");
        await Assert.That(CondicoesEmJson.Ler(json)!.CorrenteDeCurtoCircuitoKa).IsEqualTo(4.5m);
    }

    [Test]
    public async Task Campos_desconhecidos_sao_ignorados()
    {
        var condicoes = CondicoesEmJson.Ler("""{"versao":1,"temperatura_ambiente_c":30,"circuitos_agrupados":2,"material":"Cobre","futuro":true}""");

        await Assert.That(condicoes).IsEqualTo(new CondicoesDoProjeto(30m, 2, "Cobre"));
    }
}
