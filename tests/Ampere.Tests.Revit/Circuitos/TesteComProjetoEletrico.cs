using Ampere.Revit.Circuitos;
using Nice3point.TUnit.Revit;

namespace Ampere.Tests.Revit.Circuitos;

/// <summary>
///     Base dos testes de circuitos: cada teste recebe um <see cref="CenarioEletrico" /> novo, descartado no fim.
/// </summary>
public abstract class TesteComProjetoEletrico : RevitApiTest
{
    internal CenarioEletrico Cenario { get; private set; } = null!;

    internal DocumentoEletricoRevit Porta { get; private set; } = null!;

    [Before(Test)]
    public void MontarCenario()
    {
        var documento = Application.NewProjectDocument(CenarioEletrico.CaminhoDoTemplate);
        Cenario = CenarioEletrico.Montar(documento);
        Porta = new DocumentoEletricoRevit(documento);
    }

    [After(Test)]
    public void DescartarProjeto() => Cenario.Documento.Close(false);
}
