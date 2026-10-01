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
///     Dimensiona os circuitos do Ampere contra o perfil NBR 5410:2004: corrente de projeto, seção, disjuntor, queda de
///     tensão, IDR e eletroduto, com memória de cálculo e as decisões do projetista de cada circuito. Com seleção, só os
///     circuitos selecionados (ou os dos quadros e pontos selecionados); sem seleção, todos os do projeto. Grava os
///     resultados nos circuitos e as condições usadas no modelo (um único desfazer; o que não foi calculado apaga o valor
///     anterior) e salva as memórias, a planilha e a lista de materiais em Documentos\Ampere\{projeto}\Circuitos. O diálogo
///     abre com as condições guardadas no modelo.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class DimensionarCircuitosCommand : ExternalCommand
{
    // O Revit 2027 já prefixa o nome do add-in ("Ampere - ").
    private const string TituloDaJanela = "Dimensionar circuitos";

    // Condições da última rodada nesta sessão do Revit: o diálogo volta preenchido quando o projeto ainda não tem as suas.
    private static CondicoesDoProjeto? _ultimasCondicoes;

    public override void Execute()
    {
        var uiDocumento = Application.ActiveUIDocument;
        var documento = uiDocumento?.Document;
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

        var selecionados = uiDocumento!.Selection.GetElementIds().Select(id => id.Value).ToList();
        var daSelecao = selecionados.Count > 0;
        var circuitos = daSelecao ? porta.CircuitosDaSelecao(selecionados) : porta.ListarCircuitos();
        if (circuitos.Count == 0)
        {
            Cancelar(daSelecao
                ? "A seleção não tem circuitos do Ampere (nem quadros ou pontos com eles). Limpe a seleção para dimensionar o projeto todo."
                : "Nenhum circuito do Ampere no projeto. Crie os circuitos com 'Criar circuitos' primeiro.");
            return;
        }

        var perfil = PerfilNormativo.NBR5410_2004;
        var catalogos = CatalogosDeProduto.Padrao;
        var viewModel = new DimensionamentoViewModel(circuitos.Count, daSelecao, perfil.Vocabulario, catalogos.Condutores.Tipos, catalogos.Eletrodutos.Tipos,
            porta.LerCondicoes() ?? _ultimasCondicoes);
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
            resultados = DimensionamentoDoProjeto.Executar(circuitos, condicoes, perfil, catalogos, porta, doProjetoTodo: !daSelecao);
        }
        catch (Exception excecao) when (excecao is InvalidOperationException or Autodesk.Revit.Exceptions.ApplicationException)
        {
            Cancelar($"Nada foi gravado (a operação foi desfeita):{Environment.NewLine}{excecao.Message}");
            return;
        }

        var gravacao = RelatoriosDosCircuitos.Gravar(resultados, Path.GetFileNameWithoutExtension(documento.PathName), daSelecao);
        cronometro.Stop();

        var texto = ResumoDoDimensionamento.Texto(resultados, perfil, cronometro.Elapsed, gravacao);
        if (condicoes.CorrenteDeCurtoCircuitoKa is null)
            texto += $"{Environment.NewLine}{Environment.NewLine}Sem o curto-circuito presumido nas condições: a capacidade de interrupção dos disjuntores e a integral de Joule dos condutores (NBR 5410, 5.3.5.5) não foram verificadas.";
        if (porta.CondicoesNaoGravadas is { } motivo)
            texto += $"{Environment.NewLine}{Environment.NewLine}As condições do projeto não foram atualizadas no modelo ({motivo}): o diálogo continua abrindo com as anteriores. As de cada circuito foram guardadas com os resultados.";
        TaskDialog.Show(TituloDaJanela, texto);
    }

    private void Cancelar(string mensagem)
    {
        TaskDialog.Show(TituloDaJanela, mensagem);
        Result = Result.Cancelled;
    }
}
