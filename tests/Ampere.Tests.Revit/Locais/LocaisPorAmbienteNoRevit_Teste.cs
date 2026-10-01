using Ampere.Core.Cargas;
using Ampere.Core.Locais;
using Ampere.Core.Parametros;
using Ampere.Revit.Locais;
using Ampere.Tests.Revit.Circuitos;
using Ampere.Tests.Revit.Parametros;

namespace Ampere.Tests.Revit.Locais;

/// <summary>
///     "Locais pelos ambientes" contra o Revit real: ambiente de cada ponto (Room) e gravação de AMP_Local.
/// </summary>
/// <remarks>
///     A parede do cenário fica no meio de um retângulo fechado: um ambiente de cada lado ("Cozinha" e "Sala"). As
///     tomadas vão numa das faces — qual, depende do lado externo da parede —, então o teste confere que todas caem no
///     mesmo ambiente, e que ele é um dos dois.
/// </remarks>
[DependsOn(typeof(DesempenhoDaInjecao_Teste), ProceedOnFailure = true)]
public sealed class LocaisPorAmbienteNoRevit_Teste : TesteComProjetoEletrico
{
    private const string Cozinha = "Cozinha, lavanderia, area de servico ou garagem";

    [Test]
    public async Task Sem_ambientes_os_pontos_ficam_sem_ambiente()
    {
        var tomadas = ClassificarTomadas(2);

        var pontos = new DocumentoDeAmbientesRevit(Cenario.Documento).LerPontos([]);

        await Assert.That(pontos.Select(ponto => ponto.Id).Order()).IsEquivalentTo(tomadas.Order());
        await Assert.That(pontos.All(ponto => ponto.Ambiente is null && ponto.Editavel)).IsTrue();
    }

    [Test]
    public async Task Le_o_ambiente_de_cada_ponto_e_grava_o_local_escolhido()
    {
        Cenario.CriarDoisAmbientes();
        var tomadas = ClassificarTomadas(3);
        var porta = new DocumentoDeAmbientesRevit(Cenario.Documento);

        var pontos = porta.LerPontos([]);
        var ambientes = LocaisPorAmbiente.Agrupar(pontos);

        await Assert.That(pontos.Count).IsEqualTo(3);
        await Assert.That(ambientes.Count).IsEqualTo(1);
        await Assert.That(ambientes[0].Nome is "Cozinha" or "Sala").IsTrue().Because($"ambiente lido: '{ambientes[0].Nome}'");
        await Assert.That(ambientes[0].Pontos).IsEqualTo(3);

        var resultado = LocaisPorAmbiente.Aplicar(pontos, new Dictionary<string, string> { [ambientes[0].Nome] = Cozinha },
            Core.Normas.PerfilNormativo.NBR5410_2004.Vocabulario.Locais, porta);

        await Assert.That(resultado.Gravados).IsEqualTo(3);
        var locais = tomadas.Select(id => Cenario.Documento.GetElement(new ElementId(id))
            .get_Parameter(Guid("AMP_Local"))?.AsString() ?? "(vazio)").Distinct().ToList();
        await Assert.That(string.Join("|", locais)).IsEqualTo(Cozinha);
    }

    [Test]
    public async Task Com_selecao_le_so_os_pontos_classificados_selecionados()
    {
        var tomadas = ClassificarTomadas(3);
        var naoClassificada = Cenario.ColocarTomadas(1);

        var pontos = new DocumentoDeAmbientesRevit(Cenario.Documento).LerPontos([tomadas[0], naoClassificada[0], Cenario.Parede.Id.Value]);

        await Assert.That(string.Join(",", pontos.Select(ponto => ponto.Id))).IsEqualTo(tomadas[0].ToString());
    }

    private List<long> ClassificarTomadas(int quantidade)
    {
        var tomadas = Cenario.ColocarTomadas(quantidade);
        var resultado = ClassificacaoEmLote.Executar(tomadas, new ClassificacaoDeCarga(TipoDeCarga.TUG, PotenciaVA: 180m), Porta);
        if (resultado.Classificados != quantidade) throw new InvalidOperationException("Classificação do cenário incompleta.");
        return tomadas;
    }

    private static Guid Guid(string nome) => CatalogoDeParametros.Padrao.Parametros.Single(parametro => parametro.Nome == nome).Guid;
}
