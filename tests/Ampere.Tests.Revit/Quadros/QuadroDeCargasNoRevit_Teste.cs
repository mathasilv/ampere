using Ampere.Core.Cargas;
using Ampere.Core.Parametros;
using Ampere.Core.Circuitos;
using Ampere.Core.Normas;
using Ampere.Core.Quadros;
using Ampere.Revit.Quadros;
using Ampere.Tests.Revit.Circuitos;
using Ampere.Tests.Revit.Parametros;

namespace Ampere.Tests.Revit.Quadros;

/// <summary>
///     Quadro de cargas contra o Revit real (F1.4, adapter): leitura dos circuitos do painel — número, tipo, potência
///     somada dos membros, esquema e tensão — e a montagem completa com fatores informados.
/// </summary>
/// <remarks>
///     Fatores do teste são de projetista, não da norma (GAP-005). Sem fatores, o perfil oficial está com a tabela
///     TODO_NORMA e o quadro precisa ficar incompleto com o motivo.
/// </remarks>
[Property("Fonte", "TODO_NORMA")]
[DependsOn(typeof(DesempenhoDaInjecao_Teste), ProceedOnFailure = true)]
public sealed class QuadroDeCargasNoRevit_Teste : TesteComProjetoEletrico
{
    private static readonly IReadOnlyDictionary<TipoDeCarga, RegraDeAgrupamento> RegrasDoCenario = new Dictionary<TipoDeCarga, RegraDeAgrupamento>
    {
        [TipoDeCarga.Iluminacao] = new(MaximoDePontos: 3),
        [TipoDeCarga.TUG] = new(MaximoDePontos: 4),
        [TipoDeCarga.TUE] = new(CircuitoExclusivo: true)
    };

    [Test]
    public async Task Le_os_circuitos_do_quadro_com_potencia_esquema_e_tensao()
    {
        MontarQuadroComDoisCircuitos();

        var quadros = new DocumentoDeQuadrosRevit(Cenario.Documento).LerQuadrosComCircuitos();

        await Assert.That(quadros.Select(quadro => quadro.Nome)).IsEquivalentTo(["QD1"]);
        var leituras = quadros[0].Circuitos.OrderBy(circuito => circuito.Numero, StringComparer.Ordinal).ToList();
        await Assert.That(string.Join(",", leituras.Select(circuito => circuito.Numero))).IsEqualTo("IL-01,TUG-01");
        await Assert.That(string.Join(",", leituras.Select(circuito => circuito.Tipo))).IsEqualTo(
            CodigosDeTipoDeCarga.Codigo(TipoDeCarga.Iluminacao) + "," + CodigosDeTipoDeCarga.Codigo(TipoDeCarga.TUG));
        await Assert.That(leituras[0].PotenciaVA!.Value).IsEqualTo(186m);
        await Assert.That(leituras[1].PotenciaVA!.Value).IsEqualTo(720m);
        await Assert.That(leituras.All(circuito => circuito.Fases == "F+N" && circuito.TensaoV == 120m)).IsTrue();
        // O cenário atribui ao QD1 o sistema "120/208 Wye" (3 fases, 4 fios).
        await Assert.That(quadros[0].Alimentacao).IsEqualTo(new AlimentacaoDoQuadro("3F+N", 208m, "sistema de distribuição '120/208 Wye' do quadro", ["A", "B", "C"]));
        // Circuitos F+N: uma fase cada, das três do quadro (rótulos padrão das configurações elétricas).
        await Assert.That(leituras.All(circuito => circuito.FasesNoQuadro is { Count: 1 } fases && "ABC".Contains(fases[0]))).IsTrue();
    }

    [Test]
    public async Task Circuito_com_tensao_que_o_quadro_nao_fornece_e_problema()
    {
        var tomadas = Cenario.ColocarTomadas(2);
        Classificar(tomadas, TipoDeCarga.TUG, 180m, tensaoV: 127m);
        CriacaoDeCircuitos.Executar(tomadas, Cenario.Quadro.Id.Value, RegrasDoCenario, ConfiguracaoDeNumeracao.Padrao, Porta);

        var quadro = QuadroDeCargasDoProjeto.Executar(new DocumentoDeQuadrosRevit(Cenario.Documento), PerfilNormativo.NBR5410_2004,
            new Dictionary<TipoDeCarga, decimal> { [TipoDeCarga.TUG] = 0.5m })[0].Quadro;

        await Assert.That(string.Join("\n", quadro.Problemas)).Contains("circuito TUG-01 (F+N 127 V) incompatível com a alimentação do quadro (3F+N 208 V)");
    }

