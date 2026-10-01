using System.Globalization;
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
        await Assert.That(oficial.FatorDeAgrupamento("B1", 3).Valor).IsEqualTo(0.70m);
        await Assert.That(oficial.FatorDeAgrupamento("B1", 25).Valor).IsEqualTo(0.38m);
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
        await Assert.That(PerfilNormativo.NBR5410_2004.FatorDeAgrupamento("B1", circuitos).Valor).IsEqualTo(fator);
    }

    [Test]
    public async Task Faixa_de_agrupamento_vai_do_inicio_ate_a_proxima_chave_e_a_ultima_nao_tem_fim()
    {
        var oficial = PerfilNormativo.NBR5410_2004;

        await Assert.That(oficial.FaixaDeAgrupamento("B1", 5)).IsEqualTo((5, (int?)5));
        await Assert.That(oficial.FaixaDeAgrupamento("B1", 13)).IsEqualTo((12, (int?)15));
        await Assert.That(oficial.FaixaDeAgrupamento("B1", 25)).IsEqualTo((20, (int?)null));
        await Assert.That(oficial.FaixaDeAgrupamento("B1", 0)).IsNull();
    }

    [Test]
    public async Task Tabela_sem_a_faixa_do_inicio_nao_da_fator()
    {
        var aPartirDe2 = PerfilNormativo.Carregar(PerfilFicticio.Json.Replace("\"valores\": { \"1\": 1, \"2\": 0.8, \"3\": 0.7 }", "\"valores\": { \"2\": 0.8, \"3\": 0.7 }"));

        var fator = aPartirDe2.FatorDeAgrupamento("B1", 1);

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
        await Assert.That(Ficticio.FatorDeAgrupamento("B1", 2).Valor).IsEqualTo(0.8m);
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

        // Alumínio fica de fora até ter resistividade no perfil.
        await Assert.That(vocabulario.MetodosDeInstalacao).IsEquivalentTo(["A1", "A2", "B1", "B2", "C", "D", "E", "F", "G"], CollectionOrdering.Matching);
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
    [Property("Fonte", "NBR 5410:2004, Tabelas 38 e 39")]
    public async Task Metodos_E_F_e_G_pelas_tabelas_38_e_39_com_a_coluna_na_referencia()
    {
        var oficial = PerfilNormativo.NBR5410_2004;

        await Assert.That(oficial.CapacidadeDeConducaoA("E", "PVC", "Cobre", 2, 2.5m).Valor).IsEqualTo(30m);
        await Assert.That(oficial.CapacidadeDeConducaoA("E", "PVC", "Cobre", 3, 1.5m).Valor).IsEqualTo(18.5m);
        await Assert.That(oficial.CapacidadeDeConducaoA("F", "PVC", "Cobre", 2, 1000m).Valor).IsEqualTo(1346m);
        await Assert.That(oficial.CapacidadeDeConducaoA("F", "EPR ou XLPE", "Alumínio", 3, 1000m).Valor).IsEqualTo(1226m);
        await Assert.That(oficial.CapacidadeDeConducaoA("G", "EPR ou XLPE", "Cobre", 3, 25m).Valor).IsEqualTo(161m);
        var errata = oficial.CapacidadeDeConducaoA("G", "PVC", "Alumínio", 3, 630m);
        await Assert.That(errata.Valor).IsEqualTo(730m);
        await Assert.That(errata.Referencia).Contains("630 mm² como impresso");
        await Assert.That(oficial.CapacidadeDeConducaoA("F", "PVC", "Cobre", 3, 25m).Referencia).StartsWith("NBR 5410:2004, Tabela 38, coluna (5)");
        await Assert.That(oficial.CapacidadeDeConducaoA("G", "EPR ou XLPE", "Cobre", 3, 25m).Referencia).StartsWith("NBR 5410:2004, Tabela 39, coluna (8)");
        // A ref das Tabelas 36 e 37 não muda com E, F e G (a de cada linha nova é própria).
        await Assert.That(oficial.CapacidadeDeConducaoA("B1", "PVC", "Cobre", 2, 2.5m).Referencia)
            .IsEqualTo("NBR 5410:2004, Tabelas 36 e 37 (A1 a D; transcrita do texto oficial por coordenadas, validada contra IEC 60364-5-52)");
        await Assert.That(oficial.CapacidadeDeConducaoA("G", "PVC", "Cobre", 2, 2.5m).Ausencia).IsEqualTo("sem linha para método G, PVC, Cobre, 2 condutores carregados");
    }

    [Test]
    [Property("Fonte", "NBR 5410:2004, Tabelas 40 e 44 e item 6.2.5.1.2")]
    public async Task Linha_enterrada_tem_o_fator_do_solo_e_o_agrupamento_da_tabela_44()
    {
        var oficial = PerfilNormativo.NBR5410_2004;

        await Assert.That(oficial.Enterrado("D")).IsTrue();
        await Assert.That(oficial.Enterrado("B1")).IsFalse();
        await Assert.That(oficial.FatorDeTemperatura("D", "PVC", 25m).Valor).IsEqualTo(0.95m);
        await Assert.That(oficial.FatorDeTemperatura("D", "EPR ou XLPE", 80m).Valor).IsEqualTo(0.38m);
        await Assert.That(oficial.FatorDeTemperatura("D", "PVC", 20m).Referencia).IsEqualTo("NBR 5410:2004, Tabela 40 (solo; 20 °C = 1,0 é a referência)");
        await Assert.That(oficial.FatorDeTemperatura("E", "PVC", 40m).Valor).IsEqualTo(0.87m);
        await Assert.That(oficial.FatorDeTemperatura("B1", "PVC", 40m).Referencia).IsEqualTo("NBR 5410:2004, Tabela 40 (ar; 30 C = 1,0 e a referencia)");
        await Assert.That(string.Join("|", Enumerable.Range(1, 6).Select(circuitos => oficial.FatorDeAgrupamento("D", circuitos).Valor.ToString(CultureInfo.InvariantCulture))))
            .IsEqualTo("1.0|0.75|0.65|0.6|0.55|0.5");
        await Assert.That(oficial.FatorDeAgrupamento("D", 7).Ausencia).IsEqualTo("sem fator para 7 circuitos em linha enterrada (método D; a tabela vai até 6 circuitos)");
        await Assert.That(oficial.FatorDeAgrupamento("D", 0).Ausencia).IsEqualTo("número de circuitos inválido (0)");
        await Assert.That(oficial.FaixaDeAgrupamento("D", 3)).IsEqualTo((3, (int?)3));
        await Assert.That(oficial.FaixaDeAgrupamento("D", 7)).IsNull();
        // Fora do solo, a Tabela 42 (feixe) continua valendo, também para E, F e G (6.2.5.5.4).
        await Assert.That(oficial.FatorDeAgrupamento("E", 7).Valor).IsEqualTo(0.54m);
    }

    [Test]
    [Property("Fonte", "NBR 5410:2004, item 6.2.5.1.2")]
    public async Task So_os_metodos_em_eletroduto_levam_eletroduto()
    {
        var oficial = PerfilNormativo.NBR5410_2004;

        await Assert.That(string.Join("|", oficial.Vocabulario.MetodosDeInstalacao.Where(oficial.ComEletroduto))).IsEqualTo("A1|A2|B1|B2|D");
        // Perfil sem a tabela (opcional): todo método leva eletroduto, como antes.
        await Assert.That(Ficticio.ComEletroduto("C")).IsTrue();
        await Assert.That(Ficticio.Enterrado("D")).IsFalse();
    }

    [Test]
    public async Task Tabelas_opcionais_seguem_a_regra_das_demais()
    {
        const string Ancora = "\"protecao_diferencial_por_local\": {";
        string Com(string tabela) => PerfilFicticio.Json.Replace(Ancora, tabela + ", " + Ancora);

        var pendente = PerfilNormativo.Carregar(Com("\"metodos_com_eletroduto\": { \"ref\": \"TODO_NORMA\", \"valores\": [] }"));
        var enterrado = PerfilNormativo.Carregar(Com(
            "\"fator_de_agrupamento_enterrado\": { \"ref\": \"FICTÍCIO: enterrado\", \"metodos\": [\"B1\"], \"valores\": { \"1\": 1, \"2\": 0.6 } }")
            .Replace("{ \"isolacao\": \"PVC\", \"por_temperatura_c\"", "{ \"isolacao\": \"PVC\", \"metodos\": [\"B1\"], \"por_temperatura_c\""));

        await Assert.That(pendente.ComEletroduto("C")).IsTrue();
        await Assert.That(enterrado.FatorDeAgrupamento("B1", 2).Valor).IsEqualTo(0.6m);
        await Assert.That(enterrado.FatorDeAgrupamento("B1", 3).Disponivel).IsFalse();
        await Assert.That(() => PerfilNormativo.Carregar(Com("\"fator_de_agrupamento_enterrado\": { \"ref\": \"FICTÍCIO: enterrado\", \"valores\": { \"1\": 1 } }")))
            .Throws<PerfilNormativoInvalidoException>().WithMessageContaining("fator_de_agrupamento_enterrado: lista de métodos vazia");
        await Assert.That(() => PerfilNormativo.Carregar(Com("\"metodos_com_eletroduto\": { \"ref\": \"TODO_NORMA\", \"valores\": [\"B1\"] }")))
            .Throws<PerfilNormativoInvalidoException>().WithMessageContaining("metodos_com_eletroduto: tem valores mas a ref é TODO_NORMA");
    }

    [Test]
    public async Task Tabelas_que_dependem_umas_das_outras_precisam_concordar()
    {
        const string Ancora = "\"protecao_diferencial_por_local\": {";
        string Com(string tabela) => PerfilFicticio.Json.Replace(Ancora, tabela + ", " + Ancora);
        const string Enterrado = "\"fator_de_agrupamento_enterrado\": { \"ref\": \"FICTÍCIO: enterrado\", \"metodos\": [\"B1\"], \"valores\": { \"1\": 1, \"2\": 0.6 } }";

        // A linha de temperatura sem métodos vale para o enterrado: o ar seria usado no solo.
        await Assert.That(() => PerfilNormativo.Carregar(Com(Enterrado))).Throws<PerfilNormativoInvalidoException>()
            .WithMessageContaining("fator_de_temperatura: linha de PVC sem métodos vale também para o método enterrado B1");
        await Assert.That(() => PerfilNormativo.Carregar(Com("\"metodos_com_eletroduto\": { \"ref\": \"FICTÍCIO: eletroduto\", \"valores\": [\"B1\", \"BI\"] }")))
            .Throws<PerfilNormativoInvalidoException>().WithMessageContaining("metodos_com_eletroduto: método 'BI' fora da tabela de capacidade de condução");
        await Assert.That(() => PerfilNormativo.Carregar(PerfilFicticio.Json.Replace("\"2\": 0.8, \"3\": 0.7 }", "\"2\": 1.2, \"3\": 0.7 }")))
            .Throws<PerfilNormativoInvalidoException>().WithMessageContaining("fator_de_agrupamento: fator de 2 circuitos acima de 1 (1,2)");
    }

    [Test]
    [Arguments("\"ref\": \"\", ")]
    [Arguments("\"ref\": \"TODO_NORMA\", ")]
    public async Task Ref_da_linha_vazia_ou_pendente_e_rejeitada(string referencia)
    {
        var json = PerfilFicticio.Json.Replace("\"condutores_carregados\": 2,", "\"condutores_carregados\": 2, " + referencia);

        await Assert.That(() => PerfilNormativo.Carregar(json)).Throws<PerfilNormativoInvalidoException>()
            .WithMessageContaining("capacidade_de_conducao_a (B1, PVC, Cobre, 2): ref da linha vazia ou TODO_NORMA");
    }

    [Test]
    public async Task Ref_da_linha_ficticia_em_perfil_real_e_rejeitada()
    {
        var json = PerfilFicticio.Json.Replace("\"ficticio\": true", "\"ficticio\": false").Replace("FICTICIO-TESTE", "REAL")
            .Replace("FICTÍCIO", "Fonte").Replace("\"condutores_carregados\": 2,", "\"condutores_carregados\": 2, \"ref\": \"FICTÍCIO: coluna\",");

        await Assert.That(() => PerfilNormativo.Carregar(json)).Throws<PerfilNormativoInvalidoException>()
            .WithMessageContaining("perfil real com referência fictícia em capacidade_de_conducao_a (B1, PVC, Cobre, 2)");
    }

    [Test]
    [Property("Fonte", "NBR 5410:2004, item 6.4.3.1.3 e Tabela 58")]
    public async Task Secao_do_condutor_de_protecao_pela_tabela_58()
    {
        var oficial = PerfilNormativo.NBR5410_2004;
        string Pe(decimal fase) => oficial.SecaoDoCondutorDeProtecaoMm2(fase).Valor.ToString(CultureInfo.InvariantCulture);

        // Até 16: a da fase; até 35: 16; acima: S/2 na padronizada mais próxima (120 → 60: empate entre 50 e 70, a maior).
        await Assert.That(string.Join("|", new[] { 2.5m, 16m, 25m, 35m, 50m, 95m, 120m, 150m, 185m, 400m, 630m, 1000m }.Select(Pe)))
            .IsEqualTo("2.5|16.0|16.0|16.0|25.0|50.0|70.0|70.0|95.0|185.0|300.0|500.0");
        await Assert.That(oficial.SecaoDoCondutorDeProtecaoMm2(3m).Ausencia).IsEqualTo("sem seção do condutor de proteção para fase de 3 mm²");
        await Assert.That(oficial.ReferenciaDaRegra(RegraNormativa.SecaoDoNeutro)).StartsWith("NBR 5410:2004, itens 6.2.6.2.2 a 6.2.6.2.4");
        // A série das seções e a tabela do PE andam juntas: toda seção nominal tem PE.
        foreach (var secao in oficial.SecoesNominaisMm2().Valor)
            await Assert.That(oficial.SecaoDoCondutorDeProtecaoMm2(secao).Disponivel).IsTrue().Because($"{secao} mm²");
    }

    [Test]
    public async Task Secao_de_protecao_maior_que_a_da_fase_e_rejeitada()
    {
        var json = PerfilFicticio.Json.Replace("\"16\": 10, \"25\": 16", "\"16\": 25, \"25\": 16");

        await Assert.That(() => PerfilNormativo.Carregar(json)).Throws<PerfilNormativoInvalidoException>()
            .WithMessageContaining("secao_do_condutor_de_protecao_mm2: seção do condutor de proteção (25 mm²) maior que a da fase (16 mm²)");
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
