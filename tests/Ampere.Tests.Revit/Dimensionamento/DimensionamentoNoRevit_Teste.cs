using System.Diagnostics;
using Ampere.Core.Cargas;
using Ampere.Core.Catalogos;
using Ampere.Core.Circuitos;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Normas;
using Ampere.Core.Parametros;
using Ampere.Revit.Dimensionamento;
using Ampere.Tests.Revit.Circuitos;
using Ampere.Tests.Revit.Parametros;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace Ampere.Tests.Revit.Dimensionamento;

/// <summary>
///     Dimensionamento contra o Revit real (adapter <see cref="DocumentoDeDimensionamentoRevit" />): leitura dos
///     circuitos e pontos, gravação dos resultados, limpeza do que deixou de valer e o orçamento de 400 pontos em menos
///     de 5 s (especificação §8).
/// </summary>
/// <remarks>
///     Perfil oficial com os catálogos oficiais, ainda vazios (GAP-004): o cálculo vai até o IDR e para no diâmetro do
///     condutor. As verificações comparam o que ficou nos parâmetros com o resultado do motor, lendo pela API do Revit
///     diretamente — o motor tem os seus próprios testes.
/// </remarks>
[DependsOn(typeof(DesempenhoDaInjecao_Teste), ProceedOnFailure = true)]
public sealed class DimensionamentoNoRevit_Teste : TesteComProjetoEletrico
{
    private const string Cozinha = "Cozinha, lavanderia, area de servico ou garagem";
    private const string DemaisLocais = "Demais locais internos";

    private static readonly CondicoesDoProjeto Condicoes = new(30m, 1, "Cobre", "B1", "PVC");

    private static readonly IReadOnlyDictionary<TipoDeCarga, RegraDeAgrupamento> RegrasDoCenario = new Dictionary<TipoDeCarga, RegraDeAgrupamento>
    {
        [TipoDeCarga.Iluminacao] = new(MaximoDePontos: 3),
        [TipoDeCarga.TUG] = new(MaximoDePontos: 4)
    };

    // O QD1 do template tem cerca de 42 posições: 400 pontos em 25 circuitos cabem com folga.
    private static readonly IReadOnlyDictionary<TipoDeCarga, RegraDeAgrupamento> RegrasDoOrcamento = new Dictionary<TipoDeCarga, RegraDeAgrupamento>
    {
        [TipoDeCarga.TUG] = new(MaximoDePontos: 16)
    };

    private DocumentoDeDimensionamentoRevit Dimensionamento => new(Cenario.Documento);

    [Test]
    public async Task Lista_so_os_circuitos_do_Ampere_por_quadro_e_numero()
    {
        MontarIluminacaoETomadas();
        var avulsa = Cenario.ColocarTomadas(1);
        Transacionar(() => ElectricalSystem.Create(Cenario.Documento, [new ElementId(avulsa[0])], ElectricalSystemType.PowerCircuit));

        var ids = Dimensionamento.ListarCircuitos();

        await Assert.That(string.Join("|", ids.Select(id => Texto(Elemento(id), "AMP_NumeroCircuito")))).IsEqualTo("IL-01|TUG-01");
        await Assert.That(Dimensionamento.ParametrosInjetados()).IsTrue();
    }

