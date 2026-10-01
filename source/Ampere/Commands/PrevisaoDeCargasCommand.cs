using System.IO;
using System.Text;
using System.Windows.Interop;
using Ampere.Core.Previsao;
using Ampere.Relatorios;
using Ampere.Revit.Dimensionamento;
using Ampere.Revit.Previsao;
using Ampere.ViewModels;
using Ampere.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Ampere.Commands;

/// <summary>
///     Confere os cômodos de habitação contra a previsão mínima de cargas da NBR 5410 (9.5.2): o projetista escolhe a
///     categoria de cada nome de ambiente, guardada no projeto (um único desfazer); o relatório (Markdown e CSV) vai para
///     Documentos\Ampere\{projeto}\Previsao. Os pontos e os parâmetros não mudam.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class PrevisaoDeCargasCommand : ExternalCommand
{
    // O Revit 2027 já prefixa o nome do add-in ("Ampere - ").
    private const string TituloDaJanela = "Previsão de cargas";
    private const string Subpasta = "Previsao";

    public override void Execute()
    {
        var documento = Application.ActiveUIDocument?.Document;
        if (documento is null || documento.IsFamilyDocument)
        {
            Cancelar("Abra um projeto: a previsão de cargas não se aplica a documentos de família.");
            return;
        }

        if (!new DocumentoDeDimensionamentoRevit(documento).ParametrosInjetados())
        {
            Cancelar("Os parâmetros Ampere deste projeto estão incompletos ou são de uma versão anterior do catálogo. Rode 'Injetar parâmetros' primeiro.");
            return;
        }

        var porta = new DocumentoDePrevisaoRevit(documento);
        var leitura = porta.Ler();
        if (leitura.Comodos.Count == 0)
        {
            Cancelar("Nenhum ambiente (Room do modelo ou dos vínculos, ou Space) colocado no projeto: a previsão é feita por cômodo.");
            return;
        }

        var norma = NormaDePrevisao.NBR5410_2004;
        var viewModel = new PrevisaoViewModel(leitura.Comodos, PrevisaoDeCargas.Categorias(norma), porta.LerCategorias());
        var janela = new PrevisaoView(viewModel);
        _ = new WindowInteropHelper(janela) { Owner = Application.MainWindowHandle };
        if (janela.ShowDialog() != true || viewModel.Escolhas is not { } escolhas)
        {
            Result = Result.Cancelled;
            return;
        }

        ExecucaoDaPrevisao execucao;
        try
        {
            execucao = PrevisaoDeCargas.Executar(leitura, escolhas, norma, porta);
        }
        catch (Exception excecao) when (excecao is InvalidOperationException or Autodesk.Revit.Exceptions.ApplicationException)
        {
            Cancelar($"As categorias não foram gravadas (a operação foi desfeita):{Environment.NewLine}{excecao.Message}");
            return;
        }

        if (execucao.Resultado is not { } resultado)
        {
            Cancelar(string.Join(Environment.NewLine, execucao.Problemas.Select(problema => $"• {problema}")));
            return;
        }

        var (pasta, erros) = Gravar(resultado, Path.GetFileNameWithoutExtension(documento.PathName));
        var texto = new StringBuilder(RelatorioDaPrevisao.Resumo(resultado));
        foreach (var avaliacao in resultado.Comodos.Where(avaliacao => avaliacao.Situacao == SituacaoDoComodo.NaoAtende).Take(8))
            texto.Append($"{Environment.NewLine}• {avaliacao.Comodo.Nome}{(avaliacao.Comodo.Pavimento is { } pavimento ? $" ({pavimento})" : string.Empty)}: {string.Join("; ", avaliacao.Faltas)}");
        if (resultado.Contar(SituacaoDoComodo.NaoAtende) > 8) texto.Append($"{Environment.NewLine}• … e outros (ver o relatório)");
        foreach (var falta in resultado.Divisao.Take(8)) texto.Append($"{Environment.NewLine}• {falta.Circuito}: {falta.Descricao}");
        if (resultado.Divisao.Count > 8) texto.Append($"{Environment.NewLine}• … e outras faltas de divisão (ver o relatório)");
        if (execucao.CategoriasNaoGravadas is { } motivo) texto.Append($"{Environment.NewLine}{Environment.NewLine}As categorias não ficaram no modelo: {motivo}.");
        texto.Append(pasta is null ? string.Empty : $"{Environment.NewLine}{Environment.NewLine}Relatório: {pasta}");
        foreach (var erro in erros) texto.Append($"{Environment.NewLine}Não salvo: {erro}");
        TaskDialog.Show(TituloDaJanela, texto.ToString());
    }

    // Falha de disco não impede mostrar o resultado: o resumo diz o que não foi salvo.
    private static (string? Pasta, IReadOnlyList<string> Erros) Gravar(ResultadoDaPrevisao resultado, string nomeDoProjeto)
    {
        var pasta = PastaDeRelatorios.Caminho(nomeDoProjeto, Subpasta);
        try
        {
            Directory.CreateDirectory(pasta);
            File.WriteAllText(Path.Combine(pasta, "previsao.md"), RelatorioDaPrevisao.Markdown(resultado), new UTF8Encoding(false));
            // Com BOM: o Excel em português só reconhece os acentos do CSV assim.
            File.WriteAllText(Path.Combine(pasta, "previsao.csv"), RelatorioDaPrevisao.Csv(resultado), new UTF8Encoding(true));
            return (pasta, []);
        }
        catch (Exception excecao) when (excecao is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return (null, [$"{pasta}: {excecao.Message}"]);
        }
    }

    private void Cancelar(string mensagem)
    {
        TaskDialog.Show(TituloDaJanela, mensagem);
        Result = Result.Cancelled;
    }
}
