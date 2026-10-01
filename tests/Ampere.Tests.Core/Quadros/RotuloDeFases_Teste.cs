using Ampere.Core.Quadros;

namespace Ampere.Tests.Core.Quadros;

public class RotuloDeFases_Teste
{
    private static readonly string[] Abc = ["A", "B", "C"];

    [Test]
    [Arguments("A", "A")]
    [Arguments("A,B", "A|B")]
    [Arguments(" B / C ", "B|C")]
    [Arguments("AB", "A|B")]
    [Arguments("ABC", "A|B|C")]
    [Arguments("A,A", "A")]
    public async Task Le_fases_separadas_ou_juntas(string rotulo, string esperadas)
    {
        await Assert.That(string.Join("|", RotuloDeFases.Ler(rotulo, Abc)!)).IsEqualTo(esperadas);
    }

    [Test]
    [Arguments("D")]
    [Arguments("A,D")]
    [Arguments("ABD")]
    [Arguments("")]
    [Arguments("Fase A")]
    public async Task Rotulo_com_parte_desconhecida_nao_e_lido(string rotulo)
    {
        await Assert.That(RotuloDeFases.Ler(rotulo, Abc)).IsNull();
    }

    [Test]
    public async Task Rotulos_de_mais_de_uma_letra_so_por_partes()
    {
        string[] linhas = ["L1", "L2", "L3"];

        await Assert.That(string.Join("|", RotuloDeFases.Ler("L1,L3", linhas)!)).IsEqualTo("L1|L3");
        await Assert.That(RotuloDeFases.Ler("L1L3", linhas)).IsNull();
    }
}