    [Test]
    public async Task Le_pontos_quadro_e_os_dois_comprimentos_do_circuito()
    {
        MontarIluminacaoETomadas();
        var ids = Dimensionamento.ListarCircuitos();
        Transacionar(() => Parametro(Elemento(ids[1]), "AMP_ComprimentoRotaM").Set(UnitUtils.ConvertToInternalUnits(12.5, UnitTypeId.Meters)));

        var circuitos = Dimensionamento.LerCircuitos(ids);

        var iluminacao = circuitos[0];
        await Assert.That(iluminacao.Numero).IsEqualTo("IL-01");
        await Assert.That(iluminacao.TipoDeCarga).IsEqualTo(CodigosDeTipoDeCarga.Codigo(TipoDeCarga.Iluminacao));
        await Assert.That(iluminacao.Quadro).IsEqualTo("QD1");
        await Assert.That(iluminacao.Pontos.Count).IsEqualTo(3);
        await Assert.That(iluminacao.Pontos.All(ponto => ponto is { TensaoV: 127m, Fases: "F+N", Local: DemaisLocais })).IsTrue();
        await Assert.That(iluminacao.Pontos.All(ponto => ponto.TipoDeCarga == CodigosDeTipoDeCarga.Codigo(TipoDeCarga.Iluminacao))).IsTrue();
        await Assert.That(iluminacao.Pontos.All(ponto => Math.Abs(ponto.PotenciaVA!.Value - 62m) < 0.000001m)).IsTrue();
        await Assert.That(iluminacao.ComprimentoM).IsNull();
        await Assert.That(iluminacao.ComprimentoNoRevit!.Metros).IsGreaterThan(0m);
        await Assert.That(iluminacao.ComprimentoNoRevit.Caminho).StartsWith("caminho");
        await Assert.That(iluminacao.MetodoDeInstalacao).IsNull();

        var tomadas = circuitos[1];
        await Assert.That(tomadas.Pontos.All(ponto => ponto.Local == Cozinha)).IsTrue();
        await Assert.That(Math.Round(tomadas.ComprimentoM!.Value, 6)).IsEqualTo(12.5m);
    }

    [Test]
    public async Task Grava_os_resultados_do_motor_e_nunca_as_entradas()
    {
        MontarIluminacaoETomadas();
        var ids = Dimensionamento.ListarCircuitos();

        var resultados = Dimensionar(ids);

        await Assert.That(resultados.All(resultado => resultado.Dimensionamento?.Situacao == SituacaoDoDimensionamento.Interrompido)).IsTrue();
        await Assert.That(resultados.All(resultado => resultado.Dimensionamento!.Problemas.Single().Contains("TODO_CATALOGO"))).IsTrue();
        var problemas = new List<string>();
        foreach (var resultado in resultados) problemas.AddRange(ConferirGravados(resultado));
        await Assert.That(problemas).IsEmpty();

        var iluminacao = Elemento(ids[0]);
        var tomadas = Elemento(ids[1]);
        await Assert.That(Parametro(iluminacao, "AMP_IDR_SensibilidadeMa").HasValue).IsFalse();
        await Assert.That(Parametro(tomadas, "AMP_IDR_SensibilidadeMa").AsDouble()).IsEqualTo(30d);
        await Assert.That(Parametro(tomadas, "AMP_IDR_NominalA").AsDouble()).IsGreaterThanOrEqualTo(Parametro(tomadas, "AMP_DisjuntorNominalA").AsDouble());
        foreach (var nome in new[] { "AMP_MetodoInstalacao", "AMP_MaterialIsolacao", "AMP_TipoCondutor", "AMP_ComprimentoRotaM", "AMP_OcupacaoEletrodutoPct" })
            await Assert.That(Parametro(tomadas, nome).HasValue).IsFalse().Because($"{nome} não pode ter sido gravado");

        var queda = resultados[0].Memoria!.Passos.Single(passo => passo.Descricao == "Queda de tensão");
        await Assert.That(queda.Observacao).Contains("L: calculado pelo Revit (caminho");
    }

    [Test]
    public async Task Comprimento_informado_prevalece_sobre_o_do_Revit()
    {
        MontarIluminacaoETomadas();
        var ids = Dimensionamento.ListarCircuitos();
        Transacionar(() => Parametro(Elemento(ids[1]), "AMP_ComprimentoRotaM").Set(UnitUtils.ConvertToInternalUnits(10, UnitTypeId.Meters)));

        var resultados = Dimensionar(ids);

        var queda = resultados[1].Memoria!.Passos.Single(passo => passo.Descricao == "Queda de tensão");
        await Assert.That(queda.Valores.Single(valor => valor.Nome == "L").Valor).IsEqualTo(10m);
        await Assert.That(queda.Observacao).EndsWith("L: AMP_ComprimentoRotaM, informado pelo projetista");
        await Assert.That(UnitUtils.ConvertFromInternalUnits(Parametro(Elemento(ids[1]), "AMP_ComprimentoRotaM").AsDouble(), UnitTypeId.Meters))
            .IsEqualTo(10d).Within(1e-9);
    }