    [Test]
    public async Task Monta_quadro_completo_com_fatores_informados()
    {
        MontarQuadroComDoisCircuitos();
        var porta = new DocumentoDeQuadrosRevit(Cenario.Documento);

        var resultados = QuadroDeCargasDoProjeto.Executar(porta, PerfilNormativo.NBR5410_2004,
            new Dictionary<TipoDeCarga, decimal> { [TipoDeCarga.Iluminacao] = 1m, [TipoDeCarga.TUG] = 0.5m });

        await Assert.That(resultados.Count).IsEqualTo(1);
        var quadro = resultados[0].Quadro;
        await Assert.That(quadro.Problemas).IsEmpty();
        await Assert.That(quadro.Linhas.Count).IsEqualTo(2);
        await Assert.That(quadro.PotenciaInstaladaVA).IsEqualTo(906m);
        await Assert.That(quadro.DemandaVA).IsEqualTo(546m);
        await Assert.That(quadro.CorrenteDeDemandaA).IsEqualTo(546m / (1.7320508075688772935274463415m * 208m));
        await Assert.That(quadro.Memoria!.Passos[^1].Observacao).IsEqualTo("alimentação 3F+N 208 V: sistema de distribuição '120/208 Wye' do quadro");
        var fases = resultados[0].Fases!;
        await Assert.That(fases.CircuitosSemFase).IsEmpty();
        await Assert.That(fases.Fases.Sum(fase => fase.DemandaVA!.Value)).IsEqualTo(546m);
        await Assert.That(quadro.Memoria).IsNotNull();
        await Assert.That(quadro.Memoria!.Circuito).IsEqualTo("QD1");
        await Assert.That(resultados[0].Nome).IsEqualTo("QD1");
    }

    [Test]
    public async Task Sem_fatores_o_quadro_fica_incompleto_com_o_motivo_da_tabela()
    {
        MontarQuadroComDoisCircuitos();
        var porta = new DocumentoDeQuadrosRevit(Cenario.Documento);

        var resultados = QuadroDeCargasDoProjeto.Executar(porta, PerfilNormativo.NBR5410_2004);

        var quadro = resultados[0].Quadro;
        await Assert.That(quadro.DemandaVA).IsNull();
        await Assert.That(quadro.Memoria).IsNull();
        await Assert.That(quadro.PotenciaInstaladaVA).IsEqualTo(906m);
        await Assert.That(quadro.Problemas.Any(problema => problema.Contains("TODO_NORMA"))).IsTrue();
    }

    [Test]
    public async Task Grava_potencia_e_fator_nos_circuitos_e_o_hash_da_memoria_no_quadro()
    {
        MontarQuadroComDoisCircuitos();
        var porta = new DocumentoDeQuadrosRevit(Cenario.Documento);
        var resultados = QuadroDeCargasDoProjeto.Executar(porta, PerfilNormativo.NBR5410_2004,
            new Dictionary<TipoDeCarga, decimal> { [TipoDeCarga.Iluminacao] = 1m, [TipoDeCarga.TUG] = 0.5m });

        var gravacao = QuadroDeCargasDoProjeto.Gravar(resultados, porta);

        await Assert.That(gravacao.CircuitosAtualizados).IsEqualTo(2);
        await Assert.That(gravacao.QuadrosSemMemoria).IsEmpty();
        var problemas = new List<string>();
        foreach (var sistema in Cenario.Quadro.MEPModel.GetAssignedElectricalSystems())
        {
            var numero = Texto(sistema, "AMP_NumeroCircuito");
            var potencia = UnitUtils.ConvertFromInternalUnits(Parametro(sistema, "AMP_PotenciaInstaladaVA").AsDouble(), UnitTypeId.VoltAmperes);
            var fator = Parametro(sistema, "AMP_FatorDemanda").AsDouble();
            var esperados = numero == "IL-01" ? (186m, 1m) : numero == "TUG-01" ? (720m, 0.5m) : throw new InvalidOperationException($"circuito inesperado {numero}");
            if (Math.Abs(potencia - (double)esperados.Item1) > 1e-6) problemas.Add($"{numero}: potência {potencia} VA");
            if (Math.Abs(fator - (double)esperados.Item2) > 1e-9) problemas.Add($"{numero}: fator {fator}");
            // O AMP_MemoriaCalculoId do circuito é da memória do dimensionamento: o quadro não o toca.
            if (Texto(sistema, "AMP_MemoriaCalculoId") is { Length: > 0 } memoria) problemas.Add($"{numero}: hash do circuito '{memoria}'");
        }

        await Assert.That(problemas).IsEmpty();
        await Assert.That(Texto(Cenario.Quadro, "AMP_MemoriaCalculoId")).IsEqualTo(resultados[0].Quadro.Memoria!.Hash());
    }

