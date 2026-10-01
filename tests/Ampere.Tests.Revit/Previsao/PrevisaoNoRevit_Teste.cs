using Ampere.Core.Cargas;
using Ampere.Core.Previsao;
using Ampere.Revit.Previsao;
using Ampere.Tests.Revit.Circuitos;
using Ampere.Tests.Revit.Parametros;

namespace Ampere.Tests.Revit.Previsao;

/// <summary>
///     "Previsão de cargas" contra o Revit real: cômodos (Rooms) com área e perímetro, os pontos de cada um e as
///     categorias guardadas no DataStorage do Ampere.
/// </summary>
[Property("Fonte", "NBR 5410:2004, item 9.5.2")]
[DependsOn(typeof(DesempenhoDaInjecao_Teste), ProceedOnFailure = true)]
public sealed class PrevisaoNoRevit_Teste : TesteComProjetoEletrico
{
    [Test]
    public async Task Le_os_comodos_com_area_perimetro_e_pontos()
    {
        var (cozinha, sala) = Cenario.CriarDoisAmbientes();
        var tomadas = ClassificarTomadas(3);

        var leitura = new DocumentoDePrevisaoRevit(Cenario.Documento).Ler();
        var comodos = leitura.Comodos;

        await Assert.That(string.Join("|", comodos.Select(comodo => comodo.Nome).Order())).IsEqualTo("Cozinha|Sala");
        foreach (var (comodo, room) in new[] { (comodos.Single(item => item.Nome == "Cozinha"), cozinha), (comodos.Single(item => item.Nome == "Sala"), sala) })
        {
            var ambiente = (SpatialElement)room;
            await Assert.That(comodo.AreaM2).IsEqualTo(Math.Round((decimal)UnitUtils.ConvertFromInternalUnits(ambiente.Area, UnitTypeId.SquareMeters), 4));
            await Assert.That(comodo.PerimetroM).IsEqualTo(Math.Round((decimal)UnitUtils.ConvertFromInternalUnits(ambiente.Perimeter, UnitTypeId.Meters), 3));
            await Assert.That(comodo.AreaM2).IsGreaterThan(0m);
            await Assert.That(comodo.Chave).IsEqualTo($"0:{room.Id.Value}");
        }

        // As tomadas estão numa das faces da parede do meio: todas no mesmo cômodo.
        var comPontos = comodos.Where(comodo => comodo.Pontos.Count > 0).ToList();
        await Assert.That(comPontos.Count).IsEqualTo(1);
        await Assert.That(comPontos[0].Pontos.Select(ponto => ponto.Id).Order()).IsEquivalentTo(tomadas.Order());
        await Assert.That(comPontos[0].Pontos.All(ponto => ponto is { Tipo: TipoDeCarga.TUG, PotenciaVA: 180m, Circuito: null })).IsTrue();
        await Assert.That(leitura.PontosForaDosComodos).IsEmpty();
    }

    [Test]
    public async Task Categorias_ficam_guardadas_no_projeto()
    {
        Cenario.CriarDoisAmbientes();
        var porta = new DocumentoDePrevisaoRevit(Cenario.Documento);
        var leitura = porta.Ler();

        var execucao = PrevisaoDeCargas.Executar(leitura, new Dictionary<string, string?> { ["Cozinha"] = "Cozinha, copa, área de serviço ou lavanderia", ["Sala"] = "Sala ou dormitório" },
            NormaDePrevisao.NBR5410_2004, porta);

        await Assert.That(execucao.Problemas).IsEmpty();
        await Assert.That(execucao.CategoriasNaoGravadas).IsNull();
        var lidas = new DocumentoDePrevisaoRevit(Cenario.Documento).LerCategorias();
        await Assert.That(string.Join("|", lidas.OrderBy(par => par.Key, StringComparer.Ordinal).Select(par => $"{par.Key}={par.Value}")))
            .IsEqualTo("Cozinha=Cozinha, copa, área de serviço ou lavanderia|Sala=Sala ou dormitório");
        // Sem pontos, os dois cômodos ficam abaixo da previsão.
        await Assert.That(execucao.Resultado!.Contar(SituacaoDoComodo.NaoAtende)).IsEqualTo(2);
    }

    private List<long> ClassificarTomadas(int quantidade)
    {
        var tomadas = Cenario.ColocarTomadas(quantidade);
        var resultado = ClassificacaoEmLote.Executar(tomadas, new ClassificacaoDeCarga(TipoDeCarga.TUG, PotenciaVA: 180m), Porta);
        if (resultado.Classificados != quantidade) throw new InvalidOperationException("Classificação do cenário incompleta.");
        return tomadas;
    }
}
