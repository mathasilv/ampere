using System.Diagnostics;
using Ampere.Core.Cargas;
using Ampere.Core.Circuitos;
using Ampere.Core.Parametros;
using Ampere.Tests.Revit.Parametros;
using Autodesk.Revit.DB.Electrical;
using TUnit.Assertions.Enums;

namespace Ampere.Tests.Revit.Circuitos;

/// <summary>
///     Critérios de aceite da F1.2 (especificação §8) contra o Revit real: classificar 100 elementos de uma vez e criar
///     10 circuitos mistos com numeração automática.
/// </summary>
/// <remarks>
///     Verificações pela API do Revit diretamente, não pelo adapter. Roda depois de <see cref="DesempenhoDaInjecao_Teste" />,
///     que precisa medir a primeira injeção da sessão.
/// </remarks>
[DependsOn(typeof(DesempenhoDaInjecao_Teste), ProceedOnFailure = true)]
public sealed class CircuitosNoRevit_Teste : TesteComProjetoEletrico
{
    private static readonly IReadOnlyDictionary<TipoDeCarga, RegraDeAgrupamento> RegrasDoCenario = new Dictionary<TipoDeCarga, RegraDeAgrupamento>
    {
        [TipoDeCarga.Iluminacao] = new(MaximoDePontos: 3),
        [TipoDeCarga.TUG] = new(MaximoDePontos: 4),
        [TipoDeCarga.TUE] = new(CircuitoExclusivo: true)
    };

    [Test]
    public async Task Classifica_100_elementos_de_uma_vez_e_recusa_o_que_nao_e_carga()
    {
        var tomadas = Cenario.ColocarTomadas(100);
        var classificacao = new ClassificacaoDeCarga(TipoDeCarga.TUG, PotenciaVA: 180m, FatorDePotencia: 0.92m, TensaoV: 127m, Fases: "F+N");

        var cronometro = Stopwatch.StartNew();
        var resultado = ClassificacaoEmLote.Executar([.. tomadas, Cenario.Parede.Id.Value], classificacao, Porta);
        cronometro.Stop();
        Console.WriteLine($"Classificação de {tomadas.Count} elementos: {cronometro.ElapsedMilliseconds} ms");

        await Assert.That(resultado.Classificados).IsEqualTo(100);
        await Assert.That(resultado.Recusados.Select(recusado => recusado.Id)).IsEquivalentTo([Cenario.Parede.Id.Value]);

        var problemas = new List<string>();
        foreach (var id in tomadas)
        {
            var tomada = Cenario.Documento.GetElement(new ElementId(id));
            if (Texto(tomada, "AMP_TipoCarga") != "TUG") problemas.Add($"{id}: tipo '{Texto(tomada, "AMP_TipoCarga")}'");
            var potencia = UnitUtils.ConvertFromInternalUnits(Parametro(tomada, "AMP_PotenciaInstaladaVA").AsDouble(), UnitTypeId.VoltAmperes);
            if (Math.Abs(potencia - 180) > 1e-6) problemas.Add($"{id}: potência {potencia} VA");
            if (Math.Abs(Parametro(tomada, "AMP_FatorPotencia").AsDouble() - 0.92) > 1e-9) problemas.Add($"{id}: FP");
            if (Math.Abs(Parametro(tomada, "AMP_TensaoCircuitoV").AsDouble() - 127) > 1e-9) problemas.Add($"{id}: tensão");
            if (Texto(tomada, "AMP_Fases") != "F+N") problemas.Add($"{id}: fases");
        }

        await Assert.That(problemas).IsEmpty();
    }

