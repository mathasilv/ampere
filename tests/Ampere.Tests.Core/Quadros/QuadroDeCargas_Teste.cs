using Ampere.Core.Cargas;
using Ampere.Core.Normas;
using Ampere.Core.Quadros;
using Ampere.Tests.Core.Normas;

namespace Ampere.Tests.Core.Quadros;

/// <summary>
///     Quadro de cargas (especificação §3, F1.4): demanda por tipo de carga, corrente de demanda e memória auditável.
/// </summary>
/// <remarks>
///     Fatores do perfil fictício (0,8 / 0,5 / 1), não da NBR 5410. Fonte normativa: TODO_NORMA
///     (data/DATA_GAPS.md, GAP-005).
/// </remarks>
[Property("Fonte", "TODO_NORMA")]
public class QuadroDeCargas_Teste
{
    private static readonly PerfilNormativo Perfil = PerfilNormativo.Carregar(PerfilFicticio.Json);

    private static readonly IReadOnlyList<CircuitoDoQuadro> Circuitos =
    [
        new("IL-01", "Iluminação sala", TipoDeCarga.Iluminacao, 600m),
        new("IL-02", "Iluminação cozinha", TipoDeCarga.Iluminacao, 400m),
        new("TUG-01", "Tomadas sala", TipoDeCarga.TUG, 1000m),
        new("TUE-01", "Chuveiro", TipoDeCarga.TUE, 2000m)
    ];

    [Test]
    public async Task Monta_subtotais_por_tipo_com_o_fator_do_perfil()
    {
        var quadro = QuadroDeCargas.Montar("QD-01", "F+N", 220m, Circuitos, Perfil);

        await Assert.That(quadro.Problemas).IsEmpty();
        await Assert.That(quadro.Subtotais.Single(s => s.Tipo == TipoDeCarga.Iluminacao).DemandaVA).IsEqualTo(800m);
        await Assert.That(quadro.Subtotais.Single(s => s.Tipo == TipoDeCarga.TUG).DemandaVA).IsEqualTo(500m);
        await Assert.That(quadro.Subtotais.Single(s => s.Tipo == TipoDeCarga.TUE).DemandaVA).IsEqualTo(2000m);
        await Assert.That(quadro.PotenciaInstaladaVA).IsEqualTo(4000m);
        await Assert.That(quadro.DemandaVA).IsEqualTo(3300m);
    }

    [Test]
    public async Task Subtotais_seguem_a_ordem_de_primeira_aparencia_do_tipo()
    {
        var quadro = QuadroDeCargas.Montar("QD-01", "F+N", 220m, Circuitos, Perfil);

        await Assert.That(string.Join(",", quadro.Subtotais.Select(subtotal => subtotal.Tipo)))
            .IsEqualTo("Iluminacao,TUG,TUE");
    }

    [Test]
    public async Task Corrente_monofasica_e_demanda_sobre_tensao()
    {
        var quadro = QuadroDeCargas.Montar("QD-01", "F+N", 220m, Circuitos, Perfil);

        await Assert.That(quadro.CorrenteDeDemandaA).IsEqualTo(15m);
    }

    [Test]
    public async Task Corrente_trifasica_divide_pela_raiz_de_tres()
    {
        var quadro = QuadroDeCargas.Montar("QD-01", "3F+N", 380m, Circuitos, Perfil);

        await Assert.That(quadro.CorrenteDeDemandaA!.Value).IsEqualTo(3300m / (1.7320508075688772935274463415m * 380m));
    }

    [Test]
    public async Task Fator_informado_pelo_projetista_vence_o_do_perfil()
    {
        var quadro = QuadroDeCargas.Montar("QD-01", "F+N", 220m, Circuitos, Perfil,
            new Dictionary<TipoDeCarga, decimal> { [TipoDeCarga.Iluminacao] = 1m });

        var iluminacao = quadro.Subtotais.Single(subtotal => subtotal.Tipo == TipoDeCarga.Iluminacao);
        await Assert.That(iluminacao.DemandaVA).IsEqualTo(1000m);
        await Assert.That(iluminacao.FonteDoFator).IsEqualTo(QuadroDeCargas.FonteInformadaPeloProjetista);
        await Assert.That(quadro.DemandaVA).IsEqualTo(3500m);
    }

