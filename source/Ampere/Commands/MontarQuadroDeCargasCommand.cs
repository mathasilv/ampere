using System.Diagnostics;
using System.IO;
using System.Windows.Interop;
using Ampere.Core.Normas;
using Ampere.Core.Quadros;
using Ampere.Quadros;
using Ampere.Revit.Quadros;
using Ampere.ViewModels;
using Ampere.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Ampere.Commands;

/// <summary>
///     Monta o quadro de cargas de todos os quadros com circuitos do projeto: demanda por tipo de carga, corrente de
///     demanda e memória de cálculo. Grava potência, fator aplicado e o hash da memória nos circuitos (um único
///     desfazer) e salva os relatórios em Documentos\Ampere\{projeto}.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class MontarQuadroDeCargasCommand : ExternalCommand
{
    // O Revit 2027 já prefixa o nome do add-in ("Ampere - ").
    private const string TituloDaJanela = "Montar quadro de cargas";

    public override void Execute()
    {
        var documento = Application.ActiveUIDocument?.Document;
        if (documento is null || documento.IsFamilyDocument)
        {
            Cancelar("Abra um projeto: o quadro de cargas não se aplica a documentos de família.");
            return;
        }

        var porta = new DocumentoDeQuadrosRevit(documento);
        var quadros = porta.LerQuadrosComCircuitos();
        if (quadros.Count == 0)
        {
            Cancelar("Nenhum quadro com circuitos no projeto. Crie os circuitos com o Ampere primeiro.");
            return;
        }

        var viewModel = new QuadroDeCargasViewModel();
        var janela = new QuadroDeCargasView(viewModel);
        _ = new WindowInteropHelper(janela) { Owner = Application.MainWindowHandle };
        if (janela.ShowDialog() != true || viewModel.FatoresInterpretados is not { } fatores)
        {
            Result = Result.Cancelled;
            return;
        }

        var cronometro = Stopwatch.StartNew();
        var resultados = QuadroDeCargasDoProjeto.Executar(porta, PerfilNormativo.NBR5410_2004, fatores);
        int circuitosAtualizados;
        try
        {
            circuitosAtualizados = QuadroDeCargasDoProjeto.Gravar(resultados, porta);
        }
        catch (InvalidOperationException excecao)
        {
            Cancelar($"Nada foi gravado (a operação foi desfeita):{Environment.NewLine}{excecao.Message}");
            return;
        }

        var tabelas = QuadroDeCargasDoProjeto.CriarTabelas(resultados, porta);
        var (pasta, gerados, errosDeDisco) = RelatoriosDosQuadros.Gravar(resultados, Path.GetFileNameWithoutExtension(documento.PathName));
        cronometro.Stop();

        TaskDialog.Show(TituloDaJanela, ResumoDeQuadros.Montagem(
            resultados, PerfilNormativo.NBR5410_2004, cronometro.Elapsed, pasta, gerados, errosDeDisco, circuitosAtualizados, tabelas));
    }

    private void Cancelar(string mensagem)
    {
        TaskDialog.Show(TituloDaJanela, mensagem);
        Result = Result.Cancelled;
    }
}