    [Test]
    public async Task Classifica_o_local_do_ponto_em_luminarias_e_tomadas()
    {
        var pontos = Cenario.ColocarLuminarias(2).Concat(Cenario.ColocarTomadas(2)).ToList();
        const string Local = "Cozinha, lavanderia, area de servico ou garagem";

        var resultado = ClassificacaoEmLote.Executar(pontos, new ClassificacaoDeCarga(TipoDeCarga.TUG, Local: Local), Porta);
        var semLocal = ClassificacaoEmLote.Executar(pontos, new ClassificacaoDeCarga(TipoDeCarga.TUG, PotenciaVA: 100m), Porta);

        await Assert.That(resultado.Classificados).IsEqualTo(4);
        await Assert.That(semLocal.Classificados).IsEqualTo(4);
        var locais = pontos.Select(id => Texto(Cenario.Documento.GetElement(new ElementId(id)), "AMP_Local") ?? "(vazio)").Distinct().ToList();
        await Assert.That(string.Join("|", locais)).IsEqualTo(Local);
    }

    [Test]
    public async Task Aparelho_gravado_em_TUE_e_apagado_ao_mudar_de_tipo()
    {
        var tomadas = Cenario.ColocarTomadas(2);

        ClassificacaoEmLote.Executar(tomadas, new ClassificacaoDeCarga(TipoDeCarga.TUE, Aparelho: Aparelho.Torneira), Porta);
        var comoTue = tomadas.Select(id => Texto(Cenario.Documento.GetElement(new ElementId(id)), "AMP_Aparelho") ?? "(vazio)").ToList();
        ClassificacaoEmLote.Executar(tomadas, new ClassificacaoDeCarga(TipoDeCarga.TUE), Porta);
        var semAlterar = Texto(Cenario.Documento.GetElement(new ElementId(tomadas[0])), "AMP_Aparelho");
        ClassificacaoEmLote.Executar(tomadas, new ClassificacaoDeCarga(TipoDeCarga.TUG), Porta);
        var comoTug = Texto(Cenario.Documento.GetElement(new ElementId(tomadas[0])), "AMP_Aparelho");

        await Assert.That(string.Join("|", comoTue)).IsEqualTo("Torneira|Torneira");
        await Assert.That(semAlterar).IsEqualTo("Torneira");
        await Assert.That(comoTug ?? string.Empty).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Circuito_selecionado_junto_com_os_pontos_e_classificado_sem_o_local()
    {
        var luminarias = Cenario.ColocarLuminarias(2);
        Classificar(luminarias, TipoDeCarga.Iluminacao, 62m);
        CriacaoDeCircuitos.Executar(luminarias, Cenario.Quadro.Id.Value, RegrasDoCenario, ConfiguracaoDeNumeracao.Padrao, Porta);
        var circuito = Cenario.Quadro.MEPModel.GetAssignedElectricalSystems().Single();

        var resultado = ClassificacaoEmLote.Executar([.. luminarias, circuito.Id.Value],
            new ClassificacaoDeCarga(TipoDeCarga.Iluminacao, Local: "Area externa"), Porta);

        await Assert.That(resultado.Classificados).IsEqualTo(3);
        await Assert.That(Texto(Cenario.Documento.GetElement(new ElementId(luminarias[0])), "AMP_Local")).IsEqualTo("Area externa");
        await Assert.That(Parametro(circuito, "AMP_Local")).IsNull();
    }

    [Test]
    public async Task Cria_10_circuitos_mistos_numerados_no_quadro()
    {
        var luminarias = Cenario.ColocarLuminarias(6);
        var tugs = Cenario.ColocarTomadas(12);
        var tues = Cenario.ColocarTomadas(5);
        Classificar(luminarias, TipoDeCarga.Iluminacao, 62m);
        Classificar(tugs, TipoDeCarga.TUG, 180m);
        Classificar(tues, TipoDeCarga.TUE, 600m);

        var cronometro = Stopwatch.StartNew();
        var plano = CriacaoDeCircuitos.Executar(
            [.. luminarias, .. tugs, .. tues, Cenario.Parede.Id.Value], Cenario.Quadro.Id.Value, RegrasDoCenario, ConfiguracaoDeNumeracao.Padrao, Porta);
        cronometro.Stop();
        Console.WriteLine($"Criação de {plano.Circuitos.Count} circuitos: {cronometro.ElapsedMilliseconds} ms");

        await Assert.That(plano.Circuitos.Select(circuito => circuito.Numero)).IsEquivalentTo(
            ["IL-01", "IL-02", "TUG-01", "TUG-02", "TUG-03", "TUE-01", "TUE-02", "TUE-03", "TUE-04", "TUE-05"], CollectionOrdering.Matching);
        await Assert.That(plano.Ignorados.Select(ignorado => ignorado.Id)).IsEquivalentTo([Cenario.Parede.Id.Value]);

        var sistemas = Cenario.Quadro.MEPModel.GetAssignedElectricalSystems().ToList();
        await Assert.That(sistemas.Count).IsEqualTo(10);

        var problemas = new List<string>();
        foreach (var circuito in plano.Circuitos)
        {
            var sistema = sistemas.SingleOrDefault(sistema => Texto(sistema, "AMP_NumeroCircuito") == circuito.Numero);
            if (sistema is null)
            {
                problemas.Add($"{circuito.Numero}: não existe no quadro");
                continue;
            }

            var membros = sistema.Elements.Cast<Element>().Select(membro => membro.Id.Value).Order().ToList();
            if (!membros.SequenceEqual(circuito.Pontos)) problemas.Add($"{circuito.Numero}: membros [{string.Join(",", membros)}]");
            if (Texto(sistema, "AMP_TipoCarga") != CodigosDeTipoDeCarga.Codigo(circuito.Tipo)) problemas.Add($"{circuito.Numero}: tipo");
            if (Texto(sistema, "AMP_Quadro") != "QD1") problemas.Add($"{circuito.Numero}: quadro '{Texto(sistema, "AMP_Quadro")}'");
            foreach (var membro in sistema.Elements.Cast<Element>())
            {
                if (Texto(membro, "AMP_NumeroCircuito") != circuito.Numero) problemas.Add($"{circuito.Numero}: ponto {membro.Id} sem o número");
                if (Texto(membro, "AMP_Quadro") != "QD1") problemas.Add($"{circuito.Numero}: ponto {membro.Id} sem o quadro");
            }
        }

        await Assert.That(problemas).IsEmpty();
    }

    [Test]
    public async Task Lista_o_quadro_do_projeto_para_o_dialogo()
    {
        var quadros = Porta.ListarQuadros();

        await Assert.That(quadros.Select(quadro => quadro.Nome)).IsEquivalentTo(["QD1"]);
        await Assert.That(quadros[0].Id).IsEqualTo(Cenario.Quadro.Id.Value);
        await Assert.That(Porta.ParametrosInjetados()).IsTrue();
    }

    [Test]
    public async Task Pontos_ja_circuitados_nao_entram_em_outro_circuito()
    {
        var tugs = Cenario.ColocarTomadas(4);
        Classificar(tugs, TipoDeCarga.TUG, 180m);
        CriacaoDeCircuitos.Executar(tugs, Cenario.Quadro.Id.Value, RegrasDoCenario, ConfiguracaoDeNumeracao.Padrao, Porta);

        var segunda = CriacaoDeCircuitos.Executar(tugs, Cenario.Quadro.Id.Value, RegrasDoCenario, ConfiguracaoDeNumeracao.Padrao, Porta);

        await Assert.That(segunda.Circuitos).IsEmpty();
        await Assert.That(segunda.Ignorados.All(ignorado => ignorado.Motivo == "já pertence ao circuito 'TUG-01'")).IsTrue();
        await Assert.That(Cenario.Quadro.MEPModel.GetAssignedElectricalSystems().Count).IsEqualTo(1);
    }

    private void Classificar(IReadOnlyCollection<long> ids, TipoDeCarga tipo, decimal potenciaVA)
    {
        var resultado = ClassificacaoEmLote.Executar(ids, new ClassificacaoDeCarga(tipo, PotenciaVA: potenciaVA), Porta);
        if (resultado.Classificados != ids.Count) throw new InvalidOperationException($"Classificação do cenário incompleta ({tipo}).");
    }

    private static Parameter Parametro(Element elemento, string nome) =>
        elemento.get_Parameter(CatalogoDeParametros.Padrao.Parametros.Single(parametro => parametro.Nome == nome).Guid);

    private static string? Texto(Element elemento, string nome) => Parametro(elemento, nome)?.AsString();
}
