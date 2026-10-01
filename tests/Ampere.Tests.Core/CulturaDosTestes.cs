using System.Globalization;

namespace Ampere.Tests.Core;

/// <summary>
///     Os testes do núcleo rodam numa cultura "hostil", com separadores que nenhuma cultura real usa: número formatado
///     ou lido pela cultura da máquina quebra o teste em qualquer ambiente — no Windows pt-BR do usuário e no Linux da
///     nuvem. Texto para gente usa NumeroEmTexto (vírgula); dado (JSON, chave, CSV) usa CultureInfo.InvariantCulture;
///     entrada digitada recebe a cultura explicitamente.
/// </summary>
public static class CulturaDosTestes
{
    public static CultureInfo Hostil { get; } = Criar();

    [Before(Assembly)]
    public static void Aplicar()
    {
        CultureInfo.DefaultThreadCurrentCulture = Hostil;
        CultureInfo.DefaultThreadCurrentUICulture = Hostil;
    }

    private static CultureInfo Criar()
    {
        var cultura = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        cultura.NumberFormat.NumberDecimalSeparator = "‹d›";
        cultura.NumberFormat.NumberGroupSeparator = "‹m›";
        cultura.NumberFormat.PercentDecimalSeparator = "‹d›";
        cultura.NumberFormat.CurrencyDecimalSeparator = "‹d›";
        return cultura;
    }
}

public class CulturaDosTestes_Teste
{
    [Test]
    public async Task Testes_rodam_na_cultura_hostil()
    {
        await Assert.That(1.5m.ToString()).IsEqualTo("1‹d›5");
        await Assert.That($"{0.25m}").IsEqualTo("0‹d›25");
    }
}
