using System.Windows.Interop;
using Ampere.Circuitos;
using Ampere.Core.Cargas;
using Ampere.Revit.Circuitos;
using Ampere.ViewModels;
using Ampere.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Ampere.Commands;

/// <summary>
///     Grava tipo de carga, potência e fatores nos elementos selecionados, com um único desfazer.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class ClassificarCargasCommand : ExternalCommand
{
    // O Revit 2027 já prefixa o nome do add-in ("Ampere - ").
    private const string TituloDaJanela = "Classificar cargas";

    public override void Execute()
    {
        var uiDocumento = Application.ActiveUIDocument;
        var documento = uiDocumento?.Document;
        if (documento is null || documento.IsFamilyDocument)
        {
            Cancelar("Abra um projeto: a classificação não se aplica a documentos de família.");
            return;
        }

        var porta = new DocumentoEletricoRevit(documento);
        if (!porta.ParametrosInjetados())
        {
            Cancelar("Os parâmetros Ampere ainda não estão neste projeto. Rode 'Injetar parâmetros' primeiro.");
            return;
        }

        var ids = uiDocumento!.Selection.GetElementIds().Select(id => id.Value).ToList();
        if (ids.Count == 0)
        {
            Cancelar("Selecione as luminárias, tomadas ou equipamentos a classificar e rode o comando de novo.");
            return;
        }

        var viewModel = new ClassificacaoViewModel(ids.Count);
        var janela = new ClassificacaoView(viewModel);
        _ = new WindowInteropHelper(janela) { Owner = Application.MainWindowHandle };
        if (janela.ShowDialog() != true || viewModel.Classificacao is null)
        {
            Result = Result.Cancelled;
            return;
        }

        var resultado = ClassificacaoEmLote.Executar(ids, viewModel.Classificacao, porta);
        TaskDialog.Show(TituloDaJanela, ResumoDeCircuitos.Classificacao(resultado));
    }

    private void Cancelar(string mensagem)
    {
        TaskDialog.Show(TituloDaJanela, mensagem);
        Result = Result.Cancelled;
    }
}
