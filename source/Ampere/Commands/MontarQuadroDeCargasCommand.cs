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
///     demanda e memória de cálculo. Grava potência e fator aplicado nos circuitos e o hash da memória no quadro, cria as
///     tabelas (dois passos no desfazer: valores e tabelas) e salva os relatórios em Documentos\Ampere\{projeto}.
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
            // Mesmo sem quadros, apaga o que montagens anteriores deixaram (hash em quadro, fator em circuito sem quadro).
            // Result fica Succeeded: com Cancelled, o Revit desfaria essa limpeza.
            try
            {
                QuadroDeCargasDoProjeto.Gravar([], porta);
            }
            catch (Exception excecao) when (excecao is InvalidOperationException or Autodesk.Revit.Exceptions.ApplicationException)
            {
                Cancelar($"Nada foi alterado:{Environment.NewLine}{excecao.Message}");
                return;
            }

            TaskDialog.Show(TituloDaJanela, "Nenhum quadro com circuitos no projeto. Crie os circuitos com o Ampere primeiro.");
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
        GravacaoDosQuadros gravacao;
        try
        {
            gravacao = QuadroDeCargasDoProjeto.Gravar(resultados, porta);
        }
        catch (Exception excecao) when (excecao is InvalidOperationException or Autodesk.Revit.Exceptions.ApplicationException)
        {
            Cancelar($"Nada foi gravado (a operação foi desfeita):{Environment.NewLine}{excecao.Message}");
            return;
        }

        var tabelas = QuadroDeCargasDoProjeto.CriarTabelas(resultados, porta);
        var (pasta, gerados, errosDeDisco) = RelatoriosDosQuadros.Gravar(resultados, Path.GetFileNameWithoutExtension(documento.PathName));
        cronometro.Stop();

        TaskDialog.Show(TituloDaJanela, ResumoDeQuadros.Montagem(
            resultados, PerfilNormativo.NBR5410_2004, cronometro.Elapsed, pasta, gerados, errosDeDisco, gravacao.CircuitosAtualizados, tabelas,
            gravacao.QuadrosSemMemoria));
    }

    private void Cancelar(string mensagem)
    {
        TaskDialog.Show(TituloDaJanela, mensagem);
        Result = Result.Cancelled;
    }
}
