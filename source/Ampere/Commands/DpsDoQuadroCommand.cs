using System.IO;
using System.Text;
using System.Windows.Interop;
using Ampere.Core.Relatorios;
using Ampere.Core.Surtos;
using Ampere.Relatorios;
using Ampere.Revit.Surtos;
using Ampere.ViewModels;
using Ampere.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Ampere.Commands;

/// <summary>
///     Seleciona os DPS da linha de energia no quadro de entrada ou de distribuição principal (NBR 5410, 6.3.5.2): esquema
///     de conexão, Uc, Up, In ou Iimp e condutor de conexão, pelas tensões do sistema de distribuição do quadro. Não grava
///     nada no modelo; salva a memória (JSON, Markdown e PDF) em Documentos\Ampere\{projeto}\DPS.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.ReadOnly)]
public class DpsDoQuadroCommand : ExternalCommand
{
    // O Revit 2027 já prefixa o nome do add-in ("Ampere - ").
    private const string TituloDaJanela = "DPS do quadro";
    private const string Subpasta = "DPS";

    // As escolhas da última rodada, para abrir o diálogo com elas na mesma sessão do Revit.
    private static string? _ultimoQuadro;
    private static EscolhaDoDps? _ultimaEscolha;

    public override void Execute()
    {
        var documento = Application.ActiveUIDocument?.Document;
        if (documento is null || documento.IsFamilyDocument)
        {
            Cancelar("Abra um projeto: os DPS não se aplicam a documentos de família.");
            return;
        }

        var quadros = new DocumentoDeDpsRevit(documento).LerQuadros();
        if (quadros.Count == 0)
        {
            Cancelar("Nenhum quadro no modelo: ponha o quadro de entrada (ou o de distribuição principal) com um sistema de distribuição.");
            return;
        }

        var viewModel = new DpsViewModel(quadros, _ultimoQuadro, _ultimaEscolha);
        var janela = new DpsView(viewModel);
        _ = new WindowInteropHelper(janela) { Owner = Application.MainWindowHandle };
        if (janela.ShowDialog() != true || viewModel.Resultado is not { } escolhido)
        {
            Result = Result.Cancelled;
            return;
        }

        var (quadro, escolha) = escolhido;

        _ultimoQuadro = quadro.Nome;
        _ultimaEscolha = escolha;
        var norma = NormaDeDps.NBR5410_2004;
        var resultado = SelecaoDeDps.Selecionar(quadro, escolha, norma);
        if (resultado.Memoria is null)
        {
            Cancelar(resultado.Resumo());
            return;
        }

        var (pasta, erros) = Gravar(resultado, norma, Path.GetFileNameWithoutExtension(documento.PathName));
        var texto = new StringBuilder(resultado.Resumo());
        texto.Append("\n\n").Append(pasta is null ? "A memória não foi salva." : $"Memória em {pasta}.");
        if (erros.Count > 0) texto.Append('\n').Append(string.Join("\n", erros.Select(erro => $"• {erro}")));
        texto.Append("\nConfira no catálogo do DPS os itens da lista 'A conferir' do relatório.");
        TaskDialog.Show(TituloDaJanela, texto.ToString());
    }

    // Falha de disco não impede mostrar o resultado: o resumo diz o que não foi salvo.
    private static (string? Pasta, IReadOnlyList<string> Erros) Gravar(ResultadoDoDps resultado, NormaDeDps norma, string nomeDoProjeto)
    {
        var erros = new List<string>();
        var pasta = PastaDeRelatorios.Caminho(nomeDoProjeto, Subpasta);
        var memoria = resultado.Memoria!;
        var baseNome = $"dps-{PastaDeRelatorios.NomeDeArquivo(resultado.Quadro.Nome)}-{PastaDeRelatorios.Prefixo(memoria.Hash())}";
        var pdf = RelatorioEmPdf.Gerar(() => RelatorioDeMemoria.PdfDoDps(resultado, norma), erros);
        try
        {
            Directory.CreateDirectory(pasta);
            File.WriteAllText(Path.Combine(pasta, baseNome + ".json"), memoria.JsonCanonico(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(pasta, baseNome + ".md"), RelatorioDeMemoria.MarkdownDoDps(resultado, norma), new UTF8Encoding(false));
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
