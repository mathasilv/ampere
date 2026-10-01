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
            .SetToolTip("Grava tipo de carga, potência, fatores e local (IDR) nos elementos selecionados (um único desfazer).");

        circuitos.AddPushButton<LocaisPorAmbienteCommand>("Locais pelos\nambientes")
            .SetImage("/Ampere;component/Resources/Icons/RibbonIcon16.png")
            .SetLargeImage("/Ampere;component/Resources/Icons/RibbonIcon32.png")
            .SetToolTip("Grava o local (IDR) dos pontos classificados pelo ambiente em que estão (Room/Space, inclusive de vínculo): um local por ambiente. Seleção vazia = projeto inteiro (um único desfazer).");

        circuitos.AddPushButton<PrevisaoDeCargasCommand>("Previsão\nde cargas")
            .SetImage("/Ampere;component/Resources/Icons/RibbonIcon16.png")
            .SetLargeImage("/Ampere;component/Resources/Icons/RibbonIcon32.png")
            .SetToolTip("Confere cada cômodo de habitação contra a previsão mínima da NBR 5410 (9.5.2): iluminação pela área, número de tomadas pelo perímetro e potência das tomadas. A categoria de cada ambiente fica guardada no projeto (um único desfazer); o relatório vai para a pasta do projeto.");

        circuitos.AddPushButton<CriarCircuitosCommand>("Criar\ncircuitos")
            .SetImage("/Ampere;component/Resources/Icons/RibbonIcon16.png")
            .SetLargeImage("/Ampere;component/Resources/Icons/RibbonIcon32.png")
            .SetToolTip("Agrupa os pontos selecionados em circuitos numerados no quadro escolhido (um único desfazer).");

        var dimensionamento = Application.CreatePanel("Dimensionamento", "Ampere");

        dimensionamento.AddPushButton<DimensionarCircuitosCommand>("Dimensionar\ncircuitos")
            .SetImage("/Ampere;component/Resources/Icons/RibbonIcon16.png")
            .SetLargeImage("/Ampere;component/Resources/Icons/RibbonIcon32.png")
            .SetToolTip("Dimensiona os circuitos do Ampere pela NBR 5410 (IB, seção, disjuntor, queda de tensão, IDR e eletroduto) com memória de cálculo; grava os resultados nos circuitos (um único desfazer). Com seleção, só os circuitos selecionados (ou dos quadros e pontos selecionados).");

        dimensionamento.AddPushButton<VerificarProjetoCommand>("Verificar\nprojeto")
            .SetImage("/Ampere;component/Resources/Icons/RibbonIcon16.png")
            .SetLargeImage("/Ampere;component/Resources/Icons/RibbonIcon32.png")
            .SetToolTip("Lista o que falta ou mudou, sem alterar o modelo: pontos sem classificação, fora de circuito ou sem local; circuitos fora do Ampere, com dados faltando, não dimensionados ou com memória desatualizada; cômodos abaixo da previsão de cargas e circuitos fora da divisão (NBR 5410, 9.5.2 e 9.5.3), depois da 'Previsão de cargas'. Seleciona no modelo os elementos de uma pendência.");

        var quadros = Application.CreatePanel("Quadros", "Ampere");

        quadros.AddPushButton<MontarQuadroDeCargasCommand>("Montar quadro\nde cargas")
            .SetImage("/Ampere;component/Resources/Icons/RibbonIcon16.png")
            .SetLargeImage("/Ampere;component/Resources/Icons/RibbonIcon32.png")
            .SetToolTip("Monta demanda por tipo de carga e corrente de demanda dos quadros com circuitos; grava potência e fator nos circuitos e a memória no quadro e cria as tabelas, com os resultados do dimensionamento (dois passos no desfazer).");

        quadros.AddPushButton<DimensionarAlimentadoresCommand>("Dimensionar\nalimentadores")
            .SetImage("/Ampere;component/Resources/Icons/RibbonIcon16.png")
            .SetLargeImage("/Ampere;component/Resources/Icons/RibbonIcon32.png")
            .SetToolTip("Dimensiona o circuito que alimenta cada quadro (IB pela demanda do quadro de cargas — fase de maior corrente —, queda que sobra do limite total, disjuntor e eletroduto), com memória de cálculo (um único desfazer). Rode antes 'Dimensionar circuitos' e 'Montar quadro de cargas'.");

        quadros.AddPushButton<DemandaDaEntradaCommand>("Demanda da\nentrada")
            .SetImage("/Ampere;component/Resources/Icons/RibbonIcon16.png")
            .SetLargeImage("/Ampere;component/Resources/Icons/RibbonIcon32.png")
            .SetToolTip("Calcula a demanda da instalação pela norma da distribuidora (CELG CT 04/18, vigência a conferir), com todos os pontos classificados — TUE pelo aparelho —, e o padrão de entrada pela carga instalada (Equatorial NT.00001 rev. 09: disjuntor, ramal, eletroduto, condutor do cliente e aterramento); salva as memórias. Não grava nada no modelo.");

        quadros.AddPushButton<DiagramasUnifilaresCommand>("Diagramas\nunifilares")
            .SetImage("/Ampere;component/Resources/Icons/RibbonIcon16.png")
            .SetLargeImage("/Ampere;component/Resources/Icons/RibbonIcon32.png")
            .SetToolTip("Desenha o diagrama unifilar de cada quadro numa vista de desenho, com os valores do dimensionamento, e salva o SVG (um único desfazer).");
    }
}