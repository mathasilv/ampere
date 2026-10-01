using System.IO;
using System.Text;
using System.Windows.Interop;
using Ampere.Core.Cargas;
using Ampere.Core.Demanda;
using Ampere.Core.Relatorios;
using Ampere.Demanda;
using Ampere.Relatorios;
using Ampere.Revit.Demanda;
using Ampere.Revit.Dimensionamento;
using Ampere.ViewModels;
using Ampere.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Ampere.Commands;

/// <summary>
///     Calcula a demanda da instalação pela norma da distribuidora (hoje a CELG CT 04/18), com todos os pontos classificados
///     do modelo, sem gravar nada nele. Pede a edificação e, com motores, a regra deles; salva a memória (JSON, Markdown e
///     PDF) em Documentos\Ampere\{projeto}\Demanda. Ponto que impede o cálculo pode ser selecionado no modelo.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.ReadOnly)]
public class DemandaDaEntradaCommand : ExternalCommand
{
    // O Revit 2027 já prefixa o nome do add-in ("Ampere - ").
    private const string TituloDaJanela = "Demanda da entrada";
    private const string Subpasta = "Demanda";

    // As escolhas da última rodada, para abrir o diálogo com elas na mesma sessão do Revit.
    private static OpcoesDaDemanda? _ultimas;

    public override void Execute()
    {
        var uiDocumento = Application.ActiveUIDocument;
        var documento = uiDocumento?.Document;
        if (documento is null || documento.IsFamilyDocument)
        {
            Cancelar("Abra um projeto: a demanda não se aplica a documentos de família.");
            return;
        }

        if (!new DocumentoDeDimensionamentoRevit(documento).ParametrosInjetados())
        {
            Cancelar("Os parâmetros Ampere deste projeto estão incompletos ou são de uma versão anterior do catálogo (a demanda usa AMP_Aparelho). Rode 'Injetar parâmetros' primeiro.");
            return;
        }

        var perfil = PerfilDeDemanda.CelgCt04_18;
        var pontos = new DocumentoDeDemandaRevit(documento).LerPontos();
        var motores = pontos.Count(ponto => CodigosDeTipoDeCarga.TryLer(ponto.TipoDeCarga, out var tipo) && tipo == TipoDeCarga.Motor);

        var viewModel = new DemandaViewModel(perfil, motores, _ultimas);
        var janela = new DemandaView(viewModel);
        _ = new WindowInteropHelper(janela) { Owner = Application.MainWindowHandle };
        if (janela.ShowDialog() != true || viewModel.Opcoes is not { } opcoes)
        {
            Result = Result.Cancelled;
            return;
        }

        _ultimas = opcoes;
        var resultado = DemandaDaEntrada.Calcular(pontos, opcoes, perfil);
        if (resultado.Memoria is null)
        {
            MostrarProblemas(uiDocumento!, resultado);
            return;
        }

        var (pasta, erros) = Gravar(resultado, perfil, Path.GetFileNameWithoutExtension(documento.PathName));
        TaskDialog.Show(TituloDaJanela, ResumoDaDemanda.Texto(resultado, perfil, opcoes, pasta, erros));
    }

    // O que impediu, e a opção de selecionar no modelo os pontos responsáveis.
    private void MostrarProblemas(UIDocument uiDocumento, ResultadoDaDemanda resultado)
    {
        var dialogo = new TaskDialog(TituloDaJanela)
        {
            MainInstruction = "A demanda não foi calculada",
            MainContent = string.Join(Environment.NewLine, resultado.Problemas.Select(problema => $"• {problema}")),
            CommonButtons = TaskDialogCommonButtons.Close
        };
        if (resultado.PontosComProblema.Count > 0)
            dialogo.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, $"Selecionar no modelo os {resultado.PontosComProblema.Count} ponto(s)");
        if (dialogo.Show() == TaskDialogResult.CommandLink1)
            uiDocumento.Selection.SetElementIds(resultado.PontosComProblema.Select(id => new ElementId(id)).ToList());
        Result = Result.Cancelled;
    }

    // Falha de disco não impede mostrar o resultado: o resumo diz o que não foi salvo.
    private static (string? Pasta, IReadOnlyList<string> Erros) Gravar(ResultadoDaDemanda resultado, PerfilDeDemanda perfil, string nomeDoProjeto)
    {
        var erros = new List<string>();
        var pasta = PastaDeRelatorios.Caminho(nomeDoProjeto, Subpasta);
        var hash = resultado.Memoria!.Hash();
        var baseNome = $"demanda-{PastaDeRelatorios.Prefixo(hash)}";
        var pdf = RelatorioEmPdf.Gerar(() => RelatorioDeMemoria.PdfDaDemanda(resultado, perfil), erros);
        try
        {
            Directory.CreateDirectory(pasta);
            File.WriteAllText(Path.Combine(pasta, baseNome + ".json"), resultado.Memoria.JsonCanonico(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(pasta, baseNome + ".md"), RelatorioDeMemoria.MarkdownDaDemanda(resultado, perfil), new UTF8Encoding(false));
            if (pdf is not null) File.WriteAllBytes(Path.Combine(pasta, baseNome + ".pdf"), pdf);
            return (pasta, erros);
        }
        catch (Exception excecao) when (excecao is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            erros.Add($"{pasta}: {excecao.Message}");
            return (null, erros);
        }
    }

    private void Cancelar(string mensagem)
    {
        TaskDialog.Show(TituloDaJanela, mensagem);
        Result = Result.Cancelled;
    }
}
