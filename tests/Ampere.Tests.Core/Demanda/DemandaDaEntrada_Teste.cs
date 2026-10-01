using Ampere.Core.Demanda;
using Ampere.Core.Memoria;

namespace Ampere.Tests.Core.Demanda;

/// <summary>
///     Demanda pela CELG CT 04/18. Casa: 10 luminárias de 100 VA e 20 TUG de 100 VA + 3 de 600 VA (P = 4,8 kVA), 2
///     chuveiros de 5400 VA, torneira 3000 VA, micro-ondas 1200 VA, forno 4000 VA, 2 ar-condicionados de 1650 VA, bomba
///     750 VA (fator do projetista 100%) e uma sauna "Outro" de 2000 VA.
/// </summary>
[Property("Fonte", "CELG CT 04/18")]
public class DemandaDaEntrada_Teste
{
    private static readonly PerfilDeDemanda Celg = PerfilDeDemanda.CelgCt04_18;
    private static readonly OpcoesDaDemanda Casa = new("Residências", RegraDeMotores.DoProjetista, 100m, "bomba de recalque, uso contínuo");

    internal static List<PontoDeDemanda> PontosDaCasa()
    {
        var pontos = new List<PontoDeDemanda>();
        long id = 1;
        void Adicionar(int quantos, string tipo, decimal potencia, string? aparelho = null)
        {
            for (var indice = 0; indice < quantos; indice++) pontos.Add(new PontoDeDemanda(id++, tipo, aparelho, potencia));
        }

        Adicionar(10, "Iluminação", 100m);
        Adicionar(20, "TUG", 100m);
        Adicionar(3, "TUG", 600m);
        Adicionar(2, "TUE", 5400m, "Chuveiro");
        Adicionar(1, "TUE", 3000m, "Torneira");
        Adicionar(1, "TUE", 1200m, "Micro-ondas");
        Adicionar(1, "TUE", 4000m, "Forno ou fogão");
        Adicionar(2, "ArCondicionado", 1650m);
        Adicionar(1, "Motor", 750m);
        Adicionar(1, "TUE", 2000m, "Outro");
        Adicionar(1, "Reserva", 9999m);
        return pontos;
    }

    internal static ResultadoDaDemanda DaCasa() => DemandaDaEntrada.Calcular(PontosDaCasa(), Casa, Celg);

    [Test]
    public async Task Casa_soma_as_parcelas_de_cada_tabela()
    {
        var resultado = DaCasa();

        await Assert.That(resultado.Problemas).IsEmpty();
        // a = 4,8 · 0,52 (4 < P ≤ 5) = 2,496; b1 = 10,8 · 0,68 = 7,344; b2 = 3; b6b = 4 · 0,8 = 3,2; b8 = 1,2; c = 3,3; d = 0,75; f = 2.
        await Assert.That(string.Join("|", resultado.Parcelas.Select(parcela => $"{parcela.Codigo}:{parcela.DemandaVA:0.###}")))
            .IsEqualTo("a:2496|b1:7344|b2:3000|b6b:3200|b8:1200|c:3300|d:750|f:2000");
        await Assert.That(resultado.DemandaVA).IsEqualTo(23290m);
        // A reserva não entra na potência instalada nem na demanda.
        await Assert.That(resultado.PotenciaInstaladaVA).IsEqualTo(4800m + 10800m + 3000m + 1200m + 4000m + 3300m + 750m + 2000m);
        var memoria = resultado.Memoria!;
        await Assert.That(memoria.Circuito).IsEqualTo("Demanda da entrada");
        await Assert.That(memoria.PerfilNorma).IsEqualTo("CELG CT 04/18");
        await Assert.That(Passo(memoria, "a — iluminação e TUG").Observacao!).Contains("faixa 4 < P ≤ 5 kW: 52%: o fator vale para P inteira, como impresso");
        await Assert.That(Passo(memoria, "f — outros aparelhos").Referencia).IsEqualTo(DemandaDaEntrada.CriterioDosOutros);
        await Assert.That(Passo(memoria, "Demanda total").Expressao).IsEqualTo("D = a + b1 + b2 + b6b + b8 + c + d + f");
    }

    [Test]
    public async Task Ponto_sem_tipo_sem_potencia_ou_TUE_sem_aparelho_para_com_os_ids()
    {
        var pontos = PontosDaCasa();
        pontos.Add(new PontoDeDemanda(100, null, null, 100m));
        pontos.Add(new PontoDeDemanda(101, "TUG", null, null));
        pontos.Add(new PontoDeDemanda(102, "TUE", null, 5400m));

        var resultado = DemandaDaEntrada.Calcular(pontos, Casa, Celg);

        await Assert.That(resultado.Memoria).IsNull();
        await Assert.That(resultado.DemandaVA).IsNull();
        await Assert.That(resultado.Problemas.Count).IsEqualTo(3);
        await Assert.That(resultado.Problemas[2]).StartsWith("1 ponto(s) TUE sem AMP_Aparelho");
        await Assert.That(string.Join(",", resultado.PontosComProblema)).IsEqualTo("100,101,102");
    }