    [Test]
    public async Task Rodada_seguinte_apaga_o_IDR_que_deixou_de_ser_exigido()
    {
        var (_, tomadas) = MontarIluminacaoETomadas();
        var ids = Dimensionamento.ListarCircuitos();
        var primeira = Dimensionar(ids);
        var circuito = Elemento(ids[1]);
        await Assert.That(Parametro(circuito, "AMP_IDR_SensibilidadeMa").AsDouble()).IsEqualTo(30d);

        Classificar(tomadas, new ClassificacaoDeCarga(TipoDeCarga.TUG, Local: DemaisLocais));
        var segunda = Dimensionar(ids);

        await Assert.That(segunda[1].Dimensionamento!.IdrSensibilidadeMa).IsNull();
        await Assert.That(Parametro(circuito, "AMP_IDR_SensibilidadeMa").AsDouble()).IsEqualTo(0d);
        await Assert.That(Parametro(circuito, "AMP_IDR_NominalA").AsDouble()).IsEqualTo(0d);
        await Assert.That(Texto(circuito, "AMP_MemoriaCalculoId")).IsEqualTo(segunda[1].Memoria!.Hash());
        await Assert.That(segunda[1].Memoria!.Hash()).IsNotEqualTo(primeira[1].Memoria!.Hash());
    }

    [Test]
    public async Task Ponto_sem_local_deixa_o_circuito_sem_protecao_gravada()
    {
        var (_, tomadas) = MontarIluminacaoETomadas();
        var ids = Dimensionamento.ListarCircuitos();
        Dimensionar(ids);
        var circuito = Elemento(ids[1]);
        Transacionar(() => Parametro(Elemento(tomadas[0]), "AMP_Local").Set(string.Empty));

        var resultados = Dimensionar(ids);

        await Assert.That(resultados[1].Dimensionamento!.IdrAvaliado).IsFalse();
        await Assert.That(resultados[1].Dimensionamento!.DisjuntorA).IsNotNull();
        await Assert.That(Parametro(circuito, "AMP_DisjuntorNominalA").AsDouble()).IsEqualTo(0d);
        await Assert.That(Parametro(circuito, "AMP_BitolaCondutorMm2").AsDouble()).IsEqualTo(0d);
        await Assert.That(Parametro(circuito, "AMP_IDR_SensibilidadeMa").AsDouble()).IsEqualTo(0d);
        await Assert.That(UnitUtils.ConvertFromInternalUnits(Parametro(circuito, "AMP_CorrenteProjetoA").AsDouble(), UnitTypeId.Amperes))
            .IsEqualTo((double)resultados[1].Dimensionamento!.CorrenteDeProjetoA!.Value).Within(1e-9);
        await Assert.That(Texto(circuito, "AMP_MemoriaCalculoId")).IsEqualTo(resultados[1].Memoria!.Hash());
    }

    [Test]
    public async Task Circuito_que_perdeu_dados_fica_sem_os_resultados_anteriores()
    {
        MontarIluminacaoETomadas();
        var ids = Dimensionamento.ListarCircuitos();
        Dimensionar(ids);
        var circuito = Elemento(ids[1]);
        Transacionar(() => Parametro(circuito, "AMP_TipoCarga").Set(string.Empty));

        var resultados = Dimensionar(ids);

        await Assert.That(string.Join("\n", resultados[1].ProblemasDeDados)).Contains("AMP_TipoCarga vazio");
        await Assert.That(Texto(circuito, "AMP_MemoriaCalculoId") ?? string.Empty).IsEqualTo(string.Empty);
        await Assert.That(Texto(circuito, "AMP_PerfilNorma") ?? string.Empty).IsEqualTo(string.Empty);
        await Assert.That(Parametro(circuito, "AMP_DisjuntorNominalA").AsDouble()).IsEqualTo(0d);
        await Assert.That(Parametro(circuito, "AMP_CorrenteProjetoA").AsDouble()).IsEqualTo(0d);
        await Assert.That(Texto(Elemento(ids[0]), "AMP_MemoriaCalculoId")).IsEqualTo(resultados[0].Memoria!.Hash());
    }

