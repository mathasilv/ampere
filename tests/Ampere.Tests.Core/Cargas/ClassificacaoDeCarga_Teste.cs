using Ampere.Core.Cargas;

namespace Ampere.Tests.Core.Cargas;

public class ClassificacaoDeCarga_Teste
{
    [Test]
    public async Task So_o_tipo_e_obrigatorio()
    {
        await Assert.That(new ClassificacaoDeCarga(TipoDeCarga.TUG).Validar()).IsEmpty();
    }

    [Test]
    public async Task Classificacao_completa_e_valida()
    {
        var completa = new ClassificacaoDeCarga(TipoDeCarga.TUE, PotenciaVA: 5400m, FatorDePotencia: 1m, TensaoV: 220m, Fases: "2F");

        await Assert.That(completa.Validar()).IsEmpty();
    }

    [Test]
    public async Task Reserva_nao_classifica_ponto_de_carga()
    {
        await Assert.That(string.Join("\n", new ClassificacaoDeCarga(TipoDeCarga.Reserva).Validar()))
            .Contains("Reserva é tipo de circuito, não de ponto de carga");
    }

    [Test]
    public async Task Tipo_fora_do_enum_e_rejeitado()
    {
        await Assert.That(string.Join("\n", new ClassificacaoDeCarga((TipoDeCarga)99).Validar())).Contains("tipo de carga inválido");
    }

    [Test]
    public async Task Potencia_negativa_e_rejeitada()
    {
        await Assert.That(string.Join("\n", new ClassificacaoDeCarga(TipoDeCarga.TUG, PotenciaVA: -1m).Validar()))
            .Contains("potência não pode ser negativa");
    }

    [Test]
    [Arguments(0)]
    [Arguments(-0.5)]
    [Arguments(1.01)]
    public async Task Fator_de_potencia_fora_de_0_a_1_e_rejeitado(decimal fatorDePotencia)
    {
        await Assert.That(string.Join("\n", new ClassificacaoDeCarga(TipoDeCarga.TUG, FatorDePotencia: fatorDePotencia).Validar()))
            .Contains("fator de potência deve estar em (0; 1]");
    }

    [Test]
    [Arguments(0)]
    [Arguments(-127)]
    public async Task Tensao_nao_positiva_e_rejeitada(decimal tensaoV)
    {
        await Assert.That(string.Join("\n", new ClassificacaoDeCarga(TipoDeCarga.TUG, TensaoV: tensaoV).Validar()))
            .Contains("tensão deve ser positiva");
    }

    [Test]
    [Arguments("F+N")]
    [Arguments("2F")]
    [Arguments("2F+N")]
    [Arguments("3F")]
    [Arguments("3F+N")]
    public async Task Configuracoes_de_fases_aceitas(string fases)
    {
        await Assert.That(new ClassificacaoDeCarga(TipoDeCarga.TUG, Fases: fases).Validar()).IsEmpty();
    }

    [Test]
    public async Task Configuracao_de_fases_desconhecida_e_rejeitada()
    {
        await Assert.That(string.Join("\n", new ClassificacaoDeCarga(TipoDeCarga.TUG, Fases: "4F").Validar()))
            .Contains("fases inválidas: '4F'");
    }

    [Test]
    public async Task Local_e_gravado_como_veio_da_tabela()
    {
        await Assert.That(new ClassificacaoDeCarga(TipoDeCarga.TUG, Local: "Area externa").Validar()).IsEmpty();
    }

    [Test]
    [Arguments("")]
    [Arguments(" Area externa")]
    public async Task Local_vazio_ou_com_espacos_nas_pontas_e_rejeitado(string local)
    {
        await Assert.That(string.Join("\n", new ClassificacaoDeCarga(TipoDeCarga.TUG, Local: local).Validar())).Contains("local inválido");
    }

    [Test]
    public async Task Todos_os_problemas_sao_relatados_juntos()
    {
        var problemas = new ClassificacaoDeCarga(TipoDeCarga.Reserva, PotenciaVA: -1m, FatorDePotencia: 2m).Validar();

        await Assert.That(problemas.Count).IsEqualTo(3);
    }
}
