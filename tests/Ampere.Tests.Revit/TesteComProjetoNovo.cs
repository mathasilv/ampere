using Nice3point.TUnit.Revit;

namespace Ampere.Tests.Revit;

/// <summary>
///     Base dos testes de integração: cada teste recebe um projeto métrico novo, descartado no fim sem salvar.
/// </summary>
public abstract class TesteComProjetoNovo : RevitApiTest
{
    protected Document Documento { get; private set; } = null!;

    [Before(Test)]
    public void AbrirProjetoNovo() => Documento = Application.NewProjectDocument(UnitSystem.Metric);

    [After(Test)]
    public void DescartarProjeto() => Documento.Close(false);
}
