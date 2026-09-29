using Ampere.Core.Normas;

namespace Ampere.Tests.Core.Normas;

public class PerfilNormativo_Teste
{
    private static readonly PerfilNormativo Ficticio = PerfilNormativo.Carregar(PerfilFicticio.Json);

    [Test]
    public async Task Perfil_oficial_NBR_5410_2004_e_esqueleto_sem_nenhum_valor_inventado()
    {
        var oficial = PerfilNormativo.NBR5410_2004;

        await Assert.That(oficial.Nome).IsEqualTo("NBR5410:2004");
        await Assert.That(oficial.Ficticio).IsFalse();
        await Assert.That(oficial.SecoesNominaisMm2().Disponivel).IsFalse();
        await Assert.That(oficial.SecoesNominaisMm2().Referencia).IsEqualTo("TODO_NORMA");
        await Assert.That(oficial.CapacidadeDeConducaoA("B1", "PVC", "Cobre", 2, 2.5m).Disponivel).IsFalse();
        await Assert.That(oficial.QuedaDeTensaoMaximaPct("circuito_terminal").Referencia).IsEqualTo("TODO_NORMA");
    }

    [Test]
    public async Task Consulta_devolve_o_valor_com_a_referencia_da_tabela()
    {
        var capacidade = Ficticio.CapacidadeDeConducaoA("B1", "PVC", "Cobre", 2, 2.5m);

        await Assert.That(capacidade.Disponivel).IsTrue();
        await Assert.That(capacidade.Valor).IsEqualTo(20m);
        await Assert.That(capacidade.Referencia).IsEqualTo("FICTÍCIO: capacidade de condução");
    }

    [Test]
    public async Task Consultas_de_todas_as_tabelas()
    {
        await Assert.That(Ficticio.SecoesNominaisMm2().Valor).IsEquivalentTo([1.5m, 2.5m, 4m, 6m, 10m, 16m, 25m]);
        await Assert.That(Ficticio.CorrentesNominaisDeDisjuntorA().Valor!.Count).IsEqualTo(8);
        await Assert.That(Ficticio.CondutoresCarregados("3F").Valor).IsEqualTo(3);
        await Assert.That(Ficticio.SecaoMinimaMm2("Forca").Valor).IsEqualTo(2.5m);
        await Assert.That(Ficticio.FatorDeTemperatura("PVC", 40m).Valor).IsEqualTo(0.8m);
        await Assert.That(Ficticio.FatorDeAgrupamento(2).Valor).IsEqualTo(0.8m);
        await Assert.That(Ficticio.QuedaDeTensaoMaximaPct("circuito_terminal").Valor).IsEqualTo(5m);
        await Assert.That(Ficticio.ResistividadeOhmMm2PorM("Cobre").Valor).IsEqualTo(0.02m);
    }

    [Test]
    [Arguments(1, 50)]
    [Arguments(2, 30)]
    [Arguments(3, 40)]
    [Arguments(7, 40)]
    public async Task Ocupacao_de_eletroduto_usa_a_maior_faixa_para_muitos_condutores(int condutores, int esperado)
    {
        await Assert.That(Ficticio.OcupacaoMaximaDeEletrodutoPct(condutores).Valor).IsEqualTo((decimal)esperado);
    }

    [Test]
    public async Task Linha_inexistente_na_tabela_e_ausencia_explicada_nunca_aproximacao()
    {
        var capacidade = Ficticio.CapacidadeDeConducaoA("F", "PVC", "Cobre", 2, 2.5m);
        var temperatura = Ficticio.FatorDeTemperatura("PVC", 35m);

        await Assert.That(capacidade.Disponivel).IsFalse();
        await Assert.That(capacidade.Ausencia).Contains("método F");
        await Assert.That(temperatura.Disponivel).IsFalse();
        await Assert.That(temperatura.Ausencia).Contains("35 °C não tabelada");
    }

    [Test]
    public async Task Tabela_TODO_NORMA_com_valores_e_rejeitada_porque_seria_valor_sem_fonte()
    {
        var json = PerfilFicticio.Json.Replace("\"ref\": \"FICTÍCIO: seções\"", "\"ref\": \"TODO_NORMA\"");

        await Assert.That(() => PerfilNormativo.Carregar(json))
            .Throws<PerfilNormativoInvalidoException>()
            .WithMessageContaining("secoes_nominais_mm2: tem valores mas a ref é TODO_NORMA");
    }

    [Test]
    public async Task Tabela_sem_ref_e_rejeitada()
    {
        var json = PerfilFicticio.Json.Replace("\"ref\": \"FICTÍCIO: disjuntores\"", "\"ref\": \"\"");

        await Assert.That(() => PerfilNormativo.Carregar(json))
            .Throws<PerfilNormativoInvalidoException>()
            .WithMessageContaining("correntes_nominais_disjuntor_a: sem ref");
    }

    [Test]
    public async Task Perfil_ficticio_precisa_se_declarar_no_nome()
    {
        var json = PerfilFicticio.Json.Replace("\"perfil\": \"FICTICIO-TESTE\"", "\"perfil\": \"NBR5410:2004\"");

        await Assert.That(() => PerfilNormativo.Carregar(json))
            .Throws<PerfilNormativoInvalidoException>()
            .WithMessageContaining("perfil fictício precisa começar com 'FICTICIO'");
    }

    [Test]
    public async Task Perfil_real_nao_pode_citar_referencia_ficticia()
    {
        var json = PerfilFicticio.Json
            .Replace("\"ficticio\": true", "\"ficticio\": false")
            .Replace("\"perfil\": \"FICTICIO-TESTE\"", "\"perfil\": \"NBR5410:2004\"");

        await Assert.That(() => PerfilNormativo.Carregar(json))
            .Throws<PerfilNormativoInvalidoException>()
            .WithMessageContaining("perfil real com referência fictícia");
    }

    [Test]
    public async Task Chave_numerica_com_virgula_e_rejeitada()
    {
        var json = PerfilFicticio.Json.Replace("\"1.5\": 10,", "\"1,5\": 10,");

        await Assert.That(() => PerfilNormativo.Carregar(json))
            .Throws<PerfilNormativoInvalidoException>()
            .WithMessageContaining("chave numérica inválida '1,5'");
    }

    [Test]
    public async Task Arquivo_sem_meta_e_rejeitado()
    {
        var json = PerfilFicticio.Json.Replace("\"$meta\"", "\"meta_errado\"");

        await Assert.That(() => PerfilNormativo.Carregar(json)).Throws<PerfilNormativoInvalidoException>();
    }
}