    [Test]
    public async Task Le_as_decisoes_do_projetista_e_nunca_as_grava()
    {
        MontarIluminacaoETomadas();
        var ids = Dimensionamento.ListarCircuitos();
        var circuito = Elemento(ids[1]);
        Transacionar(() =>
        {
            Parametro(circuito, "AMP_SecaoMinimaProjetistaMm2").Set(4d);
            Parametro(circuito, "AMP_DisjuntorProjetistaA").Set(20d);
            Parametro(circuito, "AMP_IDR_DecisaoProjetista").Set("Dispensar");
            Parametro(circuito, "AMP_JustificativaProjetista").Set("decisão de teste");
            Parametro(circuito, "AMP_TemperaturaAmbienteC").Set(35d);
            Parametro(circuito, "AMP_CircuitosAgrupados").Set(2d);
        });

        var lidas = Dimensionamento.LerCircuitos(ids)[1].Decisoes;
        var resultados = Dimensionar(ids);

        await Assert.That(lidas).IsEqualTo(new DecisoesDoProjetista(4m, 20m, "Dispensar", null, "decisão de teste", 35m, 2m));
        await Assert.That(Dimensionamento.LerCircuitos(ids)[0].Decisoes).IsEqualTo(new DecisoesDoProjetista());
        var calculo = resultados[1].Dimensionamento!;
        await Assert.That(calculo.SecaoMm2 ?? -1m).IsGreaterThanOrEqualTo(4m);
        await Assert.That(calculo.DisjuntorA).IsEqualTo(20m);
        await Assert.That(calculo.IdrAvaliado).IsTrue();
        await Assert.That(calculo.IdrSensibilidadeMa).IsNull();
        await Assert.That(string.Join("\n", calculo.Avisos)).Contains("IDR dispensado pelo projetista");
        var observacoes = string.Join("\n", resultados[1].Memoria!.Passos.Select(passo => passo.Observacao).OfType<string>());
        await Assert.That(observacoes).Contains("θ: AMP_TemperaturaAmbienteC do circuito (o projeto usa 30 °C; justificativa: decisão de teste)");
        await Assert.That(observacoes).Contains("circuitos: AMP_CircuitosAgrupados do circuito (o projeto usa 1; justificativa: decisão de teste)");
        await Assert.That(Parametro(circuito, "AMP_DisjuntorNominalA").AsDouble()).IsEqualTo(20d);
        await Assert.That(Parametro(circuito, "AMP_IDR_SensibilidadeMa").HasValue).IsFalse();

        // As decisões continuam como o projetista deixou: o dimensionamento nunca as escreve.
        await Assert.That(Parametro(circuito, "AMP_SecaoMinimaProjetistaMm2").AsDouble()).IsEqualTo(4d);
        await Assert.That(Parametro(circuito, "AMP_DisjuntorProjetistaA").AsDouble()).IsEqualTo(20d);
        await Assert.That(Texto(circuito, "AMP_IDR_DecisaoProjetista")).IsEqualTo("Dispensar");
        await Assert.That(Parametro(circuito, "AMP_IDR_SensibilidadeProjetistaMa").HasValue).IsFalse();
        await Assert.That(Parametro(circuito, "AMP_TemperaturaAmbienteC").AsDouble()).IsEqualTo(35d);
        await Assert.That(Parametro(circuito, "AMP_CircuitosAgrupados").AsDouble()).IsEqualTo(2d);
    }

    [Test]
    public async Task Selecao_de_circuito_ponto_ou_quadro_da_os_circuitos_do_Ampere()
    {
        var (luminarias, tomadas) = MontarIluminacaoETomadas();
        var avulsa = Cenario.ColocarTomadas(1);
        Transacionar(() => ElectricalSystem.Create(Cenario.Documento, [new ElementId(avulsa[0])], ElectricalSystemType.PowerCircuit));
        var ids = Dimensionamento.ListarCircuitos();

        await Assert.That(string.Join("|", Dimensionamento.CircuitosDaSelecao([tomadas[0]]))).IsEqualTo($"{ids[1]}");
        await Assert.That(string.Join("|", Dimensionamento.CircuitosDaSelecao([Cenario.Quadro.Id.Value]))).IsEqualTo($"{ids[0]}|{ids[1]}");
        await Assert.That(string.Join("|", Dimensionamento.CircuitosDaSelecao([ids[1], tomadas[2], luminarias[0]]))).IsEqualTo($"{ids[0]}|{ids[1]}");
        await Assert.That(Dimensionamento.CircuitosDaSelecao([Cenario.Parede.Id.Value, avulsa[0]])).IsEmpty();
    }