    [Test]
    public async Task Montagem_sem_fator_apaga_o_fator_e_o_hash_da_montagem_anterior()
    {
        MontarQuadroComDoisCircuitos();
        var porta = new DocumentoDeQuadrosRevit(Cenario.Documento);
        QuadroDeCargasDoProjeto.Gravar(QuadroDeCargasDoProjeto.Executar(porta, PerfilNormativo.NBR5410_2004,
            new Dictionary<TipoDeCarga, decimal> { [TipoDeCarga.Iluminacao] = 1m, [TipoDeCarga.TUG] = 0.5m }), porta);

        // Sem fatores, o perfil oficial está com a tabela TODO_NORMA: quadro incompleto, sem memória.
        QuadroDeCargasDoProjeto.Gravar(QuadroDeCargasDoProjeto.Executar(porta, PerfilNormativo.NBR5410_2004), porta);

        var fatores = Cenario.Quadro.MEPModel.GetAssignedElectricalSystems().Select(sistema => Parametro(sistema, "AMP_FatorDemanda").AsDouble()).ToList();
        await Assert.That(fatores.All(fator => fator == 0d)).IsTrue();
        await Assert.That(Texto(Cenario.Quadro, "AMP_MemoriaCalculoId") ?? string.Empty).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Circuito_que_sai_do_quadro_perde_o_fator_da_montagem_anterior()
    {
        MontarQuadroComDoisCircuitos();
        var porta = new DocumentoDeQuadrosRevit(Cenario.Documento);
        var fatores = new Dictionary<TipoDeCarga, decimal> { [TipoDeCarga.Iluminacao] = 1m, [TipoDeCarga.TUG] = 0.5m };
        QuadroDeCargasDoProjeto.Gravar(QuadroDeCargasDoProjeto.Executar(porta, PerfilNormativo.NBR5410_2004, fatores), porta);
        var tomadas = Cenario.Quadro.MEPModel.GetAssignedElectricalSystems().Single(sistema => Texto(sistema, "AMP_NumeroCircuito") == "TUG-01");
        using (var transacao = new Transaction(Cenario.Documento, "Preparação do teste"))
        {
            transacao.Start();
            Parametro(tomadas, "AMP_TipoCarga").Set(string.Empty);
            transacao.Commit();
        }

        var resultados = QuadroDeCargasDoProjeto.Executar(porta, PerfilNormativo.NBR5410_2004, fatores);
        QuadroDeCargasDoProjeto.Gravar(resultados, porta);

        await Assert.That(string.Join("\n", resultados[0].Quadro.Problemas)).Contains("circuito TUG-01: sem AMP_TipoCarga reconhecido");
        await Assert.That(Parametro(tomadas, "AMP_FatorDemanda").AsDouble()).IsEqualTo(0d);
        await Assert.That(UnitUtils.ConvertFromInternalUnits(Parametro(tomadas, "AMP_PotenciaInstaladaVA").AsDouble(), UnitTypeId.VoltAmperes))
            .IsEqualTo(720d).Within(1e-6);
        await Assert.That(Texto(Cenario.Quadro, "AMP_MemoriaCalculoId")).IsEqualTo(resultados[0].Quadro.Memoria!.Hash());
    }

    [Test]
    public async Task Circuito_desconectado_do_quadro_perde_fator_e_quadro()
    {
        MontarQuadroComDoisCircuitos();
        var porta = new DocumentoDeQuadrosRevit(Cenario.Documento);
        var fatores = new Dictionary<TipoDeCarga, decimal> { [TipoDeCarga.Iluminacao] = 1m, [TipoDeCarga.TUG] = 0.5m };
        QuadroDeCargasDoProjeto.Gravar(QuadroDeCargasDoProjeto.Executar(porta, PerfilNormativo.NBR5410_2004, fatores), porta);
        var tomadas = Cenario.Quadro.MEPModel.GetAssignedElectricalSystems().Single(sistema => Texto(sistema, "AMP_NumeroCircuito") == "TUG-01");
        using (var transacao = new Transaction(Cenario.Documento, "Preparação do teste"))
        {
            transacao.Start();
            tomadas.DisconnectPanel();
            transacao.Commit();
        }

        QuadroDeCargasDoProjeto.Gravar(QuadroDeCargasDoProjeto.Executar(porta, PerfilNormativo.NBR5410_2004, fatores), porta);

        await Assert.That(Parametro(tomadas, "AMP_FatorDemanda").AsDouble()).IsEqualTo(0d);
        await Assert.That(Texto(tomadas, "AMP_Quadro") ?? string.Empty).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Cria_a_tabela_do_quadro_com_campos_filtro_e_ordenacao()
    {
        MontarQuadroComDoisCircuitos();
        var porta = new DocumentoDeQuadrosRevit(Cenario.Documento);
        var resultados = QuadroDeCargasDoProjeto.Executar(porta, PerfilNormativo.NBR5410_2004,
            new Dictionary<TipoDeCarga, decimal> { [TipoDeCarga.Iluminacao] = 1m, [TipoDeCarga.TUG] = 0.5m });
        QuadroDeCargasDoProjeto.Gravar(resultados, porta);

        var nomes = QuadroDeCargasDoProjeto.CriarTabelas(resultados, porta);
        var tabela = new FilteredElementCollector(Cenario.Documento).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>()
            .Single(view => view.Name == nomes[0]);
        ScheduleSheetInstance naPrancha;
        using (var transacao = new Transaction(Cenario.Documento, "Prancha do teste"))
        {
            transacao.Start();
            naPrancha = ScheduleSheetInstance.Create(Cenario.Documento, ViewSheet.Create(Cenario.Documento, ElementId.InvalidElementId).Id, tabela.Id, XYZ.Zero);
            transacao.Commit();
        }

        var segunda = QuadroDeCargasDoProjeto.CriarTabelas(resultados, porta); // idempotente: a mesma view, ainda na prancha

        await Assert.That(string.Join("|", nomes)).IsEqualTo("QD1 — quadro de cargas (Ampere)");
        await Assert.That(segunda[0]).IsEqualTo(nomes[0]);
        await Assert.That(tabela.IsValidObject).IsTrue();
        await Assert.That(naPrancha.IsValidObject).IsTrue();
        await Assert.That(tabela.Definition.CategoryId).IsEqualTo(new ElementId(BuiltInCategory.OST_ElectricalCircuit));
        await Assert.That(tabela.Definition.GetFieldCount()).IsEqualTo(12);
        await Assert.That(string.Join(",", Enumerable.Range(0, tabela.Definition.GetFieldCount())
            .Select(indice => tabela.Definition.GetField(indice).ColumnHeading))).IsEqualTo(
            "Nº,Descrição,Tipo de carga,Potência instalada (VA),Fator de demanda,IB (A),Seção (mm²),Disjuntor (A),IDR (mA),Queda (%),Memória do circuito,Quadro");
        await Assert.That(tabela.Definition.GetSortGroupFields().Count).IsEqualTo(1);
        await Assert.That(tabela.Definition.GetFilters().Count).IsEqualTo(1);
    }

    /// <summary>QD1 (120/208 Wye) com IL-01 (3 × 62 VA) e TUG-01 (4 × 180 VA), classificados com F+N 120 V.</summary>
    private void MontarQuadroComDoisCircuitos()
    {
        var luminarias = Cenario.ColocarLuminarias(3);
        var tomadas = Cenario.ColocarTomadas(4);
        Classificar(luminarias, TipoDeCarga.Iluminacao, 62m);
        Classificar(tomadas, TipoDeCarga.TUG, 180m);
        CriacaoDeCircuitos.Executar([.. luminarias, .. tomadas], Cenario.Quadro.Id.Value, RegrasDoCenario, ConfiguracaoDeNumeracao.Padrao, Porta);
    }

    private void Classificar(IReadOnlyCollection<long> ids, TipoDeCarga tipo, decimal potenciaVA, decimal tensaoV = 120m)
    {
        var resultado = ClassificacaoEmLote.Executar(ids, new ClassificacaoDeCarga(tipo, PotenciaVA: potenciaVA, TensaoV: tensaoV, Fases: "F+N"), Porta);
        if (resultado.Classificados != ids.Count) throw new InvalidOperationException($"Classificação do cenário incompleta ({tipo}).");
    }

    private static Parameter Parametro(Element elemento, string nome) =>
        elemento.get_Parameter(CatalogoDeParametros.Padrao.Parametros.Single(parametro => parametro.Nome == nome).Guid);

    private static string? Texto(Element elemento, string nome) => Parametro(elemento, nome)?.AsString();
}
