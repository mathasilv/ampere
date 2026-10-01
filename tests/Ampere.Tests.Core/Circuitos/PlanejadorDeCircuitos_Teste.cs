using Ampere.Core.Cargas;
using Ampere.Core.Circuitos;
using Ampere.Core.Previsao;
using TUnit.Assertions.Enums;

namespace Ampere.Tests.Core.Circuitos;

public class PlanejadorDeCircuitos_Teste
{
    private const string A120 = "120 V · 1 polo";
    private const string A220 = "220 V · 2 polos";

    private static readonly IReadOnlyDictionary<TipoDeCarga, RegraDeAgrupamento> SemRegras =
        new Dictionary<TipoDeCarga, RegraDeAgrupamento>();

    [Test]
    public async Task Agrupa_por_tipo_na_ordem_dos_tipos_e_numera_por_prefixo()
    {
        var plano = Planejar([Ponto(3, TipoDeCarga.TUG), Ponto(1, TipoDeCarga.Iluminacao), Ponto(4, TipoDeCarga.TUG), Ponto(2, TipoDeCarga.Iluminacao)]);

        await Assert.That(Resumo(plano)).IsEquivalentTo(["IL-01: 1,2", "TUG-01: 3,4"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Alimentacoes_diferentes_nunca_dividem_circuito()
    {
        var plano = Planejar([Ponto(1, TipoDeCarga.TUG), Ponto(2, TipoDeCarga.TUG, alimentacao: A220), Ponto(3, TipoDeCarga.TUG)]);

        await Assert.That(Resumo(plano)).IsEquivalentTo(["TUG-01: 1,3", "TUG-02: 2"], CollectionOrdering.Matching);
    }

    [Test]
    [Property("Fonte", "NBR 5410:2004, item 9.5.3")]
    public async Task Divisao_da_instalacao_separa_as_tomadas_de_cozinha_e_isola_o_equipamento_acima_do_limite()
    {
        // Tomadas 1 e 2 na cozinha, 3 na sala; chuveiro 5 (5400 VA, 220 V, 24,5 A) e torneira 6 (1000 VA, 4,5 A) na habitação.
        var divisao = new DivisaoDaInstalacao(new HashSet<long> { 1, 2, 3, 5, 6 }, new HashSet<long> { 1, 2 }, 10m, "NBR 5410:2004, item 9.5.3");
        PontoDeCarga Tue(long id, decimal va) => Ponto(id, TipoDeCarga.TUE, va, A220) with { TensaoV = 220m, Fases = "2F" };

        var plano = PlanejadorDeCircuitos.Planejar(
            [Ponto(1, TipoDeCarga.TUG), Ponto(2, TipoDeCarga.TUG), Ponto(3, TipoDeCarga.TUG), Tue(5, 5400m), Tue(6, 1000m), Tue(7, 1200m)],
            SemRegras, ConfiguracaoDeNumeracao.Padrao, [], divisao);
        var semDivisao = Planejar([Ponto(1, TipoDeCarga.TUG), Ponto(2, TipoDeCarga.TUG), Ponto(3, TipoDeCarga.TUG)]);

        await Assert.That(Resumo(plano)).IsEquivalentTo(["TUG-01: 3", "TUG-02: 1,2", "TUE-01: 5", "TUE-02: 6,7"], CollectionOrdering.Matching);
        await Assert.That(string.Join("\n", plano.Avisos)).IsEqualTo(string.Join("\n",
            "2 tomada(s) de cozinha e áreas de serviço em circuitos só delas (NBR 5410:2004, item 9.5.3)",
            "ponto 5 (24,55 A) em circuito independente, acima de 10 A (NBR 5410:2004, item 9.5.3)"));
        await Assert.That(Resumo(semDivisao)).IsEquivalentTo(["TUG-01: 1,2,3"], CollectionOrdering.Matching);
    }

    [Test]
    [Property("Fonte", "NBR 5410:2004, item 9.5.3.1")]
    public async Task Equipamento_independente_no_meio_do_grupo_nao_separa_os_demais()
    {
        // 4, 6 e 8 seguem juntos; 5 (24,55 A) sozinho; 8 sem tensão: não dá para julgar, fica no grupo com aviso.
        var divisao = new DivisaoDaInstalacao(new HashSet<long> { 4, 5, 6, 8 }, new HashSet<long>(), 10m, "NBR 5410:2004, item 9.5.3");
        PontoDeCarga Tue(long id, decimal va) => Ponto(id, TipoDeCarga.TUE, va, A220) with { TensaoV = 220m, Fases = "2F" };

        var plano = PlanejadorDeCircuitos.Planejar([Tue(4, 1000m), Tue(5, 5400m), Tue(6, 1200m), Ponto(8, TipoDeCarga.TUE, 2000m, A220)],
            SemRegras, ConfiguracaoDeNumeracao.Padrao, [], divisao);

        await Assert.That(Resumo(plano)).IsEquivalentTo(["TUE-01: 4,6,8", "TUE-02: 5"], CollectionOrdering.Matching);
        await Assert.That(plano.Avisos[^1]).IsEqualTo(
            "ponto 8 (TUE) sem potência, tensão (AMP_TensaoCircuitoV) ou fases: não deu para ver se passa de 10 A e vai em circuito independente (NBR 5410:2004, item 9.5.3)");
    }

    [Test]
    public async Task Maximo_de_pontos_divide_o_grupo()
    {
        var regras = Regras(TipoDeCarga.TUG, new RegraDeAgrupamento(MaximoDePontos: 2));

        var plano = Planejar([Ponto(1, TipoDeCarga.TUG), Ponto(2, TipoDeCarga.TUG), Ponto(3, TipoDeCarga.TUG), Ponto(4, TipoDeCarga.TUG), Ponto(5, TipoDeCarga.TUG)], regras);

        await Assert.That(Resumo(plano)).IsEquivalentTo(["TUG-01: 1,2", "TUG-02: 3,4", "TUG-03: 5"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Maxima_potencia_divide_o_grupo_sem_passar_do_limite()
    {
        var regras = Regras(TipoDeCarga.TUG, new RegraDeAgrupamento(MaximaPotenciaVA: 1200m));

        var plano = Planejar([Ponto(1, TipoDeCarga.TUG, 600m), Ponto(2, TipoDeCarga.TUG, 600m), Ponto(3, TipoDeCarga.TUG, 600m), Ponto(4, TipoDeCarga.TUG, 1200m)], regras);

        await Assert.That(Resumo(plano)).IsEquivalentTo(["TUG-01: 1,2", "TUG-02: 3", "TUG-03: 4"], CollectionOrdering.Matching);
        await Assert.That(plano.Circuitos.Select(circuito => circuito.PotenciaTotalVA)).IsEquivalentTo(new decimal?[] { 1200m, 600m, 1200m }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Ponto_que_excede_sozinho_o_limite_fica_sozinho_e_gera_aviso()
    {
        var regras = Regras(TipoDeCarga.TUE, new RegraDeAgrupamento(MaximaPotenciaVA: 1500m));

        var plano = Planejar([Ponto(7, TipoDeCarga.TUE, 2000m), Ponto(8, TipoDeCarga.TUE, 500m)], regras);

        await Assert.That(Resumo(plano)).IsEquivalentTo(["TUE-01: 7", "TUE-02: 8"], CollectionOrdering.Matching);
        await Assert.That(string.Join("\n", plano.Avisos)).Contains("ponto 7 (2000 VA) excede sozinho o limite de 1500 VA de TUE");
    }

    [Test]
    public async Task Circuito_exclusivo_poe_cada_ponto_no_seu_circuito()
    {
        var regras = Regras(TipoDeCarga.TUE, new RegraDeAgrupamento(CircuitoExclusivo: true));

        var plano = Planejar([Ponto(1, TipoDeCarga.TUE), Ponto(2, TipoDeCarga.TUE), Ponto(3, TipoDeCarga.TUE)], regras);

        await Assert.That(Resumo(plano)).IsEquivalentTo(["TUE-01: 1", "TUE-02: 2", "TUE-03: 3"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Numeracao_continua_depois_do_maior_numero_existente_no_quadro()
    {
        var plano = Planejar(
            [Ponto(1, TipoDeCarga.TUG), Ponto(2, TipoDeCarga.Iluminacao)],
            existentes: ["TUG-01", "TUG-03", "IL-02", "qualquer coisa"]);

        await Assert.That(Resumo(plano)).IsEquivalentTo(["IL-03: 2", "TUG-04: 1"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Prefixo_do_quadro_entra_no_numero_e_na_sequencia()
    {
        var numeracao = ConfiguracaoDeNumeracao.Padrao with { PrefixoDoQuadro = "QD1" };

        var plano = Planejar([Ponto(1, TipoDeCarga.TUG)], numeracao: numeracao, existentes: ["QD1-TUG-02", "TUG-05"]);

        await Assert.That(Resumo(plano)).IsEquivalentTo(["QD1-TUG-03: 1"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Digitos_e_separador_sao_configuraveis()
    {
        var numeracao = ConfiguracaoDeNumeracao.Padrao with { Digitos = 3, Separador = "." };

        var plano = Planejar([Ponto(1, TipoDeCarga.Iluminacao)], numeracao: numeracao);

        await Assert.That(Resumo(plano)).IsEquivalentTo(["IL.001: 1"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Pontos_que_nao_podem_ser_circuitados_sao_ignorados_com_motivo()
    {
        var regras = Regras(TipoDeCarga.TUG, new RegraDeAgrupamento(MaximaPotenciaVA: 1000m));

        var plano = Planejar(
            [
                Ponto(1, null),
                Ponto(2, TipoDeCarga.Reserva),
                Ponto(3, TipoDeCarga.Iluminacao, circuito: "IL-09"),
                Ponto(4, TipoDeCarga.TUG, va: null),
                Ponto(5, TipoDeCarga.TUG)
            ],
            regras);

        await Assert.That(Resumo(plano)).IsEquivalentTo(["TUG-01: 5"], CollectionOrdering.Matching);
        await Assert.That(plano.Ignorados.Select(ignorado => $"{ignorado.Id}: {ignorado.Motivo}")).IsEquivalentTo(
        [
            "1: não classificado (AMP_TipoCarga vazio ou desconhecido)",
            "2: Reserva é tipo de circuito, não de ponto de carga",
            "3: já pertence ao circuito 'IL-09'",
            "4: sem potência (AMP_PotenciaInstaladaVA), exigida pelo limite de VA de TUG"
        ], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Ponto_repetido_na_entrada_conta_uma_vez()
    {
        var plano = Planejar([Ponto(1, TipoDeCarga.TUG), Ponto(1, TipoDeCarga.TUG)]);

        await Assert.That(Resumo(plano)).IsEquivalentTo(["TUG-01: 1"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Potencia_total_fica_indefinida_se_algum_ponto_nao_tiver_potencia()
    {
        var plano = Planejar([Ponto(1, TipoDeCarga.TUG, 100m), Ponto(2, TipoDeCarga.TUG, va: null)]);

        await Assert.That(plano.Circuitos[0].PotenciaTotalVA).IsNull();
    }

    [Test]
    public async Task Mesmas_entradas_em_outra_ordem_dao_o_mesmo_plano()
    {
        PontoDeCarga[] pontos = [Ponto(5, TipoDeCarga.TUE), Ponto(1, TipoDeCarga.TUG), Ponto(9, TipoDeCarga.Iluminacao), Ponto(2, TipoDeCarga.TUG, alimentacao: A220), Ponto(3, TipoDeCarga.TUG)];

        var direto = Resumo(Planejar(pontos));
        // Enumerable.Reverse explícito: no C# 14, array.Reverse() resolve para Span.Reverse (in-place, void).
        var invertido = Resumo(Planejar(Enumerable.Reverse(pontos).ToArray()));

        await Assert.That(invertido).IsEquivalentTo(direto, CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(0, null)]
    [Arguments(null, 0)]
    public async Task Regra_invalida_e_rejeitada_antes_de_planejar(int? maximoDePontos, int? maximaPotenciaVA)
    {
        var regra = new RegraDeAgrupamento(MaximoDePontos: maximoDePontos, MaximaPotenciaVA: maximaPotenciaVA);

        await Assert.That(regra.Validar()).IsNotEmpty();
        await Assert.That(() => Planejar([Ponto(1, TipoDeCarga.TUG)], Regras(TipoDeCarga.TUG, regra))).Throws<ArgumentException>();
    }

    private static PontoDeCarga Ponto(long id, TipoDeCarga? tipo, decimal? va = 100m, string alimentacao = A120, string? circuito = null) =>
        new(id, tipo, va, alimentacao, circuito);

    private static Dictionary<TipoDeCarga, RegraDeAgrupamento> Regras(TipoDeCarga tipo, RegraDeAgrupamento regra) => new() { [tipo] = regra };

    private static PlanoDeCircuitos Planejar(
        PontoDeCarga[] pontos,
        IReadOnlyDictionary<TipoDeCarga, RegraDeAgrupamento>? regras = null,
        ConfiguracaoDeNumeracao? numeracao = null,
        string[]? existentes = null) =>
        PlanejadorDeCircuitos.Planejar(pontos, regras ?? SemRegras, numeracao ?? ConfiguracaoDeNumeracao.Padrao, existentes ?? []);

    private static List<string> Resumo(PlanoDeCircuitos plano) =>
        plano.Circuitos.Select(circuito => $"{circuito.Numero}: {string.Join(",", circuito.Pontos)}").ToList();
}