    [Test]
    public async Task Fator_informado_fora_do_intervalo_e_rejeitado()
    {
        var quadro = QuadroDeCargas.Montar("QD-01", "F+N", 220m, Circuitos, Perfil,
            new Dictionary<TipoDeCarga, decimal> { [TipoDeCarga.Iluminacao] = 1.5m });

        await Assert.That(quadro.DemandaVA).IsNull();
        await Assert.That(quadro.Problemas.Any(problema => problema.Contains("fora do intervalo (0, 1]"))).IsTrue();
    }

    /// <summary>Perfil fictício sem a tabela de fatores: simula o perfil oficial até a norma chegar.</summary>
    private static PerfilNormativo PerfilSemFatores() => PerfilNormativo.Carregar(
        PerfilFicticio.Json.Replace(
            "\"fator_de_demanda_por_tipo\": { \"ref\": \"FICTÍCIO: fatores de demanda\", \"valores\": { \"Iluminacao\": 0.8, \"TUG\": 0.5, \"TUE\": 1, \"ArCondicionado\": 1, \"Motor\": 1, \"Reserva\": 1 } }",
            "\"fator_de_demanda_por_tipo\": { \"ref\": \"TODO_NORMA\", \"valores\": {} }"));

    [Test]
    public async Task Sem_fator_em_lugar_nenhum_o_quadro_para_com_o_motivo()
    {
        var quadro = QuadroDeCargas.Montar("QD-01", "F+N", 220m, Circuitos, PerfilSemFatores());

        await Assert.That(quadro.DemandaVA).IsNull();
        await Assert.That(quadro.Memoria).IsNull();
        await Assert.That(quadro.CorrenteDeDemandaA).IsNull();
        await Assert.That(quadro.PotenciaInstaladaVA).IsEqualTo(4000m);
        await Assert.That(quadro.Problemas.Any(problema => problema.Contains("TODO_NORMA"))).IsTrue();
        await Assert.That(quadro.Linhas.All(linha => linha.DemandaVA is null)).IsTrue();
    }

    [Test]
    public async Task Esquema_desconhecido_deixa_a_corrente_sem_calculo_mas_com_demanda()
    {
        var quadro = QuadroDeCargas.Montar("QD-01", "2F+N", 220m, Circuitos, Perfil);

        await Assert.That(quadro.DemandaVA).IsEqualTo(3300m);
        await Assert.That(quadro.CorrenteDeDemandaA).IsNull();
        await Assert.That(quadro.Problemas.Any(problema => problema.Contains("esquema desconhecido"))).IsTrue();
        await Assert.That(quadro.Memoria).IsNotNull();
        await Assert.That(quadro.Memoria!.Passos[^1].Resultado).IsNull();
    }

    [Test]
    public async Task Numero_de_circuito_repetido_aparece_nos_problemas()
    {
        var repetidos = new List<CircuitoDoQuadro>(Circuitos) { new("TUG-01", "Repetido", TipoDeCarga.TUG, 100m) };

        var quadro = QuadroDeCargas.Montar("QD-01", "F+N", 220m, repetidos, Perfil);

        await Assert.That(quadro.Problemas.Any(problema => problema.Contains("repetido"))).IsTrue();
    }

    [Test]
    public async Task Memoria_registra_a_fonte_de_cada_fator_e_o_hash_e_deterministico()
    {
        var primeiro = QuadroDeCargas.Montar("QD-01", "F+N", 220m, Circuitos, Perfil);
        var segundo = QuadroDeCargas.Montar("QD-01", "F+N", 220m, Circuitos, Perfil);

        await Assert.That(primeiro.Memoria!.Passos[0].Referencia).IsEqualTo("FICTÍCIO: fatores de demanda");
        var passoDaDemandaTotal = primeiro.Memoria!.Passos.Single(passo => passo.Descricao == "Demanda total do quadro");
        await Assert.That(passoDaDemandaTotal.Referencia).IsEqualTo("FICTÍCIO: regra de demanda do quadro");
        await Assert.That(primeiro.Memoria!.Circuito).IsEqualTo("QD-01");
        await Assert.That(primeiro.Memoria!.Hash()).IsEqualTo(segundo.Memoria!.Hash());
    }

    [Test]
    public async Task Quadro_sem_circuitos_fica_incompleto()
    {
        var quadro = QuadroDeCargas.Montar("QD-01", "F+N", 220m, [], Perfil);

        await Assert.That(quadro.DemandaVA).IsNull();
        await Assert.That(quadro.Memoria).IsNull();
        await Assert.That(quadro.Problemas.Any(problema => problema.Contains("sem circuitos"))).IsTrue();
    }
}