    [Test]
    public async Task Condicoes_da_rodada_ficam_guardadas_no_modelo()
    {
        MontarIluminacaoETomadas();
        var ids = Dimensionamento.ListarCircuitos();
        var armazensAntes = Armazens();
        var primeiras = Condicoes with { TemperaturaAmbienteC = 35.5m, CircuitosAgrupados = 2, TipoDeEletroduto = "Eletroduto de teste" };

        await Assert.That(Dimensionamento.LerCondicoes()).IsNull();
        DimensionamentoDoProjeto.Executar(ids, primeiras, PerfilNormativo.NBR5410_2004, CatalogosDeProduto.Padrao, Dimensionamento);
        await Assert.That(Dimensionamento.LerCondicoes()).IsEqualTo(primeiras);
        await Assert.That(Armazens()).IsEqualTo(armazensAntes + 1);

        var porta = Dimensionamento;
        DimensionamentoDoProjeto.Executar(ids, Condicoes, PerfilNormativo.NBR5410_2004, CatalogosDeProduto.Padrao, porta);
        await Assert.That(porta.CondicoesNaoGravadas).IsNull();
        await Assert.That(Dimensionamento.LerCondicoes()).IsEqualTo(Condicoes);
        await Assert.That(Armazens()).IsEqualTo(armazensAntes + 1);
    }

    [Test]
    public async Task Dimensiona_400_pontos_em_menos_de_5_segundos()
    {
        var tomadas = Cenario.ColocarTomadas(400);
        Classificar(tomadas, new ClassificacaoDeCarga(TipoDeCarga.TUG, PotenciaVA: 180m, TensaoV: 127m, Fases: "F+N", Local: Cozinha));
        var plano = CriacaoDeCircuitos.Executar(tomadas, Cenario.Quadro.Id.Value, RegrasDoOrcamento, ConfiguracaoDeNumeracao.Padrao, Porta);
        if (plano.Circuitos.Count != 25) throw new InvalidOperationException($"Cenário com {plano.Circuitos.Count} circuitos, esperado 25.");

        var cronometro = Stopwatch.StartNew();
        var porta = new DocumentoDeDimensionamentoRevit(Cenario.Documento);
        var resultados = DimensionamentoDoProjeto.Executar(porta.ListarCircuitos(), Condicoes, PerfilNormativo.NBR5410_2004, CatalogosDeProduto.Padrao, porta);
        cronometro.Stop();

        Console.WriteLine($"Dimensionamento de {tomadas.Count} pontos em {resultados.Count} circuitos: {cronometro.Elapsed.TotalMilliseconds:0} ms");
        await Assert.That(resultados.Count).IsEqualTo(25);
        await Assert.That(resultados.All(resultado => resultado.Memoria is not null)).IsTrue();
        await Assert.That(cronometro.Elapsed).IsLessThan(TimeSpan.FromSeconds(5));
    }

    /// <summary>QD1 com IL-01 (3 × 62 VA, demais locais) e TUG-01 (4 × 180 VA, cozinha), F+N 127 V.</summary>
    private (List<long> Luminarias, List<long> Tomadas) MontarIluminacaoETomadas()
    {
        var luminarias = Cenario.ColocarLuminarias(3);
        var tomadas = Cenario.ColocarTomadas(4);
        Classificar(luminarias, new ClassificacaoDeCarga(TipoDeCarga.Iluminacao, PotenciaVA: 62m, TensaoV: 127m, Fases: "F+N", Local: DemaisLocais));
        Classificar(tomadas, new ClassificacaoDeCarga(TipoDeCarga.TUG, PotenciaVA: 180m, TensaoV: 127m, Fases: "F+N", Local: Cozinha));
        CriacaoDeCircuitos.Executar([.. luminarias, .. tomadas], Cenario.Quadro.Id.Value, RegrasDoCenario, ConfiguracaoDeNumeracao.Padrao, Porta);
        return (luminarias, tomadas);
    }