    [Test]
    public async Task Motores_exigem_a_escolha_da_regra()
    {
        var sem = DemandaDaEntrada.Calcular(PontosDaCasa(), Casa with { Motores = null }, Celg);
        var semJustificativa = DemandaDaEntrada.Calcular(PontosDaCasa(), Casa with { Justificativa = " " }, Celg);

        await Assert.That(sem.Problemas.Single()).StartsWith("1 motor(es): escolha a regra — a do documento (edifícios residenciais de uso coletivo");
        await Assert.That(semJustificativa.Problemas.Single()).StartsWith("fator dos motores do projetista");
    }

    [Test]
    public async Task Motores_pela_regra_do_documento_80_no_maior_e_50_nos_demais()
    {
        PontoDeDemanda[] pontos = [new(1, "Motor", null, 3000m), new(2, "Motor", null, 1000m), new(3, "Motor", null, 2000m)];

        var resultado = DemandaDaEntrada.Calcular(pontos, new OpcoesDaDemanda("Residências", RegraDeMotores.DoDocumento), Celg);

        await Assert.That(resultado.Parcelas.Single(parcela => parcela.Codigo == "d").DemandaVA).IsEqualTo(3000m * 0.8m + 3000m * 0.5m);
        await Assert.That(Passo(resultado.Memoria!, "d — motores").Expressao).IsEqualTo("d = 0,8 · Pmaior + 0,5 · Pdemais");
    }

    [Test]
    public async Task Escola_escalonada_100_nos_primeiros_12_kW_e_50_no_que_exceder()
    {
        var pontos = Enumerable.Range(1, 15).Select(id => new PontoDeDemanda(id, "Iluminação", null, 1000m)).ToList();

        var resultado = DemandaDaEntrada.Calcular(pontos, new OpcoesDaDemanda("Escolas e semelhantes"), Celg);

        await Assert.That(resultado.DemandaVA).IsEqualTo(13500m);
        await Assert.That(Passo(resultado.Memoria!, "a — iluminação e TUG").Expressao).IsEqualTo("a = 12 · 1 + (P − 12) · 0,5");
    }

    [Test]
    public async Task Maquinas_de_solda_100_70_40_e_30_nos_demais()
    {
        PontoDeDemanda[] pontos = [new(1, "TUE", "Máquina de solda", 6000m), new(2, "TUE", "Máquina de solda", 10000m), new(3, "TUE", "Máquina de solda", 4000m),
            new(4, "TUE", "Máquina de solda", 8000m)];

        var resultado = DemandaDaEntrada.Calcular(pontos, new OpcoesDaDemanda("Residências"), Celg);

        // a = 0 (sem iluminação e TUG); e = 10 + 0,7 · 8 + 0,4 · 6 + 0,3 · 4 = 19,2 kVA.
        await Assert.That(resultado.DemandaVA).IsEqualTo(19200m);
        await Assert.That(Passo(resultado.Memoria!, "e — máquinas de solda").Expressao).IsEqualTo("e = 1 · P1 + 0,7 · P2 + 0,4 · P3 + 0,3 · Pdemais");
    }

    [Test]
    public async Task Ar_condicionado_comercial_fora_das_residencias_e_fornos_nas_duas_faixas()
    {
        var pontos = Enumerable.Range(1, 11).Select(id => new PontoDeDemanda(id, "ArCondicionado", null, 1000m)).ToList();
        pontos.Add(new PontoDeDemanda(20, "TUE", "Forno ou fogão", 3500m));
        pontos.Add(new PontoDeDemanda(21, "TUE", "Forno ou fogão", 3500m));
        pontos.Add(new PontoDeDemanda(22, "TUE", "Forno ou fogão", 6000m));

        var resultado = DemandaDaEntrada.Calcular(pontos, new OpcoesDaDemanda("Lojas e semelhantes"), Celg);

        // c: 11 aparelhos, coluna comercial 90%; b6a: 2 de 3,5 kW (no limite) a 75%; b6b: 1 de 6 kW a 80%.
        await Assert.That(string.Join("|", resultado.Parcelas.Select(parcela => $"{parcela.Codigo}:{parcela.DemandaVA:0.###}"))).IsEqualTo("a:0|b6a:5250|b6b:4800|c:9900");
    }

    [Test]
    public async Task Edificacao_fora_da_tabela_e_recusada()
    {
        var resultado = DemandaDaEntrada.Calcular(PontosDaCasa(), Casa with { Edificacao = "Estádios" }, Celg);

        await Assert.That(resultado.Problemas.Single()).StartsWith("edificação 'Estádios' fora da tabela de iluminação e tomadas");
    }

    [Test]
    public async Task Mesmos_pontos_em_outra_ordem_dao_a_mesma_memoria()
    {
        var invertidos = Enumerable.Reverse(PontosDaCasa()).ToList();

        await Assert.That(DemandaDaEntrada.Calcular(invertidos, Casa, Celg).Memoria!.Hash()).IsEqualTo(DaCasa().Memoria!.Hash());
    }

    private static PassoDeCalculo Passo(MemoriaDeCalculo memoria, string descricao) => memoria.Passos.Single(passo => passo.Descricao == descricao);
}
