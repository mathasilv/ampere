using System.Diagnostics;
using System.IO;
using System.Windows.Interop;
using Ampere.Core.Catalogos;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Normas;
using Ampere.Dimensionamento;
using Ampere.Revit.Dimensionamento;
using Ampere.ViewModels;
using Ampere.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Ampere.Commands;

/// <summary>
///     Dimensiona todos os circuitos do Ampere no projeto contra o perfil NBR 5410:2004: corrente de projeto, seção,
///     disjuntor, queda de tensão, IDR e eletroduto, com memória de cálculo. Grava os resultados nos circuitos (um único
///     desfazer; o que não foi calculado apaga o valor anterior) e salva as memórias em Documentos\Ampere\{projeto}\Circuitos.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class DimensionarCircuitosCommand : ExternalCommand
{
    // O Revit 2027 já prefixa o nome do add-in ("Ampere - ").
    private const string TituloDaJanela = "Dimensionar circuitos";

    // Condições da última rodada nesta sessão do Revit: o diálogo volta preenchido.
    private static CondicoesDoProjeto? _ultimasCondicoes;

    public override void Execute()
    {
        var documento = Application.ActiveUIDocument?.Document;
        if (documento is null || documento.IsFamilyDocument)
        {
            Cancelar("Abra um projeto: o dimensionamento não se aplica a documentos de família.");
            return;
        }

        var porta = new DocumentoDeDimensionamentoRevit(documento);
        if (!porta.ParametrosInjetados())
        {
            Cancelar("Os parâmetros Ampere deste projeto estão incompletos ou são de uma versão anterior do catálogo. Rode 'Injetar parâmetros' primeiro.");
            return;
        }

        var circuitos = porta.ListarCircuitos();
        if (circuitos.Count == 0)
        {
            Cancelar("Nenhum circuito do Ampere no projeto. Crie os circuitos com 'Criar circuitos' primeiro.");
            return;
        }

        var perfil = PerfilNormativo.NBR5410_2004;
        var catalogos = CatalogosDeProduto.Padrao;
        var viewModel = new DimensionamentoViewModel(circuitos.Count, perfil.Vocabulario, catalogos.Condutores.Tipos, catalogos.Eletrodutos.Tipos, _ultimasCondicoes);
        var janela = new DimensionamentoView(viewModel);
        _ = new WindowInteropHelper(janela) { Owner = Application.MainWindowHandle };
        if (janela.ShowDialog() != true || viewModel.Condicoes is not { } condicoes)
        {
            Result = Result.Cancelled;
            return;
        }

        _ultimasCondicoes = condicoes;
        var cronometro = Stopwatch.StartNew();
        IReadOnlyList<ResultadoDoCircuito> resultados;
        try
        {
            resultados = DimensionamentoDoProjeto.Executar(circuitos, condicoes, perfil, catalogos, porta);
        }
        catch (Exception excecao) when (excecao is InvalidOperationException or Autodesk.Revit.Exceptions.ApplicationException)
        {
            Cancelar($"Nada foi gravado (a operação foi desfeita):{Environment.NewLine}{excecao.Message}");
            return;
        }

        var (pasta, gerados, errosDeDisco) = RelatoriosDosCircuitos.Gravar(resultados, Path.GetFileNameWithoutExtension(documento.PathName));
        cronometro.Stop();

        TaskDialog.Show(TituloDaJanela, ResumoDoDimensionamento.Texto(resultados, perfil, cronometro.Elapsed, pasta, gerados, errosDeDisco));
    }

    private void Cancelar(string mensagem)
    {
        TaskDialog.Show(TituloDaJanela, mensagem);
        Result = Result.Cancelled;
    }
}
