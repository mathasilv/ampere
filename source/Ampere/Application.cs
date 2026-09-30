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

        var circuitos = Application.CreatePanel("Circuitos", "Ampere");

        circuitos.AddPushButton<ClassificarCargasCommand>("Classificar\ncargas")
            .SetImage("/Ampere;component/Resources/Icons/RibbonIcon16.png")
            .SetLargeImage("/Ampere;component/Resources/Icons/RibbonIcon32.png")
            .SetToolTip("Grava tipo de carga, potência e fatores nos elementos selecionados (um único desfazer).");

        circuitos.AddPushButton<CriarCircuitosCommand>("Criar\ncircuitos")
            .SetImage("/Ampere;component/Resources/Icons/RibbonIcon16.png")
            .SetLargeImage("/Ampere;component/Resources/Icons/RibbonIcon32.png")
            .SetToolTip("Agrupa os pontos selecionados em circuitos numerados no quadro escolhido (um único desfazer).");

        var quadros = Application.CreatePanel("Quadros", "Ampere");

        quadros.AddPushButton<MontarQuadroDeCargasCommand>("Montar quadro\nde cargas")
            .SetImage("/Ampere;component/Resources/Icons/RibbonIcon16.png")
            .SetLargeImage("/Ampere;component/Resources/Icons/RibbonIcon32.png")
            .SetToolTip("Monta demanda por tipo de carga e corrente de demanda dos quadros com circuitos (somente leitura).");
    }
}