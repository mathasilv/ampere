using System.IO;
using System.Text;
using Ampere.Core.Catalogos;
using Ampere.Core.Diagramas;
using Ampere.Core.Normas;
using Ampere.Relatorios;
using Ampere.Revit.Diagramas;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Ampere.Commands;

/// <summary>
///     Desenha o diagrama unifilar de cada quadro com circuitos numa vista de desenho "{quadro} — unifilar (Ampere)" (um
///     único desfazer; a vista é reaproveitada e continua nas pranchas) e salva o SVG em Documentos\Ampere\{projeto}.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class DiagramasUnifilaresCommand : ExternalCommand
{
    // O Revit 2027 já prefixa o nome do add-in ("Ampere - ").
    private const string TituloDaJanela = "Diagramas unifilares";

    public override void Execute()
    {
        var documento = Application.ActiveUIDocument?.Document;
        if (documento is null || documento.IsFamilyDocument)
        {
            Cancelar("Abra um projeto: os diagramas não se aplicam a documentos de família.");
            return;
        }

        IReadOnlyList<UnifilarDesenhado> desenhados;
        try
        {
            desenhados = DiagramasDoProjeto.Desenhar(new DocumentoDeDiagramasRevit(documento), PerfilNormativo.NBR5410_2004, CatalogosDeProduto.Padrao);
        }
        catch (Exception excecao) when (excecao is InvalidOperationException or Autodesk.Revit.Exceptions.ApplicationException)
        {
            Cancelar($"Nenhum diagrama foi desenhado (a operação foi desfeita):{Environment.NewLine}{excecao.Message}");
            return;
        }

        if (desenhados.Count == 0)
        {
            Cancelar("Nenhum quadro com circuitos no projeto. Crie os circuitos com o Ampere primeiro.");
            return;
        }

        var texto = new StringBuilder();
        texto.AppendLine($"Diagramas desenhados: {desenhados.Count} (um único desfazer; navegador de projeto, vistas de desenho)");
        foreach (var desenhado in desenhados) texto.AppendLine(desenhado.Vista);
        texto.AppendLine().AppendLine("Os valores são os gravados pelo 'Dimensionar circuitos': rode-o antes para o diagrama ficar completo.");

        var (pasta, erro) = GravarSvgs(desenhados, Path.GetFileNameWithoutExtension(documento.PathName));
        if (pasta is not null) texto.AppendLine().AppendLine("SVG (abre no navegador):").AppendLine(pasta);
        if (erro is not null) texto.AppendLine().AppendLine($"SVG não gravado: {erro}");
        TaskDialog.Show(TituloDaJanela, texto.ToString());
    }

    private static (string? Pasta, string? Erro) GravarSvgs(IReadOnlyList<UnifilarDesenhado> desenhados, string nomeDoProjeto)
    {
        var pasta = PastaDeRelatorios.Caminho(nomeDoProjeto);
        try
        {
            Directory.CreateDirectory(pasta);
            foreach (var desenhado in desenhados)
            {
                File.WriteAllText(Path.Combine(pasta, $"{PastaDeRelatorios.NomeDeArquivo(desenhado.Quadro)}-unifilar.svg"),
                    DiagramaUnifilar.Svg(desenhado.Desenho), new UTF8Encoding(false));
            }

            return (pasta, null);
        }
        catch (Exception excecao) when (excecao is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return (null, excecao.Message);
        }
    }

    private void Cancelar(string mensagem)
    {
        TaskDialog.Show(TituloDaJanela, mensagem);
        Result = Result.Cancelled;
    }
}
