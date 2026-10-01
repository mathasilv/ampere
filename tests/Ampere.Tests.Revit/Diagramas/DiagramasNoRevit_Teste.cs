using Ampere.Core.Cargas;
using Ampere.Core.Catalogos;
using Ampere.Core.Circuitos;
using Ampere.Core.Diagramas;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Normas;
using Ampere.Revit.Diagramas;
using Ampere.Revit.Dimensionamento;
using Ampere.Tests.Revit.Circuitos;
using Ampere.Tests.Revit.Parametros;

namespace Ampere.Tests.Revit.Diagramas;

/// <summary>
///     Diagrama unifilar contra o Revit real: leitura dos circuitos dimensionados e desenho numa vista de desenho
///     reaproveitável (continua na prancha).
/// </summary>
[DependsOn(typeof(DesempenhoDaInjecao_Teste), ProceedOnFailure = true)]
public sealed class DiagramasNoRevit_Teste : TesteComProjetoEletrico
{
    private static readonly IReadOnlyDictionary<TipoDeCarga, RegraDeAgrupamento> Regras = new Dictionary<TipoDeCarga, RegraDeAgrupamento>
    {
        [TipoDeCarga.Iluminacao] = new(MaximoDePontos: 3),
        [TipoDeCarga.TUG] = new(MaximoDePontos: 4)
    };

    [Test]
    public async Task Le_os_circuitos_com_os_valores_do_dimensionamento()
    {
        MontarEDimensionar();

        var quadros = new DocumentoDeDiagramasRevit(Cenario.Documento).LerQuadros();

        await Assert.That(quadros.Select(quadro => quadro.Nome)).IsEquivalentTo(["QD1"]);
        var circuitos = quadros[0].Circuitos;
        await Assert.That(string.Join(",", circuitos.Select(circuito => circuito.Numero))).IsEqualTo("IL-01,TUG-01");
        await Assert.That(circuitos.All(circuito => circuito is { DisjuntorA: not null, SecaoMm2: not null, Fases: "F+N", TensaoV: 127m })).IsTrue();
        await Assert.That(circuitos[0].IdrSensibilidadeMa).IsNull();
        await Assert.That(circuitos[1].IdrSensibilidadeMa).IsEqualTo(30m);
        await Assert.That(Math.Round(circuitos[1].PotenciaVA!.Value, 6)).IsEqualTo(720m);
        // QD1 do cenário: 120/208 Wye, sem alimentador no modelo; cada circuito F+N numa das fases.
        await Assert.That(quadros[0].Alimentacao).IsEqualTo("3F+N 208/120 V");
        await Assert.That(quadros[0].Alimentador).IsNull();
        await Assert.That(circuitos.All(circuito => circuito.FasesNoQuadro is { Count: 1 })).IsTrue();
    }

    [Test]
    public async Task Desenha_na_vista_de_desenho_e_redesenha_na_mesma_vista()
    {
        MontarEDimensionar();
        var porta = new DocumentoDeDiagramasRevit(Cenario.Documento);

        var primeiro = DiagramasDoProjeto.Desenhar(porta, PerfilNormativo.NBR5410_2004, CatalogosDeProduto.Padrao);
        var vista = new FilteredElementCollector(Cenario.Documento).OfClass(typeof(ViewDrafting)).Cast<ViewDrafting>()
            .Single(view => view.Name == primeiro[0].Vista);
        Viewport naPrancha;
        using (var transacao = new Transaction(Cenario.Documento, "Prancha do teste"))
        {
            transacao.Start();
            naPrancha = Viewport.Create(Cenario.Documento, ViewSheet.Create(Cenario.Documento, ElementId.InvalidElementId).Id, vista.Id, XYZ.Zero);
            transacao.Commit();
        }

        var segundo = DiagramasDoProjeto.Desenhar(porta, PerfilNormativo.NBR5410_2004, CatalogosDeProduto.Padrao);

        await Assert.That(primeiro[0].Vista).IsEqualTo("QD1 — unifilar (Ampere)");
        await Assert.That(segundo[0].Vista).IsEqualTo(primeiro[0].Vista);
        await Assert.That(vista.IsValidObject && naPrancha.IsValidObject).IsTrue();
        var textos = new FilteredElementCollector(Cenario.Documento, vista.Id).OfCategory(BuiltInCategory.OST_TextNotes).Cast<TextNote>().ToList();
        var linhas = new FilteredElementCollector(Cenario.Documento, vista.Id).OfCategory(BuiltInCategory.OST_Lines).ToElementIds();
        await Assert.That(textos.Count).IsEqualTo(segundo[0].Desenho.Elementos.OfType<Texto>().Count());
        await Assert.That(linhas.Count).IsEqualTo(segundo[0].Desenho.Elementos.OfType<Segmento>().Sum(segmento => segmento.Grosso ? 3 : 1));
        await Assert.That(textos.Any(texto => texto.Text.StartsWith("TUG-01", StringComparison.Ordinal))).IsTrue();
        // Neutro e PE da memória que confere com a gravada (refeita com as condições guardadas no circuito).
        await Assert.That(textos.Any(texto => texto.Text.StartsWith("2,5 mm² (N 2,5 · PE 2,5)", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>QD1 com IL-01 (3 luminárias, demais locais) e TUG-01 (4 tomadas, cozinha), dimensionados.</summary>
    private void MontarEDimensionar()
    {
        var luminarias = Cenario.ColocarLuminarias(3);
        var tomadas = Cenario.ColocarTomadas(4);
        Classificar(luminarias, new ClassificacaoDeCarga(TipoDeCarga.Iluminacao, 62m, TensaoV: 127m, Fases: "F+N", Local: "Demais locais internos"));
        Classificar(tomadas, new ClassificacaoDeCarga(TipoDeCarga.TUG, 180m, TensaoV: 127m, Fases: "F+N", Local: "Cozinha, lavanderia, area de servico ou garagem"));
        CriacaoDeCircuitos.Executar([.. luminarias, .. tomadas], Cenario.Quadro.Id.Value, Regras, ConfiguracaoDeNumeracao.Padrao, Porta);
        var dimensionamento = new DocumentoDeDimensionamentoRevit(Cenario.Documento);
        DimensionamentoDoProjeto.Executar(dimensionamento.ListarCircuitos(), new CondicoesDoProjeto(30m, 1, "Cobre", "B1", "PVC"),
            PerfilNormativo.NBR5410_2004, CatalogosDeProduto.Padrao, dimensionamento);
    }

    private void Classificar(IReadOnlyCollection<long> ids, ClassificacaoDeCarga classificacao)
    {
        if (ClassificacaoEmLote.Executar(ids, classificacao, Porta).Classificados != ids.Count)
            throw new InvalidOperationException("Classificação do cenário incompleta.");
    }
}
