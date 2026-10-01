using System.Globalization;
using Ampere.Core.Cargas;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Entradas;

namespace Ampere.Tests.Core.Entradas;

public class NumeroDigitado_Teste
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    [Test]
    [Arguments("0,92", 0.92)]
    [Arguments("1200", 1200)]
    [Arguments(" 127 ", 127)]
    [Arguments("-5", -5)]
    public async Task Numero_em_pt_BR_com_virgula_decimal(string texto, decimal esperado)
    {
        var numero = NumeroDigitado.Interpretar(texto, PtBr);

        await Assert.That(numero.Problema).IsNull();
        await Assert.That(numero.Valor).IsEqualTo(esperado);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Vazio_significa_nao_informado(string? texto)
    {
        var numero = NumeroDigitado.Interpretar(texto, PtBr);

        await Assert.That(numero.Valor).IsNull();
        await Assert.That(numero.Problema).IsNull();
    }

    [Test]
    [Arguments("1.200")]
    [Arguments("0.92")]
    public async Task Separador_de_milhar_e_recusado_em_vez_de_adivinhado(string texto)
    {
        var numero = NumeroDigitado.Interpretar(texto, PtBr);

        await Assert.That(numero.Valor).IsNull();
        await Assert.That(numero.Problema).Contains("sem separador de milhar");
    }

    [Test]
    public async Task Texto_que_nao_e_numero_e_recusado()
    {
        await Assert.That(NumeroDigitado.Interpretar("abc", PtBr).Problema).IsEqualTo("número inválido: 'abc'");
    }

    [Test]
    public async Task Em_en_US_vale_o_ponto_e_a_virgula_e_recusada()
    {
        await Assert.That(NumeroDigitado.Interpretar("0.92", EnUs).Valor).IsEqualTo(0.92m);
        await Assert.That(NumeroDigitado.Interpretar("1,200", EnUs).Problema).Contains("sem separador de milhar");
    }
}

