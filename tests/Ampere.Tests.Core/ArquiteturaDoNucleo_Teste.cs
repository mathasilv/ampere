namespace Ampere.Tests.Core;

/// <summary>
///     Regra inviolável nº 1: Ampere.Core é puro — nenhuma referência à API do Revit.
/// </summary>
public class ArquiteturaDoNucleo_Teste
{
    private static readonly string[] PrefixosProibidos = ["Revit", "Autodesk", "AdWindows", "UIFramework", "Nice3point.Revit"];

    [Test]
    public async Task Nucleo_nao_referencia_a_API_do_Revit()
    {
        // Qualificado: o TUnit importa HookType.Assembly via using static global.
        var referenciasProibidas = System.Reflection.Assembly.Load("Ampere.Core")
            .GetReferencedAssemblies()
            .Select(referencia => referencia.Name ?? string.Empty)
            .Where(nome => PrefixosProibidos.Any(prefixo => nome.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        await Assert.That(referenciasProibidas).IsEmpty();
    }
}
