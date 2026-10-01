using System.Diagnostics;
using System.Windows.Interop;
using Ampere.Circuitos;
using Ampere.Core.Circuitos;
using Ampere.Core.Previsao;
using Ampere.Revit.Circuitos;
using Ampere.Revit.Previsao;
using Ampere.ViewModels;
using Ampere.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Ampere.Commands;

/// <summary>
///     Agrupa os pontos selecionados em circuitos numerados no quadro escolhido, com um único desfazer. Se o Revit
///     recusar qualquer circuito, nada é criado.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class CriarCircuitosCommand : ExternalCommand
{
    // O Revit 2027 já prefixa o nome do add-in ("Ampere - ").
    private const string TituloDaJanela = "Criar circuitos";

    public override void Execute()
    {
        var uiDocumento = Application.ActiveUIDocument;
        var documento = uiDocumento?.Document;
        if (documento is null || documento.IsFamilyDocument)
        {
            Cancelar("Abra um projeto: a criação de circuitos não se aplica a documentos de família.");
            return;
        }

        var porta = new DocumentoEletricoRevit(documento);
        if (!porta.ParametrosInjetados())
        {
            Cancelar("Os parâmetros Ampere deste projeto estão incompletos ou são de uma versão anterior do catálogo. Rode 'Injetar parâmetros' primeiro.");
            return;
        }

        var ids = uiDocumento!.Selection.GetElementIds().Select(id => id.Value).ToList();
        if (ids.Count == 0)
        {
            Cancelar("Selecione os pontos de carga já classificados e rode o comando de novo.");
            return;
        }

        var quadros = porta.ListarQuadros();
        if (quadros.Count == 0)
        {
            Cancelar("O projeto não tem quadro elétrico (equipamento elétrico com conector de força).");
            return;
        }

        var viewModel = new CriacaoDeCircuitosViewModel(ids.Count, quadros);
        var janela = new CriacaoDeCircuitosView(viewModel);
        _ = new WindowInteropHelper(janela) { Owner = Application.MainWindowHandle };
        if (janela.ShowDialog() != true || viewModel is not { Quadro: { } quadro, RegrasInterpretadas: { } regras, Numeracao: { } numeracao })
        {
            Result = Result.Cancelled;
            return;
        }

        var cronometro = Stopwatch.StartNew();
        PlanoDeCircuitos plano;
        try
        {
            plano = CriacaoDeCircuitos.Executar(ids, quadro.Id, regras, numeracao, porta, Divisao(documento));
        }
        catch (Exception excecao) when (excecao is InvalidOperationException or Autodesk.Revit.Exceptions.ApplicationException)
        {
            Cancelar($"Nenhum circuito foi criado (a operação foi desfeita):{Environment.NewLine}{excecao.Message}");
            return;
        }

        cronometro.Stop();
        TaskDialog.Show(TituloDaJanela, ResumoDeCircuitos.Criacao(plano, cronometro.Elapsed));
    }

    private void Cancelar(string mensagem)
    {
        TaskDialog.Show(TituloDaJanela, mensagem);
        Result = Result.Cancelled;
    }

    // A divisão da instalação (NBR 5410, 9.5.3) sai das categorias que a 'Previsão de cargas' guardou; sem elas, nada muda.
    private static DivisaoDaInstalacao? Divisao(Document documento)
    {
        var previsao = new DocumentoDePrevisaoRevit(documento);
        var categorias = previsao.LerCategorias();
        return categorias.Count == 0 ? null : PrevisaoDeCargas.DivisaoParaCircuitos(previsao.Ler(), categorias, NormaDePrevisao.NBR5410_2004);
    }
}