    private IReadOnlyList<ResultadoDoCircuito> Dimensionar(IReadOnlyCollection<long> ids) =>
        DimensionamentoDoProjeto.Executar(ids, Condicoes, PerfilNormativo.NBR5410_2004, CatalogosDeProduto.Padrao, Dimensionamento);

    private void Classificar(IReadOnlyCollection<long> ids, ClassificacaoDeCarga classificacao)
    {
        var resultado = ClassificacaoEmLote.Executar(ids, classificacao, Porta);
        if (resultado.Classificados != ids.Count) throw new InvalidOperationException($"Classificação do cenário incompleta ({classificacao.Tipo}).");
    }

    /// <summary>Diferenças entre os parâmetros do circuito e o resultado do motor (vazio = tudo confere).</summary>
    private IEnumerable<string> ConferirGravados(ResultadoDoCircuito resultado)
    {
        var circuito = Elemento(resultado.Id);
        var calculo = resultado.Dimensionamento!;
        var esperados = new (string Nome, decimal? Valor, bool Corrente)[]
        {
            ("AMP_CorrenteProjetoA", calculo.CorrenteDeProjetoA, true),
            ("AMP_BitolaCondutorMm2", calculo.SecaoMm2, false),
            ("AMP_CapacidadeConducaoA", calculo.CapacidadeDeConducaoA, true),
            ("AMP_FCA", calculo.FCA, false),
            ("AMP_FCT", calculo.FCT, false),
            ("AMP_DisjuntorNominalA", calculo.DisjuntorA, false),
            ("AMP_IDR_NominalA", calculo.IdrNominalA, false),
            ("AMP_IDR_SensibilidadeMa", calculo.IdrSensibilidadeMa, false),
            ("AMP_QuedaTensaoPct", calculo.QuedaDeTensaoPct, false)
        };
        foreach (var (nome, valor, corrente) in esperados)
        {
            var parametro = Parametro(circuito, nome);
            if (valor is null)
            {
                if (parametro.HasValue) yield return $"{resultado.Numero}: {nome} gravado sem resultado";
                continue;
            }

            var gravado = corrente ? UnitUtils.ConvertFromInternalUnits(parametro.AsDouble(), UnitTypeId.Amperes) : parametro.AsDouble();
            if (Math.Abs(gravado - (double)valor.Value) > 1e-9) yield return $"{resultado.Numero}: {nome} = {gravado}, esperado {valor}";
        }

        if (calculo.SecaoMm2 is null || calculo.DisjuntorA is null) yield return $"{resultado.Numero}: cálculo não chegou ao disjuntor";
        if (Texto(circuito, "AMP_PerfilNorma") != PerfilNormativo.NBR5410_2004.Nome) yield return $"{resultado.Numero}: perfil '{Texto(circuito, "AMP_PerfilNorma")}'";
        if (Texto(circuito, "AMP_MemoriaCalculoId") != resultado.Memoria!.Hash()) yield return $"{resultado.Numero}: hash '{Texto(circuito, "AMP_MemoriaCalculoId")}'";
    }

    private int Armazens() => new FilteredElementCollector(Cenario.Documento).OfClass(typeof(DataStorage)).GetElementCount();

    private Element Elemento(long id) => Cenario.Documento.GetElement(new ElementId(id));

    private void Transacionar(Action acao)
    {
        using var transacao = new Transaction(Cenario.Documento, "Preparação do teste");
        transacao.Start();
        acao();
        transacao.Commit();
    }

    private static Parameter Parametro(Element elemento, string nome) =>
        elemento.get_Parameter(CatalogoDeParametros.Padrao.Parametros.Single(parametro => parametro.Nome == nome).Guid);

    private static string? Texto(Element elemento, string nome) => Parametro(elemento, nome)?.AsString();
}
