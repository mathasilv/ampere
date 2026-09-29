using System.Diagnostics;
using Ampere.Core.Parametros;
using Ampere.Parametros;
using Ampere.Revit.Parametros;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Ampere.Commands;

/// <summary>
///     Injeta no projeto os parâmetros compartilhados Ampere (AMP_*): idempotente, com um único desfazer, e sem
///     alterar nada se houver conflito com parâmetros existentes.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class InjetarParametrosCommand : ExternalCommand
{
    // O Revit 2027 já prefixa o nome do add-in ("Ampere - "); repetir "Ampere" aqui duplicaria o título.
    private const string TituloDaJanela = "Injetar parâmetros";

    public override void Execute()
    {
        var documento = Application.ActiveUIDocument?.Document;
        if (documento is null || documento.IsFamilyDocument)
        {
            TaskDialog.Show(TituloDaJanela, "Abra um projeto: a injeção de parâmetros não se aplica a documentos de família.");
            Result = Result.Cancelled;
            return;
        }

        var cronometro = Stopwatch.StartNew();
        var plano = InjecaoDeParametros.Executar(CatalogoDeParametros.Padrao, new ParametrosDoDocumentoRevit(documento));
        cronometro.Stop();

        TaskDialog.Show(TituloDaJanela, ResumoDaInjecao.Texto(plano, cronometro.Elapsed));
        if (plano.TemConflitos) Result = Result.Cancelled;
    }
}
