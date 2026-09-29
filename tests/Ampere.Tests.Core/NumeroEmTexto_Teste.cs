using System.Globalization;
using Ampere.Core;

namespace Ampere.Tests.Core;

public class NumeroEmTexto_Teste
{
    [Test]
    [Arguments("2.5", "2,5")]
    [Arguments("12.80", "12,8")]
    [Arguments("1270", "1270")]
    [Arguments("25400.5", "25400,5")]
    [Arguments("0.01724", "0,01724")]
    [Arguments("-0.5", "-0,5")]
    [Arguments("0.000", "0")]
    [Arguments("17.320508075688772935274463415", "17,320508075688772935274463415")]
    public async Task Virgula_decimal_sem_milhar_e_sem_zeros_a_direita(string valor, string esperado)
    {
        await Assert.That(NumeroEmTexto.Formatar(decimal.Parse(valor, CultureInfo.InvariantCulture))).IsEqualTo(esperado);
    }

    [Test]
    public async Task Nao_depende_da_cultura_da_maquina()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            await Assert.That(NumeroEmTexto.Formatar(1234.5m)).IsEqualTo("1234,5");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
