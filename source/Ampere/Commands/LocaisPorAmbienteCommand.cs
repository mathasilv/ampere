using System.Windows.Interop;
using Ampere.Circuitos;
using Ampere.Core.Locais;
using Ampere.Core.Normas;
using Ampere.Revit.Circuitos;
using Ampere.Revit.Locais;
using Ampere.ViewModels;
using Ampere.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Ampere.Commands;

/// <summary>
///     Grava AMP_Local nos pontos classificados pelo ambiente (Room/Space, inclusive de vínculo) em que estão: o projetista
///     escolhe um local por nome de ambiente. Com seleção, só os pontos selecionados; sem, todos os do projeto. Um único
///     desfazer.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class LocaisPorAmbienteCommand : ExternalCommand
{
    // O Revit 2027 já prefixa o nome do add-in ("Ampere - ").
    private const string TituloDaJanela = "Locais pelos ambientes";

    // Escolhas da sessão do Revit por nome de ambiente: a segunda rodada vem preenchida.
    private static readonly Dictionary<string, string> EscolhasAnteriores = new(StringComparer.OrdinalIgnoreCase);

    public override void Execute()
    {
        var uiDocumento = Application.ActiveUIDocument;
        var documento = uiDocumento?.Document;
        if (documento is null || documento.IsFamilyDocument)
        {
            Cancelar("Abra um projeto: os locais não se aplicam a documentos de família.");
            return;
        }

        if (!new DocumentoEletricoRevit(documento).ParametrosInjetados())
        {
            Cancelar("Os parâmetros Ampere deste projeto estão incompletos ou são de uma versão anterior do catálogo. Rode 'Injetar parâmetros' primeiro.");
            return;
        }

        var porta = new DocumentoDeAmbientesRevit(documento);
        var selecionados = uiDocumento!.Selection.GetElementIds().Select(id => id.Value).ToList();
        var pontos = porta.LerPontos(selecionados);
        var ambientes = LocaisPorAmbiente.Agrupar(pontos);
        if (ambientes.Count == 0)
        {
            Cancelar(pontos.Count == 0
                ? "Nenhum ponto classificado (com tipo de carga) na seleção ou no projeto. Rode 'Classificar cargas' primeiro."
                : $"Nenhum dos {pontos.Count} ponto(s) está dentro de um ambiente (Room ou Space). Coloque os ambientes ou informe o local pelo 'Classificar cargas'.");
            return;
        }

        var perfil = PerfilNormativo.NBR5410_2004;
        var viewModel = new LocaisPorAmbienteViewModel(ambientes, perfil.Vocabulario.Locais,
            pontos.Count(ponto => string.IsNullOrWhiteSpace(ponto.Ambiente)), pontos.Count(ponto => !ponto.Editavel), EscolhasAnteriores);
        var janela = new LocaisPorAmbienteView(viewModel);
        _ = new WindowInteropHelper(janela) { Owner = Application.MainWindowHandle };
        if (janela.ShowDialog() != true || viewModel.Escolhas is not { } escolhas)
        {
            Result = Result.Cancelled;
            return;
        }

        ResultadoDosLocais resultado;
        try
        {
            resultado = LocaisPorAmbiente.Aplicar(pontos, escolhas, perfil.Vocabulario.Locais, porta);
        }
        catch (Exception excecao) when (excecao is InvalidOperationException or Autodesk.Revit.Exceptions.ApplicationException)
        {
            Cancelar($"Nenhum local foi gravado (a operação foi desfeita):{Environment.NewLine}{excecao.Message}");
            return;
        }

        if (resultado.Problemas.Count == 0)
        {
            foreach (var (ambiente, local) in escolhas) EscolhasAnteriores[ambiente] = local;
        }

        TaskDialog.Show(TituloDaJanela, ResumoDeCircuitos.Locais(resultado));
    }

    private void Cancelar(string mensagem)
    {
        TaskDialog.Show(TituloDaJanela, mensagem);
        Result = Result.Cancelled;
    }
}
