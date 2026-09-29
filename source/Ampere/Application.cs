using Nice3point.Revit.Toolkit.External;
using Ampere.Commands;

namespace Ampere;

/// <summary>
///     Application entry point
/// </summary>
[UsedImplicitly]
public class Application : ExternalApplication
{
    public override void OnStartup()
    {
        CreateRibbon();
    }

    private void CreateRibbon()
    {
        var parametros = Application.CreatePanel("Parâmetros", "Ampere");

        parametros.AddPushButton<InjetarParametrosCommand>("Injetar\nparâmetros")
            .SetImage("/Ampere;component/Resources/Icons/RibbonIcon16.png")
            .SetLargeImage("/Ampere;component/Resources/Icons/RibbonIcon32.png")
            .SetToolTip("Injeta no projeto os parâmetros compartilhados AMP_* (idempotente, um único desfazer).");

        var panel = Application.CreatePanel("Commands", "Ampere");

        panel.AddPushButton<StartupCommand>("Execute")
            .SetImage("/Ampere;component/Resources/Icons/RibbonIcon16.png")
            .SetLargeImage("/Ampere;component/Resources/Icons/RibbonIcon32.png");
    }
}