public class EntradaDeClassificacao_Teste
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] Locais = ["Area externa", "Demais locais internos"];

    [Test]
    public async Task Campos_validos_viram_classificacao()
    {
        var entrada = EntradaDeClassificacao.Interpretar(TipoDeCarga.TUE, "5400", "0,95", "220", "2F", PtBr);

        await Assert.That(entrada.Problemas).IsEmpty();
        await Assert.That(entrada.Classificacao).IsEqualTo(new ClassificacaoDeCarga(TipoDeCarga.TUE, 5400m, 0.95m, 220m, "2F"));
    }

    [Test]
    public async Task Campos_vazios_nao_alteram_o_elemento()
    {
        var entrada = EntradaDeClassificacao.Interpretar(TipoDeCarga.TUG, "", " ", null, "", PtBr);

        await Assert.That(entrada.Classificacao).IsEqualTo(new ClassificacaoDeCarga(TipoDeCarga.TUG));
    }

    [Test]
    public async Task Sem_tipo_nao_ha_classificacao()
    {
        var entrada = EntradaDeClassificacao.Interpretar(null, "100", "", "", "", PtBr);

        await Assert.That(entrada.Classificacao).IsNull();
        await Assert.That(string.Join("\n", entrada.Problemas)).Contains("escolha o tipo de carga");
    }

    [Test]
    public async Task Problemas_de_digitacao_e_de_regra_sao_relatados_por_campo()
    {
        var entrada = EntradaDeClassificacao.Interpretar(TipoDeCarga.TUG, "1.200", "2", "abc", "", PtBr);

        await Assert.That(entrada.Classificacao).IsNull();
        var problemas = string.Join("\n", entrada.Problemas);
        await Assert.That(problemas).Contains("potência: número sem separador de milhar");
        await Assert.That(problemas).Contains("tensão: número inválido: 'abc'");
    }

    [Test]
    public async Task Potencia_em_W_com_fator_de_potencia_vira_VA()
    {
        var entrada = EntradaDeClassificacao.Interpretar(TipoDeCarga.TUE, "", "0,92", "220", "2F", PtBr, potenciaW: "5500");

        await Assert.That(entrada.Problemas).IsEmpty();
        await Assert.That(entrada.Classificacao!.PotenciaVA).IsEqualTo(5978.26m);
        await Assert.That(entrada.Classificacao.FatorDePotencia).IsEqualTo(0.92m);
    }

    [Test]
    public async Task Potencia_em_W_sem_fator_de_potencia_e_recusada()
    {
        var entrada = EntradaDeClassificacao.Interpretar(TipoDeCarga.TUE, "", "", "", "", PtBr, potenciaW: "5500");

        await Assert.That(entrada.Classificacao).IsNull();
        await Assert.That(entrada.Problemas).IsEquivalentTo(["potência em W exige o fator de potência (VA = W / FP)"]);
    }

    [Test]
    public async Task Potencia_em_VA_e_em_W_ao_mesmo_tempo_e_recusada()
    {
        var entrada = EntradaDeClassificacao.Interpretar(TipoDeCarga.TUE, "6000", "1", "", "", PtBr, potenciaW: "5500");

        await Assert.That(entrada.Classificacao).IsNull();
        await Assert.That(entrada.Problemas).IsEquivalentTo(["informe a potência em VA ou em W, não as duas"]);
    }

    [Test]
    public async Task Fator_de_potencia_invalido_com_potencia_em_W_cai_na_regra_de_dominio()
    {
        var entrada = EntradaDeClassificacao.Interpretar(TipoDeCarga.TUE, "", "1,5", "", "", PtBr, potenciaW: "5500");

        await Assert.That(entrada.Classificacao).IsNull();
        await Assert.That(string.Join("\n", entrada.Problemas)).Contains("fator de potência deve estar em (0; 1]");
    }

    [Test]
    public async Task Local_da_tabela_do_perfil_entra_na_classificacao()
    {
        var entrada = EntradaDeClassificacao.Interpretar(TipoDeCarga.TUG, "", "", "", "", PtBr, " Area externa ", Locais);

        await Assert.That(entrada.Problemas).IsEmpty();
        await Assert.That(entrada.Classificacao).IsEqualTo(new ClassificacaoDeCarga(TipoDeCarga.TUG, Local: "Area externa"));
    }

    [Test]
    public async Task Local_vazio_nao_altera_o_elemento()
    {
        var entrada = EntradaDeClassificacao.Interpretar(TipoDeCarga.TUG, "", "", "", "", PtBr, "  ", Locais);

        await Assert.That(entrada.Classificacao).IsEqualTo(new ClassificacaoDeCarga(TipoDeCarga.TUG));
    }

    [Test]
    public async Task Local_fora_da_tabela_do_perfil_e_recusado()
    {
        var entrada = EntradaDeClassificacao.Interpretar(TipoDeCarga.TUG, "", "", "", "", PtBr, "Banheiro", Locais);
        var semTabela = EntradaDeClassificacao.Interpretar(TipoDeCarga.TUG, "", "", "", "", PtBr, "Area externa");

        await Assert.That(entrada.Classificacao).IsNull();
        await Assert.That(string.Join("\n", entrada.Problemas)).Contains("local 'Banheiro' fora da tabela de proteção diferencial do perfil");
        await Assert.That(semTabela.Classificacao).IsNull();
    }

    [Test]
    public async Task Regra_de_dominio_e_aplicada_depois_da_digitacao()
    {
        var entrada = EntradaDeClassificacao.Interpretar(TipoDeCarga.TUG, "100", "1,5", "", "", PtBr);

        await Assert.That(entrada.Classificacao).IsNull();
        await Assert.That(string.Join("\n", entrada.Problemas)).Contains("fator de potência deve estar em (0; 1]");
    }
}

