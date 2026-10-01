using System.Globalization;
using Ampere.Core.Cargas;
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
