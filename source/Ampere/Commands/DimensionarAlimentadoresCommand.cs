using System.Diagnostics;
using System.IO;
using Ampere.Alimentadores;
using Ampere.Core;
using Ampere.Core.Alimentadores;
using Ampere.Core.Catalogos;
using Ampere.Core.Normas;
using Ampere.Core.Relatorios;
using Ampere.Dimensionamento;
using Ampere.Relatorios;
using Ampere.Revit.Alimentadores;
using Ampere.Revit.Quadros;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Ampere.Commands;

/// <summary>
///     Dimensiona o circuito que alimenta cada quadro (IB pela demanda do quadro de cargas, queda de tensão que sobra do
///     limite total, disjuntor e eletroduto), com memória de cálculo. Pede a origem da instalação (o limite de queda
///     total vem do perfil), grava os resultados nos alimentadores (um único desfazer) e salva as memórias, a planilha
///     (alimentadores.csv) e a lista de materiais (materiais.csv) em Documentos\Ampere\{projeto}\Alimentadores.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class DimensionarAlimentadoresCommand : ExternalCommand
{
    // O Revit 2027 já prefixa o nome do add-in ("Ampere - ").
    private const string TituloDaJanela = "Dimensionar alimentadores";
    private const string Subpasta = "Alimentadores";
    private const string NomeDaPlanilha = "alimentadores.csv";

    public override void Execute()
    {
        var documento = Application.ActiveUIDocument?.Document;
        if (documento is null || documento.IsFamilyDocument)
        {
            Cancelar("Abra um projeto: o dimensionamento não se aplica a documentos de família.");
            return;
        }

        var porta = new DocumentoDeAlimentadoresRevit(documento);
        if (!porta.ParametrosInjetados())
        {
            Cancelar("Os parâmetros Ampere deste projeto estão incompletos ou são de uma versão anterior do catálogo. Rode 'Injetar parâmetros' primeiro.");
            return;
        }

        var perfil = PerfilNormativo.NBR5410_2004;
        if (EscolherOrigem(perfil) is not { } origem)
        {
            Result = Result.Cancelled;
            return;
        }

        var cronometro = Stopwatch.StartNew();
        IReadOnlyList<ResultadoDoAlimentador> resultados;
        try
        {
            resultados = DimensionamentoDeAlimentadores.Executar(origem, perfil, CatalogosDeProduto.Padrao, porta, new DocumentoDeQuadrosRevit(documento));
        }
        catch (Exception excecao) when (excecao is InvalidOperationException or Autodesk.Revit.Exceptions.ApplicationException)
        {
            Cancelar($"Nada foi gravado (a operação foi desfeita):{Environment.NewLine}{excecao.Message}");
            return;
        }

        var (pasta, gerados, erros) = GravarRelatorios(resultados, Path.GetFileNameWithoutExtension(documento.PathName));
        cronometro.Stop();
        TaskDialog.Show(TituloDaJanela, ResumoDosAlimentadores.Texto(resultados, origem, cronometro.Elapsed, pasta, gerados, erros));
    }

    // A origem decide o limite de queda total (tabela do perfil): uma opção por origem com o valor do perfil.
    private static string? EscolherOrigem(PerfilNormativo perfil)
    {
        var dialogo = new TaskDialog(TituloDaJanela)
        {
            MainInstruction = "De onde vem a energia da instalação?",
            MainContent = "Decide o limite de queda de tensão total; o alimentador fica com o que sobra depois da maior queda dos circuitos do quadro.",
            CommonButtons = TaskDialogCommonButtons.Cancel
        };
        var opcoes = new[]
        {
            (Origem: "ponto_de_entrega", Link: TaskDialogCommandLinkId.CommandLink1, Resultado: TaskDialogResult.CommandLink1),
            (Origem: "transformador_proprio", Link: TaskDialogCommandLinkId.CommandLink2, Resultado: TaskDialogResult.CommandLink2),
            (Origem: "transformador_da_distribuidora", Link: TaskDialogCommandLinkId.CommandLink4, Resultado: TaskDialogResult.CommandLink4),
            (Origem: "gerador", Link: TaskDialogCommandLinkId.CommandLink3, Resultado: TaskDialogResult.CommandLink3)
        };
        foreach (var (origem, link, _) in opcoes)
        {
            var limite = perfil.QuedaDeTensaoMaximaPct(origem);
            var texto = DimensionamentoDeAlimentadores.Origens[origem];
            dialogo.AddCommandLink(link, char.ToUpperInvariant(texto[0]) + texto[1..],
                limite.Disponivel ? $"queda total até {NumeroEmTexto.Formatar(limite.Valor)}% ({limite.Referencia})" : $"limite indisponível no perfil: {limite.Ausencia}");
        }

        var escolha = dialogo.Show();
        return opcoes.Where(opcao => opcao.Resultado == escolha).Select(opcao => opcao.Origem).FirstOrDefault();
    }

    // Memórias dos alimentadores calculados e, de todos (calculados ou não), a planilha e a lista de materiais: os mesmos
    // formatos dos circuitos terminais, refeitos a cada rodada.
    private static (string? Pasta, int Gerados, IReadOnlyList<string> Erros) GravarRelatorios(IReadOnlyList<ResultadoDoAlimentador> resultados, string nomeDoProjeto)
    {
        var circuitos = resultados.Select(resultado => resultado.Circuito).OfType<Ampere.Core.Dimensionamento.ResultadoDoCircuito>().ToList();
        if (circuitos.Count == 0) return (null, 0, []);

        var pasta = PastaDeRelatorios.Caminho(nomeDoProjeto, Subpasta);
        try
        {
            Directory.CreateDirectory(pasta);
        }
        catch (Exception excecao) when (excecao is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return (null, 0, [$"não foi possível criar a pasta {pasta}: {excecao.Message}"]);
        }

        var erros = new List<string>();
        RelatoriosDosCircuitos.GravarCsv(pasta, NomeDaPlanilha, PlanilhaDeCircuitos.Csv(circuitos), erros);
        RelatoriosDosCircuitos.GravarCsv(pasta, RelatoriosDosCircuitos.NomeDosMateriais, ListaDeMateriais.Montar(circuitos).Csv(), erros);
        var gerados = RelatoriosDosCircuitos.GravarMemorias(circuitos, pasta, erros);
        return (pasta, gerados, erros);
    }

    private void Cancelar(string mensagem)
    {
        TaskDialog.Show(TituloDaJanela, mensagem);
        Result = Result.Cancelled;
    }
}