public class EntradaDeRegra_Teste
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    [Test]
    public async Task Campos_vazios_sao_regra_sem_limites()
    {
        var entrada = EntradaDeRegra.Interpretar(false, "", "", PtBr);

        await Assert.That(entrada.Problemas).IsEmpty();
        await Assert.That(entrada.Regra).IsEqualTo(new Ampere.Core.Circuitos.RegraDeAgrupamento());
    }

    [Test]
    public async Task Limites_digitados_viram_regra()
    {
        var entrada = EntradaDeRegra.Interpretar(true, "8", "1270,5", PtBr);

        await Assert.That(entrada.Regra).IsEqualTo(new Ampere.Core.Circuitos.RegraDeAgrupamento(true, 8, 1270.5m));
    }

    [Test]
    [Arguments("2,5")]
    [Arguments("99999999999")]
    public async Task Maximo_de_pontos_precisa_ser_inteiro(string maximoDePontos)
    {
        var entrada = EntradaDeRegra.Interpretar(false, maximoDePontos, "", PtBr);

        await Assert.That(entrada.Regra).IsNull();
        await Assert.That(string.Join("\n", entrada.Problemas)).Contains("máximo de pontos deve ser um número inteiro");
    }

    [Test]
    public async Task Regra_invalida_e_relatada()
    {
        var entrada = EntradaDeRegra.Interpretar(false, "0", "", PtBr);

        await Assert.That(entrada.Regra).IsNull();
        await Assert.That(string.Join("\n", entrada.Problemas)).Contains("máximo de pontos por circuito deve ser pelo menos 1");
    }
}

public class EntradaDeCondicoes_Teste
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    [Test]
    public async Task Campos_validos_viram_condicoes_do_projeto()
    {
        var entrada = EntradaDeCondicoes.Interpretar("35", "3", "Cobre", "B1", "PVC", " Fio 750 V ", "PVC rígido", PtBr);

        await Assert.That(entrada.Problemas).IsEmpty();
        await Assert.That(entrada.Condicoes).IsEqualTo(new CondicoesDoProjeto(35m, 3, "Cobre", "B1", "PVC", "Fio 750 V", "PVC rígido"));
    }

    [Test]
    public async Task Padroes_vazios_ficam_nulos()
    {
        var entrada = EntradaDeCondicoes.Interpretar("30", "1", "Cobre", "", " ", null, "", PtBr);

        await Assert.That(entrada.Condicoes).IsEqualTo(new CondicoesDoProjeto(30m, 1, "Cobre"));
    }

    [Test]
    public async Task Temperatura_agrupamento_e_material_sao_obrigatorios()
    {
        var entrada = EntradaDeCondicoes.Interpretar("", " ", "", "B1", "PVC", null, null, PtBr);

        await Assert.That(entrada.Condicoes).IsNull();
        await Assert.That(entrada.Problemas).IsEquivalentTo(
        [
            "informe a temperatura ambiente (°C)",
            "informe os circuitos agrupados (1 = circuito sozinho)",
            "escolha o material do condutor"
        ]);
    }

    [Test]
    [Arguments("0")]
    [Arguments("2,5")]
    [Arguments("-1")]
    public async Task Circuitos_agrupados_precisa_ser_inteiro_de_1_em_diante(string agrupados)
    {
        var entrada = EntradaDeCondicoes.Interpretar("30", agrupados, "Cobre", null, null, null, null, PtBr);

        await Assert.That(entrada.Condicoes).IsNull();
        await Assert.That(string.Join("\n", entrada.Problemas)).Contains("circuitos agrupados deve ser um número inteiro de 1 em diante");
    }

    [Test]
    public async Task Problema_de_digitacao_e_relatado_por_campo()
    {
        var entrada = EntradaDeCondicoes.Interpretar("30.5", "abc", "Cobre", null, null, null, null, PtBr);

        var problemas = string.Join("\n", entrada.Problemas);
        await Assert.That(problemas).Contains("temperatura ambiente: número sem separador de milhar");
        await Assert.That(problemas).Contains("circuitos agrupados: número inválido: 'abc'");
    }
}
