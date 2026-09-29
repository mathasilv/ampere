using Ampere.Core.Cargas;

namespace Ampere.Tests.Core.Cargas;

public class TipoDeCarga_Teste
{
    [Test]
    [Arguments(TipoDeCarga.Iluminacao, "Iluminação")]
    [Arguments(TipoDeCarga.TUG, "TUG")]
    [Arguments(TipoDeCarga.TUE, "TUE")]
    [Arguments(TipoDeCarga.ArCondicionado, "ArCondicionado")]
    [Arguments(TipoDeCarga.Motor, "Motor")]
    [Arguments(TipoDeCarga.Reserva, "Reserva")]
    public async Task Codigo_gravado_em_AMP_TipoCarga_segue_o_catalogo(TipoDeCarga tipo, string codigo)
    {
        await Assert.That(CodigosDeTipoDeCarga.Codigo(tipo)).IsEqualTo(codigo);
        await Assert.That(CodigosDeTipoDeCarga.TryLer(codigo, out var lido)).IsTrue();
        await Assert.That(lido).IsEqualTo(tipo);
    }

    [Test]
    [Arguments(" tug ", TipoDeCarga.TUG)]
    [Arguments("iluminação", TipoDeCarga.Iluminacao)]
    [Arguments("Iluminacao", TipoDeCarga.Iluminacao)]
    [Arguments("arcondicionado", TipoDeCarga.ArCondicionado)]
    public async Task Leitura_tolera_maiusculas_espacos_e_nome_do_enum(string texto, TipoDeCarga esperado)
    {
        await Assert.That(CodigosDeTipoDeCarga.TryLer(texto, out var lido)).IsTrue();
        await Assert.That(lido).IsEqualTo(esperado);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("Tomada")]
    [Arguments("2")]
    public async Task Texto_desconhecido_nao_e_classificado(string? texto)
    {
        await Assert.That(CodigosDeTipoDeCarga.TryLer(texto, out _)).IsFalse();
    }
}
