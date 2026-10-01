using Ampere.Core.Cargas;
using Ampere.Core.Normas;
using TUnit.Assertions.Enums;

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
        // Pacote 2 do texto oficial: capacidade (Tabelas 36/37), condutores carregados e seções com ref real.
        await Assert.That(oficial.SecoesNominaisMm2().Valor!.Count).IsEqualTo(24);
        var capacidadeB1 = oficial.CapacidadeDeConducaoA("B1", "PVC", "Cobre", 2, 2.5m);
        await Assert.That(capacidadeB1.Disponivel).IsTrue();
        await Assert.That(capacidadeB1.Valor).IsEqualTo(24m);
        var condutoresCarregados = oficial.CondutoresCarregados("F+N");
        await Assert.That(condutoresCarregados.Valor).IsEqualTo(2);
        await Assert.That(oficial.CapacidadeDeConducaoA("B1", "EPR ou XLPE", "Cobre", 2, 1.5m).Valor).IsEqualTo(23m);
        // Pacote 1 preenchido a partir do texto oficial (data/DATA_GAPS.md): queda, temperatura, agrupamento e ocupação têm ref real.
        var queda = oficial.QuedaDeTensaoMaximaPct("circuito_terminal");
        await Assert.That(queda.Disponivel).IsTrue();
        await Assert.That(queda.Valor).IsEqualTo(4m);
        await Assert.That(queda.Referencia).Contains("6.2.7");
        var temperatura = oficial.FatorDeTemperatura("B1", "PVC", 40m);
        await Assert.That(temperatura.Valor).IsEqualTo(0.87m);
        await Assert.That(oficial.FatorDeAgrupamento(3).Valor).IsEqualTo(0.70m);
        await Assert.That(oficial.FatorDeAgrupamento(25).Valor).IsEqualTo(0.38m);
        await Assert.That(oficial.OcupacaoMaximaDeEletrodutoPct(3).Valor).IsEqualTo(40m);
        await Assert.That(oficial.CorrentesNominaisDeIdrA().Valor!.Count).IsEqualTo(5);
        await Assert.That(oficial.SensibilidadesNominaisDeIdrMa().Valor).IsEquivalentTo([6m, 10m, 30m, 100m, 300m, 500m]);
        await Assert.That(oficial.ProtecaoDiferencialPorLocal().Valor!.Count).IsEqualTo(4);
        await Assert.That(oficial.ProtecaoDiferencialPorLocal().Valor!["Local com banheira ou chuveiro"].SensibilidadeMaximaMa).IsEqualTo(30m);
    }

    [Test]
    [Arguments(8, 0.52)]
    [Arguments(9, 0.50)]
    [Arguments(11, 0.50)]
    [Arguments(12, 0.45)]
    [Arguments(15, 0.45)]
    [Arguments(16, 0.41)]
    [Arguments(19, 0.41)]
    [Arguments(20, 0.38)]
    [Arguments(40, 0.38)]
    public async Task Fator_de_agrupamento_oficial_pela_faixa_de_circuitos(int circuitos, decimal fator)
    {
        await Assert.That(PerfilNormativo.NBR5410_2004.FatorDeAgrupamento(circuitos).Valor).IsEqualTo(fator);
    }

    [Test]
    public async Task Faixa_de_agrupamento_vai_do_inicio_ate_a_proxima_chave_e_a_ultima_nao_tem_fim()
    {
        var oficial = PerfilNormativo.NBR5410_2004;

        await Assert.That(oficial.FaixaDeAgrupamento(5)).IsEqualTo((5, (int?)5));
        await Assert.That(oficial.FaixaDeAgrupamento(13)).IsEqualTo((12, (int?)15));
        await Assert.That(oficial.FaixaDeAgrupamento(25)).IsEqualTo((20, (int?)null));
        await Assert.That(oficial.FaixaDeAgrupamento(0)).IsNull();
    }

    [Test]
    public async Task Tabela_sem_a_faixa_do_inicio_nao_da_fator()
    {
        var aPartirDe2 = PerfilNormativo.Carregar(PerfilFicticio.Json.Replace("\"valores\": { \"1\": 1, \"2\": 0.8, \"3\": 0.7 }", "\"valores\": { \"2\": 0.8, \"3\": 0.7 }"));

        var fator = aPartirDe2.FatorDeAgrupamento(1);

        await Assert.That(fator.Disponivel).IsFalse();
        await Assert.That(fator.Ausencia).IsEqualTo("sem fator para 1 circuitos agrupados");
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
        await Assert.That(Ficticio.FatorDeTemperatura("B1", "PVC", 40m).Valor).IsEqualTo(0.8m);
        await Assert.That(Ficticio.FatorDeAgrupamento(2).Valor).IsEqualTo(0.8m);
        await Assert.That(Ficticio.QuedaDeTensaoMaximaPct("circuito_terminal").Valor).IsEqualTo(5m);
        await Assert.That(Ficticio.ResistividadeOhmMm2PorM("Cobre").Valor).IsEqualTo(0.02m);
        await Assert.That(Ficticio.CorrentesNominaisDeIdrA().Valor).IsEquivalentTo([25m, 40m, 63m]);
        await Assert.That(Ficticio.SensibilidadesNominaisDeIdrMa().Valor).IsEquivalentTo([10m, 30m, 300m]);
        await Assert.That(Ficticio.ProtecaoDiferencialPorLocal().Valor.Count).IsEqualTo(4);
    }

    [Test]
    public async Task Vocabulario_do_perfil_oficial_na_ordem_do_arquivo()
    {
        var vocabulario = PerfilNormativo.NBR5410_2004.Vocabulario;

        // D (enterrado) fica de fora até a Tabela 40 do solo; alumínio, até ter resistividade no perfil.
        await Assert.That(vocabulario.MetodosDeInstalacao).IsEquivalentTo(["A1", "A2", "B1", "B2", "C"], CollectionOrdering.Matching);
        await Assert.That(vocabulario.Isolacoes).IsEquivalentTo(["PVC", "EPR ou XLPE"], CollectionOrdering.Matching);
        await Assert.That(vocabulario.Materiais).IsEquivalentTo(["Cobre"], CollectionOrdering.Matching);
        await Assert.That(vocabulario.Locais).IsEquivalentTo(
            ["Local com banheira ou chuveiro", "Area externa", "Cozinha, lavanderia, area de servico ou garagem", "Demais locais internos"],
            CollectionOrdering.Matching);
    }

    [Test]
    public async Task Vocabulario_do_perfil_ficticio_traz_o_que_o_motor_encontra()
    {
        var vocabulario = Ficticio.Vocabulario;

        await Assert.That(vocabulario.MetodosDeInstalacao).IsEquivalentTo(["B1"]);
        await Assert.That(vocabulario.Isolacoes).IsEquivalentTo(["PVC"]);
        await Assert.That(vocabulario.Materiais).IsEquivalentTo(["Cobre"]);
        await Assert.That(vocabulario.Locais).IsEquivalentTo(["LOCAL-SECO", "LOCAL-MOLHADO", "LOCAL-EXTERNO", "LOCAL-ESPECIAL"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Todo_metodo_e_material_do_vocabulario_oficial_tem_fator_de_temperatura_e_resistividade()
    {
        var oficial = PerfilNormativo.NBR5410_2004;

        foreach (var metodo in oficial.Vocabulario.MetodosDeInstalacao)
        foreach (var isolacao in oficial.Vocabulario.Isolacoes)
            await Assert.That(oficial.FatorDeTemperatura(metodo, isolacao, 30m).Disponivel).IsTrue().Because($"{metodo}, {isolacao}");
        foreach (var material in oficial.Vocabulario.Materiais)
            await Assert.That(oficial.ResistividadeOhmMm2PorM(material).Disponivel).IsTrue().Because(material);
    }

    [Test]
    public async Task Linha_de_temperatura_vale_so_para_os_metodos_declarados()
    {
        var perfil = PerfilNormativo.Carregar(PerfilFicticio.Json.Replace(
            "{ \"isolacao\": \"PVC\", \"por_temperatura_c\"", "{ \"isolacao\": \"PVC\", \"metodos\": [\"B1\"], \"por_temperatura_c\""));

        await Assert.That(perfil.FatorDeTemperatura("B1", "PVC", 30m).Valor).IsEqualTo(1m);
        await Assert.That(perfil.FatorDeTemperatura("D", "PVC", 30m).Ausencia).IsEqualTo("sem fatores de temperatura para o método D (PVC) no perfil");
        await Assert.That(perfil.FatorDeTemperatura("D", "EPR", 30m).Ausencia).IsEqualTo("sem fatores para a isolação EPR");
    }

    [Test]
    [Arguments("[]")]
    [Arguments("[null]")]
    [Arguments("[\"B1\", \" \"]")]
    public async Task Lista_de_metodos_vazia_ou_com_metodo_em_branco_e_rejeitada(string metodos)
    {
        var json = PerfilFicticio.Json.Replace("{ \"isolacao\": \"PVC\", \"por_temperatura_c\"", $"{{ \"isolacao\": \"PVC\", \"metodos\": {metodos}, \"por_temperatura_c\"");

        await Assert.That(() => PerfilNormativo.Carregar(json))
            .Throws<PerfilNormativoInvalidoException>()
            .WithMessageContaining("fator_de_temperatura: lista de métodos vazia ou com método em branco");
    }

    [Test]
    public async Task Tabela_sem_dados_oficiais_deixa_o_vocabulario_vazio()
    {
        var json = PerfilFicticio.Json[..PerfilFicticio.Json.IndexOf("\"protecao_diferencial_por_local\"", StringComparison.Ordinal)]
                   + "\"protecao_diferencial_por_local\": { \"ref\": \"TODO_NORMA\", \"valores\": [] } } }";

        var perfil = PerfilNormativo.Carregar(json);

        await Assert.That(perfil.Vocabulario.Locais).IsEmpty();
        await Assert.That(perfil.Vocabulario.MetodosDeInstalacao).IsEquivalentTo(["B1"]);
    }

    [Test]
    public async Task Protecao_diferencial_por_local_da_a_sensibilidade_exigida_por_tipo_de_carga()
    {
        var tabela = Ficticio.ProtecaoDiferencialPorLocal();

        await Assert.That(tabela.Referencia).IsEqualTo("FICTÍCIO: IDR por local");
        await Assert.That(tabela.Valor["LOCAL-MOLHADO"].SensibilidadeExigidaMa(TipoDeCarga.Iluminacao)).IsEqualTo(30m);
        await Assert.That(tabela.Valor["LOCAL-ESPECIAL"].SensibilidadeExigidaMa(TipoDeCarga.TUE)).IsEqualTo(10m);
        await Assert.That(tabela.Valor["LOCAL-EXTERNO"].SensibilidadeExigidaMa(TipoDeCarga.Iluminacao)).IsNull();
        await Assert.That(tabela.Valor["LOCAL-SECO"].SensibilidadeExigidaMa(TipoDeCarga.TUG)).IsNull();
    }

    [Test]
    [Arguments("\"tipos_de_carga\": [\"TUG\"], \"sensibilidade_maxima_ma\": 30", "\"tipos_de_carga\": [\"Tomada\"], \"sensibilidade_maxima_ma\": 30",
        "'LOCAL-EXTERNO' com tipo de carga inválido 'Tomada'")]
    [Arguments("\"tipos_de_carga\": [\"TUG\"], \"sensibilidade_maxima_ma\": 30", "\"tipos_de_carga\": [\"Reserva\"], \"sensibilidade_maxima_ma\": 30",
        "'LOCAL-EXTERNO' com tipo de carga inválido 'Reserva'")]
    [Arguments("\"tipos_de_carga\": [\"TUG\", \"TUE\"], \"sensibilidade_maxima_ma\": 10", "\"tipos_de_carga\": [\"TUG\", \"tug\"], \"sensibilidade_maxima_ma\": 10",
        "'LOCAL-ESPECIAL' com tipo de carga 'tug' repetido")]
    [Arguments("\"tipos_de_carga\": [\"TUG\"], \"sensibilidade_maxima_ma\": 30", "\"tipos_de_carga\": [\"TUG\"]",
        "'LOCAL-EXTERNO' exige IDR sem sensibilidade_maxima_ma positiva")]
    [Arguments("{ \"local\": \"LOCAL-SECO\", \"tipos_de_carga\": [] }", "{ \"local\": \"LOCAL-SECO\", \"tipos_de_carga\": [], \"sensibilidade_maxima_ma\": 30 }",
        "'LOCAL-SECO' tem sensibilidade_maxima_ma mas nenhum tipo de carga")]
    [Arguments("{ \"local\": \"LOCAL-SECO\", \"tipos_de_carga\": [] }", "{ \"local\": \"LOCAL-SECO\" }",
        "'LOCAL-SECO' sem tipos_de_carga")]
    [Arguments("\"local\": \"LOCAL-EXTERNO\"", "\"local\": \"LOCAL-MOLHADO\"", "local 'LOCAL-MOLHADO' repetido")]
    [Arguments("\"local\": \"LOCAL-EXTERNO\"", "\"local\": \" \"", "protecao_diferencial_por_local: linha sem local")]
    public async Task Linha_invalida_de_protecao_diferencial_e_rejeitada(string trecho, string troca, string mensagem)
    {
        var json = PerfilFicticio.Json.Replace(trecho, troca);

        await Assert.That(json).IsNotEqualTo(PerfilFicticio.Json);
        await Assert.That(() => PerfilNormativo.Carregar(json))
            .Throws<PerfilNormativoInvalidoException>()
            .WithMessageContaining(mensagem);
    }

    [Test]
    [Arguments(1, 50)]
    [Arguments(2, 30)]
    [Arguments(3, 40)]
    [Arguments(7, 40)]
    public async Task Ocupacao_de_eletroduto_usa_a_maior_faixa_para_muitos_condutores(int condutores, int esperado)
    {
        await Assert.That(Ficticio.OcupacaoMaximaDeEletrodutoPct(condutores).Valor).IsEqualTo((decimal)esperado);
        await Assert.That(Ficticio.FaixaDeOcupacao(condutores)).IsEqualTo(Math.Min(condutores, 3));
    }

    [Test]
    public async Task Linha_inexistente_na_tabela_e_ausencia_explicada_nunca_aproximacao()
    {
        var capacidade = Ficticio.CapacidadeDeConducaoA("F", "PVC", "Cobre", 2, 2.5m);
        var temperatura = Ficticio.FatorDeTemperatura("B1", "PVC", 35m);

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
    public async Task Regras_da_norma_tem_referencia_no_perfil()
    {
        await Assert.That(Ficticio.ReferenciaDaRegra(RegraNormativa.QuedaDeTensao)).IsEqualTo("FICTÍCIO: regra de queda");
        await Assert.That(PerfilNormativo.NBR5410_2004.ReferenciaDaRegra(RegraNormativa.CoordenacaoCondutorProtecao)).Contains("5.3.4.1");
        await Assert.That(PerfilNormativo.NBR5410_2004.ReferenciaDaRegra(RegraNormativa.CoordenacaoIdrDisjuntor)).IsEqualTo("TODO_NORMA");
        await Assert.That(Ficticio.ReferenciaDaRegra(RegraNormativa.CoordenacaoIdrDisjuntor)).IsEqualTo("FICTÍCIO: regra IDR x disjuntor");
    }

    [Test]
    public async Task Regra_sem_referencia_e_rejeitada()
    {
        var json = PerfilFicticio.Json.Replace("\"queda_de_tensao\": \"FICTÍCIO: regra de queda\"", "\"queda_de_tensao\": \"\"");

        await Assert.That(() => PerfilNormativo.Carregar(json))
            .Throws<PerfilNormativoInvalidoException>()
            .WithMessageContaining("regra queda_de_tensao sem ref");
    }

    [Test]
    public async Task Arquivo_sem_meta_e_rejeitado()
    {
        var json = PerfilFicticio.Json.Replace("\"$meta\"", "\"meta_errado\"");

        await Assert.That(() => PerfilNormativo.Carregar(json)).Throws<PerfilNormativoInvalidoException>();
    }
}
