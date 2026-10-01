using System.IO;
using System.Text;
using System.Windows.Interop;
using Ampere.Core.Catalogos;
using Ampere.Core.Normas;
using Ampere.Core.Verificacao;
using Ampere.Relatorios;
using Ampere.Revit.Quadros;
using Ampere.Revit.Verificacao;
using Ampere.ViewModels;
using Ampere.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Ampere.Commands;

/// <summary>
///     Lista o que falta ou mudou no projeto, sem gravar nada no modelo: pontos sem classificação, fora de circuito ou sem
///     local; circuitos criados fora do Ampere, com dados faltando, não dimensionados ou com a memória desatualizada;
///     quadros sem quadro de cargas ou com ele desatualizado; e onde o cálculo para. Salva o relatório em
///     Documentos\Ampere\{projeto}\verificacao.md e pode selecionar no modelo os elementos de uma pendência.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.ReadOnly)]
public class VerificarProjetoCommand : ExternalCommand
{
    // O Revit 2027 já prefixa o nome do add-in ("Ampere - ").
    private const string TituloDaJanela = "Verificar projeto";
    private const string NomeDoRelatorio = "verificacao.md";

    public override void Execute()
    {
        var uiDocumento = Application.ActiveUIDocument;
        var documento = uiDocumento?.Document;
        if (documento is null || documento.IsFamilyDocument)
        {
            Cancelar("Abra um projeto: a verificação não se aplica a documentos de família.");
            return;
        }

        var porta = new DocumentoDeVerificacaoRevit(documento);
        if (!porta.ParametrosInjetados())
        {
            Cancelar("Os parâmetros Ampere deste projeto estão incompletos ou são de uma versão anterior do catálogo. Rode 'Injetar parâmetros' primeiro.");
            return;
        }

        RelatorioDeVerificacao relatorio;
        try
        {
            relatorio = VerificacaoDoProjeto.Executar(porta, PerfilNormativo.NBR5410_2004, CatalogosDeProduto.Padrao, new DocumentoDeQuadrosRevit(documento));
        }
        catch (Exception excecao) when (excecao is InvalidOperationException or Autodesk.Revit.Exceptions.ApplicationException)
        {
            Cancelar($"A verificação não pôde ser concluída:{Environment.NewLine}{excecao.Message}");
            return;
        }

        var arquivo = Salvar(relatorio, Path.GetFileNameWithoutExtension(documento.PathName));

        var viewModel = new VerificacaoViewModel(relatorio, arquivo);
        var janela = new VerificacaoView(viewModel);
        _ = new WindowInteropHelper(janela) { Owner = Application.MainWindowHandle };
        if (janela.ShowDialog() == true && viewModel.ParaSelecionar is { } elementos)
            uiDocumento!.Selection.SetElementIds(elementos.Select(id => new ElementId(id)).ToList());
    }

    // Falha de disco não impede mostrar o resultado: o diálogo diz que o relatório não foi salvo.
    private static string? Salvar(RelatorioDeVerificacao relatorio, string nomeDoProjeto)
    {
        try
        {
            var pasta = PastaDeRelatorios.Caminho(nomeDoProjeto);
            Directory.CreateDirectory(pasta);
            var caminho = Path.Combine(pasta, NomeDoRelatorio);
            File.WriteAllText(caminho, relatorio.Markdown(), new UTF8Encoding(false));
            return caminho;
        }
        catch (Exception excecao) when (excecao is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    private void Cancelar(string mensagem)
    {
        TaskDialog.Show(TituloDaJanela, mensagem);
        Result = Result.Cancelled;
    }
}
