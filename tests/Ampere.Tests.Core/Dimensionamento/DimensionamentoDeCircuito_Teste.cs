using Ampere.Core.Cargas;
using Ampere.Core.Catalogos;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Memoria;
using Ampere.Core.Normas;
using Ampere.Tests.Core.Catalogos;
using Ampere.Tests.Core.Normas;

namespace Ampere.Tests.Core.Dimensionamento;

/// <summary>
///     Motor de dimensionamento contra o perfil FICTÍCIO (números redondos, não são da NBR 5410). Os resultados
///     esperados foram calculados à mão a partir desse perfil.
/// </summary>
public class DimensionamentoDeCircuito_Teste
{
    private static readonly PerfilNormativo Ficticio = PerfilNormativo.Carregar(PerfilFicticio.Json);

    private static readonly CatalogosDeProduto CatalogosFicticiosCarregados = new(
        CatalogoDeCondutores.Carregar(CatalogosFicticios.Condutores),
        CatalogoDeEletrodutos.Carregar(CatalogosFicticios.Eletrodutos));

    [Test]
    public async Task Local_que_nao_exige_deixa_o_circuito_sem_IDR()
    {
        var resultado = Dimensionar(Entrada());

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Dimensionado);
        await Assert.That(resultado.IdrSensibilidadeMa).IsNull();
        await Assert.That(resultado.IdrNominalA).IsNull();
        await Assert.That(PassoDe(resultado, "Exigência de IDR").Resultado).IsEqualTo(0m);
        await Assert.That(PassoDe(resultado, "Exigência de IDR").Observacao).IsEqualTo("LOCAL-SECO (1 ponto): não exige");
    }

    [Test]
    [Arguments(3175, 25, 25)]
    [Arguments(3810, 32, 40)]
    public async Task Local_que_exige_da_IDR_com_a_menor_corrente_nominal_nao_inferior_ao_disjuntor(decimal potenciaVA, decimal disjuntorA, decimal idrA)
    {
        var resultado = Dimensionar(Entrada(potenciaVA: potenciaVA, locais: ["LOCAL-SECO", "LOCAL-MOLHADO"]));

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Dimensionado);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(disjuntorA);
        await Assert.That(PassoDe(resultado, "Exigência de IDR").Resultado).IsEqualTo(1m);
        await Assert.That(resultado.IdrSensibilidadeMa).IsEqualTo(30m);
        await Assert.That(resultado.IdrNominalA).IsEqualTo(idrA);
    }

    [Test]
    public async Task Tipo_de_carga_que_o_local_nao_atinge_fica_sem_IDR()
    {
        var resultado = Dimensionar(Entrada(potenciaVA: 127m, tipo: TipoDeCarga.Iluminacao, locais: ["LOCAL-EXTERNO"]));

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Dimensionado);
        await Assert.That(resultado.IdrSensibilidadeMa).IsNull();
        await Assert.That(PassoDe(resultado, "Exigência de IDR").Observacao).IsEqualTo("LOCAL-EXTERNO (1 ponto): não exige");
    }

    [Test]
    public async Task Locais_com_exigencias_diferentes_adotam_a_menor_sensibilidade()
    {
        var resultado = Dimensionar(Entrada(locais: ["LOCAL-MOLHADO", "LOCAL-ESPECIAL", "LOCAL-ESPECIAL"]));

        await Assert.That(PassoDe(resultado, "Exigência de IDR").Resultado).IsEqualTo(3m);
        await Assert.That(PassoDe(resultado, "Exigência de IDR").Observacao)
            .IsEqualTo("LOCAL-ESPECIAL (2 pontos): exige IΔn ≤ 10 mA; LOCAL-MOLHADO (1 ponto): exige IΔn ≤ 30 mA");
        await Assert.That(resultado.IdrSensibilidadeMa).IsEqualTo(10m);
        await Assert.That(resultado.IdrNominalA).IsEqualTo(25m);
    }

    [Test]
    public async Task Ponto_sem_local_para_no_IDR_com_explicacao()
    {
        var resultado = Dimensionar(Entrada(locais: [null, "LOCAL-SECO"]));

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Interrompido);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(10m);
        await Assert.That(resultado.Eletroduto).IsNull();
        await Assert.That(string.Join("\n", resultado.Problemas)).Contains("1 ponto sem local: informe o local ou a decisão do projetista sobre o IDR");
    }

    [Test]
    public async Task Local_fora_da_tabela_para_com_explicacao()
    {
        var resultado = Dimensionar(Entrada(locais: ["COZINHA"]));

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Interrompido);
        await Assert.That(string.Join("\n", resultado.Problemas)).Contains("local fora da tabela de IDR: 'COZINHA'");
    }

    [Test]
    public async Task Projetista_exige_IDR_e_a_memoria_registra_a_tabela_e_a_divergencia()
    {
        var resultado = Dimensionar(Entrada(locais: ["LOCAL-MOLHADO"], idr: DecisaoDeIdr.Exigido(300m, "motivo de teste")));

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Dimensionado);
        await Assert.That(resultado.IdrSensibilidadeMa).IsEqualTo(300m);
        await Assert.That(resultado.IdrNominalA).IsEqualTo(25m);
        await Assert.That(PassoDe(resultado, "Exigência de IDR").Observacao)
            .IsEqualTo("decisão do projetista, prevalece sobre a tabela (motivo: motivo de teste); pela tabela: LOCAL-MOLHADO (1 ponto): exige IΔn ≤ 30 mA");
        await Assert.That(resultado.Avisos).IsEquivalentTo(["IΔn de 300 mA acima da máxima de 30 mA exigida pela tabela"]);
    }

    [Test]
    public async Task Projetista_dispensa_IDR_com_aviso_quando_a_tabela_exige()
    {
        var resultado = Dimensionar(Entrada(locais: ["LOCAL-MOLHADO", "LOCAL-SECO"], idr: DecisaoDeIdr.Dispensado("motivo de teste")));

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Dimensionado);
        await Assert.That(resultado.IdrSensibilidadeMa).IsNull();
        await Assert.That(PassoDe(resultado, "Exigência de IDR").Resultado).IsEqualTo(0m);
        await Assert.That(PassoDe(resultado, "Exigência de IDR").Observacao).Contains("ATENÇÃO: a tabela exige IDR em 1 ponto");
        await Assert.That(resultado.Avisos).IsEquivalentTo(["IDR dispensado pelo projetista, mas a tabela o exige em 1 ponto"]);
    }

    [Test]
    public async Task Decisao_do_projetista_dispensa_o_local_dos_pontos()
    {
        var resultado = Dimensionar(Entrada(locais: [null], idr: DecisaoDeIdr.Exigido(30m)));

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Dimensionado);
        await Assert.That(resultado.IdrSensibilidadeMa).IsEqualTo(30m);
        await Assert.That(PassoDe(resultado, "Exigência de IDR").Observacao)
            .Contains("(sem motivo informado); pela tabela: não avaliada (1 ponto sem local");
        await Assert.That(resultado.Avisos).IsEmpty();
    }

    [Test]
    public async Task Nenhuma_corrente_de_IDR_atende_o_disjuntor_para_com_explicacao()
    {
        var soAte25 = PerfilNormativo.Carregar(PerfilFicticio.Json.Replace("\"valores\": [25, 40, 63]", "\"valores\": [25]"));

        var resultado = DimensionamentoDeCircuito.Dimensionar(Entrada(potenciaVA: 3810m, locais: ["LOCAL-MOLHADO"]), soAte25, CatalogosFicticiosCarregados);

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Interrompido);
        await Assert.That(resultado.IdrSensibilidadeMa).IsEqualTo(30m);
        await Assert.That(resultado.IdrNominalA).IsNull();
        await Assert.That(resultado.IdrAvaliado).IsFalse(); // exigência decidida, mas a proteção não ficou completa
        await Assert.That(string.Join("\n", resultado.Problemas)).Contains("nenhuma corrente nominal de IDR do perfil atende In = 32 A (maior: 25 A)");
    }

    [Test]
    public async Task Decisao_do_projetista_incoerente_e_entrada_invalida()
    {
        var semSensibilidade = Dimensionar(Entrada(idr: new DecisaoDeIdr(true, null)));
        var dispensaComSensibilidade = Dimensionar(Entrada(idr: new DecisaoDeIdr(false, 30m)));

        await Assert.That(semSensibilidade.Situacao).IsEqualTo(SituacaoDoDimensionamento.EntradaInvalida);
        await Assert.That(string.Join("\n", semSensibilidade.Problemas)).Contains("IDR exigido pelo projetista sem sensibilidade positiva");
        await Assert.That(dispensaComSensibilidade.Situacao).IsEqualTo(SituacaoDoDimensionamento.EntradaInvalida);
        await Assert.That(string.Join("\n", dispensaComSensibilidade.Problemas)).Contains("IDR dispensado pelo projetista não leva sensibilidade");
    }

    [Test]
    public async Task Secao_minima_do_projetista_eleva_o_piso_e_a_memoria_registra_a_justificativa()
    {
        var resultado = Dimensionar(Entrada() with { SecaoMinimaDoProjetistaMm2 = 4m, Justificativa = "padrão da obra" });

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Dimensionado);
        await Assert.That(resultado.SecaoMm2).IsEqualTo(4m);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(10m);
        await Assert.That(PassoDe(resultado, "Seção mínima do projetista").Resultado).IsEqualTo(4m);
        await Assert.That(PassoDe(resultado, "Seção mínima do projetista").Observacao)
            .IsEqualTo("decisão do projetista (justificativa: padrão da obra); o cálculo pode adotar seção maior, nunca menor");
        await Assert.That(PassoDe(resultado, "Seção pela capacidade de condução").Valores[0]).IsEqualTo(new ValorDoPasso("Spiso", 4m, "mm²"));
        await Assert.That(resultado.Avisos).IsEmpty();
    }

    [Test]
    public async Task Secao_minima_do_projetista_abaixo_da_norma_vale_a_da_norma_com_aviso()
    {
        var resultado = Dimensionar(Entrada() with { SecaoMinimaDoProjetistaMm2 = 1.5m });

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Dimensionado);
        await Assert.That(resultado.SecaoMm2).IsEqualTo(2.5m);
        await Assert.That(PassoDe(resultado, "Seção mínima do projetista").Observacao)
            .IsEqualTo("decisão do projetista (sem justificativa informada); abaixo da seção mínima da norma (2,5 mm²), que prevalece");
        await Assert.That(resultado.Avisos).IsEquivalentTo(["seção mínima do projetista (1,5 mm²) abaixo da mínima da norma (2,5 mm²): vale a da norma"]);
    }

    [Test]
    public async Task Secao_minima_do_projetista_fora_das_nominais_para_com_explicacao()
    {
        var resultado = Dimensionar(Entrada() with { SecaoMinimaDoProjetistaMm2 = 3m });

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Interrompido);
        await Assert.That(resultado.SecaoMm2).IsNull();
        await Assert.That(resultado.Problemas).IsEquivalentTo(["3 mm² não é seção nominal do perfil (1,5; 2,5; 4; 6; 10; 16; 25)"]);
    }

    [Test]
    public async Task Disjuntor_do_projetista_e_verificado_e_adotado()
    {
        var resultado = Dimensionar(Entrada() with { DisjuntorDoProjetistaA = 20m, Justificativa = "seletividade" });

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Dimensionado);
        await Assert.That(resultado.SecaoMm2).IsEqualTo(2.5m);
        await Assert.That(resultado.CapacidadeDeConducaoA).IsEqualTo(20m);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(20m);
        await Assert.That(PassoDe(resultado, "Disjuntor do projetista").Expressao).IsEqualTo("IB ≤ In (IZ ≥ In verificado na seção)");
        await Assert.That(PassoDe(resultado, "Disjuntor").Expressao).IsEqualTo("In do projetista, com IB ≤ In ≤ IZ");
        await Assert.That(PassoDe(resultado, "Disjuntor").Observacao).IsEqualTo("decisão do projetista, verificada (justificativa: seletividade)");
    }

    [Test]
    public async Task Disjuntor_do_projetista_eleva_a_secao_ate_IZ_atender()
    {
        var resultado = Dimensionar(Entrada() with { DisjuntorDoProjetistaA = 25m });

        await Assert.That(resultado.SecaoMm2).IsEqualTo(4m);
        await Assert.That(resultado.CapacidadeDeConducaoA).IsEqualTo(30m);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(25m);
        await Assert.That(PassoDe(resultado, "Seção pela capacidade de condução").Expressao)
            .IsEqualTo("menor S ≥ Smín com IZ₀(S) · FCA · FCT ≥ In do projetista");
    }

    [Test]
    public async Task IDR_acompanha_o_disjuntor_do_projetista()
    {
        var resultado = Dimensionar(Entrada(locais: ["LOCAL-MOLHADO"]) with { DisjuntorDoProjetistaA = 32m });

        await Assert.That(resultado.SecaoMm2).IsEqualTo(6m);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(32m);
        await Assert.That(resultado.IdrNominalA).IsEqualTo(40m);
    }

    [Test]
    [Arguments(15, "In = 15 A fora das correntes nominais do perfil (10; 16; 20; 25; 32; 40; 50; 63)")]
    [Arguments(10, "In = 10 A do projetista abaixo de IB = 15 A")]
    public async Task Disjuntor_do_projetista_invalido_para_com_explicacao(decimal disjuntorA, string problema)
    {
        var resultado = Dimensionar(Entrada(potenciaVA: 1905m) with { DisjuntorDoProjetistaA = disjuntorA });

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Interrompido);
        await Assert.That(resultado.SecaoMm2).IsNull();
        await Assert.That(resultado.DisjuntorA).IsNull();
        await Assert.That(resultado.Problemas).IsEquivalentTo([problema]);
    }

    [Test]
    public async Task Disjuntor_do_projetista_acima_de_qualquer_IZ_para_com_explicacao()
    {
        var resultado = Dimensionar(Entrada(temperaturaC: 40m, circuitosAgrupados: 3) with { DisjuntorDoProjetistaA = 63m });

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Interrompido);
        await Assert.That(resultado.Problemas).IsEquivalentTo(["nenhuma seção do perfil atende In = 63 A (maior seção: 25 mm²)"]);
    }

    [Test]
    public async Task Decisao_numerica_nao_positiva_e_entrada_invalida()
    {
        var resultado = Dimensionar(Entrada() with { SecaoMinimaDoProjetistaMm2 = 0m, DisjuntorDoProjetistaA = -10m });

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.EntradaInvalida);
        await Assert.That(resultado.Problemas).IsEquivalentTo(
            ["seção mínima do projetista deve ser positiva", "disjuntor do projetista deve ser positivo"]);
    }

    [Test]
    public async Task Disjuntor_do_projetista_volta_a_elevar_a_secao_se_uma_secao_maior_tem_IZ_menor()
    {
        // Tabela não monotônica (fictícia): a queda leva de 6 para 10 mm², que tem IZ abaixo do In do projetista.
        var naoMonotonica = PerfilNormativo.Carregar(PerfilFicticio.Json.Replace("\"6\": 40, \"10\": 60", "\"6\": 40, \"10\": 20"));

        var resultado = DimensionamentoDeCircuito.Dimensionar(Entrada(comprimentoM: 100m) with { DisjuntorDoProjetistaA = 25m }, naoMonotonica, CatalogosFicticiosCarregados);

        await Assert.That(resultado.SecaoMm2).IsEqualTo(16m);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(25m);
        await Assert.That(Observacoes(resultado)).Contains("seção elevada de 10 para 16 mm²: IZ = 20 A abaixo do In = 25 A do disjuntor do projetista");
    }

    [Test]
    public async Task IDn_do_projetista_fora_das_sensibilidades_nominais_para_com_explicacao()
    {
        var resultado = Dimensionar(Entrada(locais: ["LOCAL-MOLHADO"], idr: DecisaoDeIdr.Exigido(25m)));

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Interrompido);
        await Assert.That(resultado.IdrAvaliado).IsFalse();
        await Assert.That(resultado.IdrNominalA).IsNull();
        await Assert.That(resultado.Problemas).IsEquivalentTo(["IΔn = 25 mA do projetista fora das sensibilidades nominais do perfil (10; 30; 300)"]);
    }

    [Test]
    public async Task Agrupamento_entre_chaves_usa_a_faixa_e_a_memoria_mostra_qual()
    {
        var comFaixas = PerfilNormativo.Carregar(PerfilFicticio.Json.Replace("\"3\": 0.7 }", "\"3\": 0.7, \"5\": 0.6 }"));

        var quatro = DimensionamentoDeCircuito.Dimensionar(Entrada(circuitosAgrupados: 4), comFaixas, CatalogosFicticiosCarregados);
        var sete = DimensionamentoDeCircuito.Dimensionar(Entrada(circuitosAgrupados: 7), comFaixas, CatalogosFicticiosCarregados);

        await Assert.That(quatro.FCA).IsEqualTo(0.7m);
        await Assert.That(PassoDe(quatro, "Fator de correção de agrupamento").Expressao).IsEqualTo("FCA = tabela (4 circuitos: faixa de 3 a 4)");
        await Assert.That(sete.FCA).IsEqualTo(0.6m);
        await Assert.That(PassoDe(sete, "Fator de correção de agrupamento").Expressao).IsEqualTo("FCA = tabela (7 circuitos: faixa de 5 ou mais)");
        await Assert.That(PassoDe(Dimensionar(Entrada(circuitosAgrupados: 2)), "Fator de correção de agrupamento").Expressao).IsEqualTo("FCA = tabela (2 circuitos)");
    }

    [Test]
    public async Task Origem_da_temperatura_e_do_agrupamento_vai_para_a_memoria_dos_fatores()
    {
        var resultado = Dimensionar(Entrada(temperaturaC: 40m, circuitosAgrupados: 2) with
        {
            OrigemDaTemperatura = "origem da temperatura",
            OrigemDoAgrupamento = "origem do agrupamento"
        });

        await Assert.That(PassoDe(resultado, "Fator de correção de temperatura").Observacao).IsEqualTo("θ: origem da temperatura");
        await Assert.That(PassoDe(resultado, "Fator de correção de agrupamento").Observacao).IsEqualTo("circuitos: origem do agrupamento");
        await Assert.That(resultado.Memoria!.Hash()).IsNotEqualTo(Dimensionar(Entrada(temperaturaC: 40m, circuitosAgrupados: 2)).Memoria!.Hash());
    }

    [Test]
    public async Task Sem_decisoes_a_memoria_nao_ganha_passos_do_projetista()
    {
        var resultado = Dimensionar(Entrada());

        await Assert.That(resultado.Memoria!.Passos.Where(passo => passo.Descricao.Contains("projetista"))).IsEmpty();
        await Assert.That(PassoDe(resultado, "Fator de correção de temperatura").Observacao).IsNull();
    }

    [Test]
    public async Task Eletroduto_e_o_menor_tamanho_dentro_da_ocupacao_maxima()
    {
        var resultado = Dimensionar(Entrada());

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Dimensionado);
        await Assert.That(resultado.Eletroduto).IsEqualTo("B");
        await Assert.That(resultado.DiametroInternoDoEletrodutoMm).IsEqualTo(15m);
        await Assert.That(Math.Round(resultado.OcupacaoDoEletrodutoPct!.Value, 4)).IsEqualTo(21.3333m);
        await Assert.That(Observacoes(resultado)).Contains("acima da taxa: A (48%)");
        await Assert.That(Observacoes(resultado)).Contains("outros circuitos na mesma tubulação não entram");
    }

    [Test]
    public async Task Ocupacao_igual_a_taxa_maxima_e_aceita()
    {
        // 3 condutores de 4 mm no tamanho A (Di 10 mm) ocupam exatamente 48%.
        var taxaDe48 = PerfilNormativo.Carregar(PerfilFicticio.Json.Replace("\"3\": 40", "\"3\": 48"));

        var resultado = DimensionamentoDeCircuito.Dimensionar(Entrada(), taxaDe48, CatalogosFicticiosCarregados);

        await Assert.That(PassoDe(resultado, "Taxa máxima de ocupação").Resultado).IsEqualTo(48m);
        await Assert.That(resultado.Eletroduto).IsEqualTo("A");
        await Assert.That(resultado.OcupacaoDoEletrodutoPct).IsEqualTo(48m);
    }

    [Test]
    public async Task Trifasico_com_neutro_leva_cinco_condutores_ao_eletroduto()
    {
        var resultado = Dimensionar(Entrada(potenciaVA: 6600m, fases: "3F+N", tensaoV: 220m));

        await Assert.That(resultado.SecaoMm2).IsEqualTo(4m);
        await Assert.That(PassoDe(resultado, "Condutores no eletroduto").Resultado).IsEqualTo(5m);
        await Assert.That(PassoDe(resultado, "Taxa máxima de ocupação").Expressao).IsEqualTo("taxa = tabela (5 condutores: faixa de 3 ou mais)");
        await Assert.That(resultado.Eletroduto).IsEqualTo("C");
        await Assert.That(resultado.OcupacaoDoEletrodutoPct).IsEqualTo(31.25m);
    }

    [Test]
    public async Task Nenhum_eletroduto_do_catalogo_atende_para_com_explicacao()
    {
        var soOPequeno = CatalogoDeEletrodutos.Carregar("""
            {
              "$meta": { "fonte": "fictício, só para testes", "versao": "0", "data": "2026-09-29", "ficticio": true },
              "catalogo": "eletrodutos",
              "ref": "FICTÍCIO: só um tamanho pequeno",
              "tipos": [ { "tipo": "ELETRODUTO-TESTE", "tamanhos": [ { "nominal": "A", "diametro_interno_mm": 10 } ] } ]
            }
            """);

        var resultado = DimensionamentoDeCircuito.Dimensionar(Entrada(), Ficticio, CatalogosFicticiosCarregados with { Eletrodutos = soOPequeno });

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Interrompido);
        await Assert.That(string.Join("\n", resultado.Problemas)).Contains("nenhum eletroduto 'ELETRODUTO-TESTE' do catálogo atende");
    }

    [Test]
    public async Task Catalogo_oficial_vazio_para_no_diametro_do_condutor()
    {
        var resultado = DimensionamentoDeCircuito.Dimensionar(Entrada(), Ficticio, CatalogosDeProduto.Padrao);

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Interrompido);
        await Assert.That(resultado.SecaoMm2).IsEqualTo(2.5m);
        var ultimo = resultado.Memoria!.Passos[^1];
        await Assert.That(ultimo.Referencia).IsEqualTo("TODO_CATALOGO");
        await Assert.That(ultimo.Observacao).Contains("catálogo de condutores sem dados (TODO_CATALOGO)");
    }

    [Test]
    [Arguments("F+N", 1270, 127, 10)]
    [Arguments("2F", 4400, 220, 20)]
    [Arguments("3F", 6600, 220, 17.320508)]
    [Arguments("3F+N", 6600, 220, 17.320508)]
    public async Task Corrente_de_projeto_por_configuracao(string fases, decimal potenciaVA, decimal tensaoV, decimal esperadaA)
    {
        var resultado = Dimensionar(Entrada(potenciaVA, fases, tensaoV));

        await Assert.That(Math.Round(resultado.CorrenteDeProjetoA!.Value, 6)).IsEqualTo(esperadaA);
    }

    [Test]
    public async Task Circuito_simples_dimensionado_de_ponta_a_ponta()
    {
        var resultado = Dimensionar(Entrada());

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Dimensionado);
        await Assert.That(resultado.CorrenteDeProjetoA).IsEqualTo(10m);
        await Assert.That(resultado.SecaoMm2).IsEqualTo(2.5m);
        await Assert.That(resultado.CapacidadeDeConducaoA).IsEqualTo(20m);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(10m);
        await Assert.That(Math.Round(resultado.QuedaDeTensaoPct!.Value, 4)).IsEqualTo(1.2598m);
        await Assert.That(resultado.PerfilNorma).IsEqualTo("FICTICIO-TESTE");
    }

    [Test]
    public async Task Fatores_de_temperatura_e_agrupamento_reduzem_a_capacidade()
    {
        var resultado = Dimensionar(Entrada(potenciaVA: 1905m, circuitosAgrupados: 3));

        await Assert.That(resultado.FCA).IsEqualTo(0.7m);
        await Assert.That(resultado.FCT).IsEqualTo(1m);
        await Assert.That(resultado.SecaoMm2).IsEqualTo(4m);
        await Assert.That(resultado.CapacidadeDeConducaoA).IsEqualTo(21m);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(16m);
    }

    [Test]
    public async Task Coordenacao_com_o_disjuntor_eleva_a_secao_e_explica()
    {
        var resultado = Dimensionar(Entrada(potenciaVA: 1524m, temperaturaC: 40m, circuitosAgrupados: 2));

        await Assert.That(resultado.SecaoMm2).IsEqualTo(4m);
        await Assert.That(resultado.CapacidadeDeConducaoA).IsEqualTo(19.2m);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(16m);
        await Assert.That(Observacoes(resultado)).Contains("seção elevada de 2,5 para 4 mm²: nenhum disjuntor entre IB = 12 A e IZ = 12,8 A");
    }

    [Test]
    public async Task IDR_avaliado_so_quando_a_exigencia_foi_decidida()
    {
        await Assert.That(Dimensionar(Entrada()).IdrAvaliado).IsTrue();
        await Assert.That(Dimensionar(Entrada(locais: ["LOCAL-MOLHADO"])).IdrAvaliado).IsTrue();
        await Assert.That(Dimensionar(Entrada(locais: ["LOCAL-MOLHADO"]) with { TipoDeEletroduto = null }).IdrAvaliado).IsTrue();
        await Assert.That(Dimensionar(Entrada(locais: [null])).IdrAvaliado).IsFalse();
        await Assert.That(Dimensionar(Entrada(fases: "2F+N")).IdrAvaliado).IsFalse();
        await Assert.That(Dimensionar(Entrada(tensaoV: 0m)).IdrAvaliado).IsFalse();
    }

    [Test]
    public async Task Comprimento_zero_e_entrada_invalida()
    {
        var resultado = Dimensionar(Entrada(comprimentoM: 0m));

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.EntradaInvalida);
        await Assert.That(resultado.Problemas).IsEquivalentTo(["comprimento deve ser positivo"]);
    }

    [Test]
    [Property("Fonte", "TODO_NORMA")]
    public async Task Metodo_enterrado_para_no_fator_de_temperatura_enquanto_a_tabela_do_solo_falta()
    {
        var resultado = DimensionamentoDeCircuito.Dimensionar(
            Entrada(locais: ["Demais locais internos"]) with { MetodoDeInstalacao = "D" }, PerfilNormativo.NBR5410_2004, CatalogosFicticiosCarregados);

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Interrompido);
        await Assert.That(resultado.FCT).IsNull();
        await Assert.That(resultado.Problemas).IsEquivalentTo(["sem fatores de temperatura para o método D (PVC) no perfil"]);
    }

    [Test]
    public async Task Origem_do_comprimento_vai_para_a_observacao_da_queda_de_tensao()
    {
        var resultado = Dimensionar(Entrada() with { OrigemDoComprimento = "calculado pelo Revit (do quadro ao ponto mais distante)" });

        await Assert.That(PassoDe(resultado, "Queda de tensão").Observacao).IsEqualTo(
            "fórmula resistiva (sem reatância), só o circuito terminal; L: calculado pelo Revit (do quadro ao ponto mais distante)");
        await Assert.That(resultado.Memoria!.Hash()).IsNotEqualTo(Dimensionar(Entrada()).Memoria!.Hash());
    }

    [Test]
    public async Task Sem_tipo_de_condutor_calcula_ate_o_IDR_e_para_no_diametro()
    {
        var resultado = Dimensionar(Entrada(locais: ["LOCAL-MOLHADO"]) with { TipoDeCondutor = null });

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Interrompido);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(10m);
        await Assert.That(resultado.IdrNominalA).IsEqualTo(25m);
        await Assert.That(resultado.Eletroduto).IsNull();
        await Assert.That(resultado.Problemas).IsEquivalentTo(["tipo de condutor não informado (no circuito ou nas condições do projeto)"]);
        await Assert.That(resultado.Memoria!.Passos[^1].Expressao).IsEqualTo("d = catálogo (tipo não informado; 2,5 mm²)");
    }

    [Test]
    public async Task Sem_tipo_de_eletroduto_para_nos_tamanhos_com_o_motivo()
    {
        var resultado = Dimensionar(Entrada() with { TipoDeEletroduto = " " });

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Interrompido);
        await Assert.That(resultado.Problemas).IsEquivalentTo(["tipo de eletroduto não informado (nas condições do projeto)"]);
        await Assert.That(resultado.Memoria!.Passos[^1].Descricao).IsEqualTo("Tamanhos de eletroduto");
        await Assert.That(resultado.Memoria.Passos[^1].Expressao).IsEqualTo("Di ∈ catálogo (tipo não informado)");
    }

    [Test]
    public async Task Queda_de_tensao_eleva_a_secao_e_explica()
    {
        var resultado = Dimensionar(Entrada(comprimentoM: 60m));

        await Assert.That(resultado.SecaoMm2).IsEqualTo(4m);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(10m);
        await Assert.That(Math.Round(resultado.QuedaDeTensaoPct!.Value, 4)).IsEqualTo(4.7244m);
        await Assert.That(Observacoes(resultado)).Contains("seção elevada de 2,5 para 4 mm²: queda de tensão 7,5591% acima do limite de 5%");
    }

    [Test]
    public async Task Iluminacao_usa_a_secao_minima_de_iluminacao()
    {
        var resultado = Dimensionar(Entrada(potenciaVA: 127m, tipo: TipoDeCarga.Iluminacao));

        await Assert.That(resultado.SecaoMm2).IsEqualTo(1.5m);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(10m);
    }

    [Test]
    public async Task Toda_linha_da_memoria_cita_a_referencia_do_perfil_nunca_do_codigo()
    {
        var memoria = Dimensionar(Entrada(comprimentoM: 60m, locais: ["LOCAL-MOLHADO"])).Memoria!;

        await Assert.That(memoria.Passos.Count).IsGreaterThan(5);
        await Assert.That(memoria.Passos.Select(passo => passo.Referencia).Where(referencia => !referencia.StartsWith("FICTÍCIO"))).IsEmpty();
    }

    [Test]
    public async Task Mesma_entrada_gera_a_mesma_memoria_e_o_mesmo_hash()
    {
        await Assert.That(Dimensionar(Entrada()).Memoria!.Hash()).IsEqualTo(Dimensionar(Entrada()).Memoria!.Hash());
    }

    [Test]
    public async Task Perfil_oficial_dimensiona_o_circuito_completo()
    {
        var resultado = DimensionamentoDeCircuito.Dimensionar(
            Entrada(locais: ["Demais locais internos"]), PerfilNormativo.NBR5410_2004, CatalogosFicticiosCarregados);

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Dimensionado);
        await Assert.That(resultado.CorrenteDeProjetoA).IsEqualTo(10m);
        await Assert.That(resultado.SecaoMm2).IsEqualTo(2.5m);
        await Assert.That(resultado.DisjuntorA).IsEqualTo(10m);
        await Assert.That(resultado.Problemas).IsEmpty();
    }

    [Test]
    public async Task Configuracao_2F_mais_N_para_com_explicacao()
    {
        var resultado = Dimensionar(Entrada(fases: "2F+N"));

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Interrompido);
        await Assert.That(resultado.CorrenteDeProjetoA).IsNull();
        await Assert.That(string.Join("\n", resultado.Problemas)).Contains("2F+N");
    }

    [Test]
    public async Task Corrente_maior_que_qualquer_secao_para_com_explicacao()
    {
        var resultado = Dimensionar(Entrada(potenciaVA: 25400m));

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.Interrompido);
        await Assert.That(string.Join("\n", resultado.Problemas)).Contains("nenhuma seção do perfil atende IB = 200 A");
    }

    [Test]
    public async Task Entrada_invalida_nao_gera_memoria()
    {
        var resultado = Dimensionar(Entrada(potenciaVA: -1m, tensaoV: 0m, tipo: TipoDeCarga.Reserva, circuitosAgrupados: 0));

        await Assert.That(resultado.Situacao).IsEqualTo(SituacaoDoDimensionamento.EntradaInvalida);
        await Assert.That(resultado.Memoria).IsNull();
        await Assert.That(resultado.Problemas.Count).IsEqualTo(4);
    }

    private static ResultadoDoDimensionamento Dimensionar(EntradaDeDimensionamento entrada) =>
        DimensionamentoDeCircuito.Dimensionar(entrada, Ficticio, CatalogosFicticiosCarregados);

    private static EntradaDeDimensionamento Entrada(
        decimal potenciaVA = 1270m,
        string fases = "F+N",
        decimal tensaoV = 127m,
        decimal comprimentoM = 10m,
        TipoDeCarga tipo = TipoDeCarga.TUG,
        decimal temperaturaC = 30m,
        int circuitosAgrupados = 1,
        IReadOnlyList<string?>? locais = null,
        DecisaoDeIdr? idr = null) =>
        new("TUG-01", tipo, potenciaVA, fases, tensaoV, comprimentoM, "B1", "PVC", "Cobre", temperaturaC, circuitosAgrupados,
            CatalogosFicticios.TipoDeCondutor, CatalogosFicticios.TipoDeEletroduto, locais ?? ["LOCAL-SECO"], idr);

    private static PassoDeCalculo PassoDe(ResultadoDoDimensionamento resultado, string descricao) =>
        resultado.Memoria!.Passos.Single(passo => passo.Descricao == descricao);

    private static string Observacoes(ResultadoDoDimensionamento resultado) =>
        string.Join("\n", resultado.Memoria!.Passos.Select(passo => passo.Observacao).OfType<string>());
}
