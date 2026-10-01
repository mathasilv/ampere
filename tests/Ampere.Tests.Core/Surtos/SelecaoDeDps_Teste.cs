using System.Globalization;
using Ampere.Core.Surtos;

namespace Ampere.Tests.Core.Surtos;

/// <summary>Seleção dos DPS do quadro principal (NBR 5410, 6.3.5.2, Figura 13 e Tabelas 31 e 49).</summary>
[Property("Fonte", "NBR 5410:2004, 6.3.5.2.2, 6.3.5.2.4, 6.3.5.2.6, 6.3.5.2.9 e Tabelas 31 e 49")]
public class SelecaoDeDps_Teste
{
    private static readonly NormaDeDps Norma = NormaDeDps.NBR5410_2004;

    /// <summary>QD1 trifásico 220/127 V com neutro.</summary>
    public static QuadroParaDps Trifasico(string esquema = "3F+N", decimal uo = 127m, decimal? u = 220m) =>
        new(1, "QD1", esquema, uo, u, "sistema de distribuição '220/127 Y' do quadro");

    /// <summary>O caso dos golden files: TT, 3F+N 220/127 V, DPS a montante do DR contra as duas finalidades.</summary>
    public static ResultadoDoDps DoQuadroTt() =>
        SelecaoDeDps.Selecionar(Trifasico(), new EscolhaDoDps(EsquemasDeAterramento.Tt, FinalidadeDoDps.Ambas), Norma);

    [Test]
    public async Task Tabelas_transcritas()
    {
        await Assert.That(Norma.Uc.Count).IsEqualTo(11);
        await Assert.That(Norma.Uc[(LigacoesDoDps.FasePe, EsquemasDeAterramento.ItComNeutro)].Texto).IsEqualTo("√3 Uo");
        await Assert.That(Norma.Uc[(LigacoesDoDps.FasePe, EsquemasDeAterramento.ItSemNeutro)].EntreFases).IsTrue();
        await Assert.That(Norma.Uc.ContainsKey((LigacoesDoDps.FaseNeutro, EsquemasDeAterramento.TnC))).IsFalse();
        await Assert.That(string.Join("|", Norma.Tabela31.Select(linha => Inv(linha.CategoriaIiKv)))).IsEqualTo("1.5|2.5|4");
        await Assert.That(string.Join("|", Norma.Tabela31.Select(linha => Inv(linha.CategoriaIvKv)))).IsEqualTo("4|6|8");
        await Assert.That(Norma.LinhaDaTensao(115m)!.CategoriaIiKv).IsEqualTo(1.5m);
        await Assert.That(Norma.LinhaDaTensao(230m)!.CategoriaIiKv).IsEqualTo(2.5m);
        await Assert.That(Norma.LinhaDaTensao(240m)).IsNull();
        await Assert.That(Norma.AConferir.Count).IsEqualTo(5);
    }

    [Test]
    public async Task TN_C_S_vai_no_esquema_1_entre_cada_fase_e_o_PEN()
    {
        var resultado = SelecaoDeDps.Selecionar(Trifasico(), new EscolhaDoDps(EsquemasDeAterramento.TnCS, FinalidadeDoDps.LinhaExterna), Norma);

        await Assert.That(resultado.Problemas).IsEmpty();
        await Assert.That(resultado.EsquemaDeConexao).IsEqualTo(1);
        await Assert.That(resultado.Ligacoes.Count).IsEqualTo(1);
        var fases = resultado.Ligacoes[0];
        await Assert.That(fases.Ligacao).IsEqualTo(LigacoesDoDps.FasePen);
        await Assert.That(fases.Quantidade).IsEqualTo(3);
        await Assert.That(fases.UcMinimoV).IsEqualTo(139.7m);
        await Assert.That(fases.InMinimoKa).IsEqualTo(5m);
        await Assert.That(fases.IimpMinimoKa).IsNull();
        await Assert.That(resultado.UpMaximoKv).IsEqualTo(1.5m);
        await Assert.That(resultado.SecaoDoCondutorDeConexaoMm2).IsEqualTo(4m);
        var memoria = resultado.Memoria!;
        await Assert.That(memoria.Passos[0].Observacao!).Contains("nota b");
        await Assert.That(memoria.Passos.Any(passo => passo.Descricao.Contains("Corrente subsequente"))).IsFalse();
    }

    [Test]
    public async Task TT_a_montante_do_DR_obriga_o_esquema_3()
    {
        var resultado = SelecaoDeDps.Selecionar(Trifasico(), new EscolhaDoDps(EsquemasDeAterramento.Tt, FinalidadeDoDps.LinhaExterna, EsquemaDeConexao: 2), Norma);

        await Assert.That(resultado.EsquemaDeConexao).IsEqualTo(3);
        await Assert.That(string.Join("|", resultado.Ligacoes.Select(ligacao => $"{ligacao.Ligacao}:{ligacao.Quantidade}:{Inv(ligacao.UcMinimoV)}:{Inv(ligacao.InMinimoKa)}")))
            .IsEqualTo("fase-neutro:3:139.7:5|neutro-PE:1:127:20");
        var passos = resultado.Memoria!.Passos;
        await Assert.That(passos[0].Referencia).Contains("6.3.5.2.6, alínea b");
        await Assert.That(passos[0].Observacao!).Contains("o esquema 2 escolhido não se aplica");
        await Assert.That(passos.Single(passo => passo.Descricao.StartsWith("Corrente subsequente", StringComparison.Ordinal)).Resultado).IsEqualTo(100m);
        await Assert.That(passos.Any(passo => passo.Descricao.StartsWith("Imunidade", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task TN_S_com_neutro_pede_a_escolha_entre_os_esquemas_2_e_3()
    {
        var semEscolha = SelecaoDeDps.Selecionar(Trifasico(), new EscolhaDoDps(EsquemasDeAterramento.TnS, FinalidadeDoDps.LinhaExterna), Norma);
        var esquema2 = SelecaoDeDps.Selecionar(Trifasico(), new EscolhaDoDps(EsquemasDeAterramento.TnS, FinalidadeDoDps.LinhaExterna, 2), Norma);

        await Assert.That(semEscolha.Memoria).IsNull();
        await Assert.That(semEscolha.Problemas.Single()).Contains("escolha um");
        await Assert.That(SelecaoDeDps.EscolheOEsquemaDeConexao("3F+N", EsquemasDeAterramento.TnS, aJusanteDeDr: false)).IsTrue();
        await Assert.That(string.Join("|", esquema2.Ligacoes.Select(ligacao => $"{ligacao.Ligacao}:{ligacao.Quantidade}:{Inv(ligacao.UcMinimoV)}:{Inv(ligacao.InMinimoKa)}")))
            .IsEqualTo("fase-PE:3:139.7:5|neutro-PE:1:127:5");
    }

    [Test]
    public async Task IT_com_neutro_pede_raiz_de_3_Uo_entre_fase_e_PE()
    {
        var resultado = SelecaoDeDps.Selecionar(Trifasico(uo: 220m, u: 380m),
            new EscolhaDoDps(EsquemasDeAterramento.ItComNeutro, FinalidadeDoDps.LinhaExterna, 2), Norma);

        await Assert.That(Math.Round(resultado.Ligacoes[0].UcMinimoV, 3)).IsEqualTo(381.051m);
        await Assert.That(resultado.Memoria!.Passos.Single(passo => passo.Descricao == "Uc mínimo — fase–PE").Expressao).IsEqualTo("Uc ≥ √3 · Uo");
        await Assert.That(resultado.UpMaximoKv).IsEqualTo(2.5m);
    }

    [Test]
    public async Task IT_sem_neutro_pede_U_e_recusa_quadro_com_neutro()
    {
        var semNeutro = SelecaoDeDps.Selecionar(Trifasico("3F", 220m, 380m), new EscolhaDoDps(EsquemasDeAterramento.ItSemNeutro, FinalidadeDoDps.LinhaExterna), Norma);
        var comNeutro = SelecaoDeDps.Selecionar(Trifasico(), new EscolhaDoDps(EsquemasDeAterramento.ItSemNeutro, FinalidadeDoDps.LinhaExterna), Norma);

        await Assert.That(semNeutro.EsquemaDeConexao).IsEqualTo(1);
        await Assert.That(semNeutro.Ligacoes.Single().UcMinimoV).IsEqualTo(380m);
        await Assert.That(comNeutro.Problemas.Single()).Contains("IT sem neutro");
    }

    [Test]
    public async Task Descargas_diretas_pedem_Iimp_e_condutor_de_16_mm2()
    {
        var resultado = SelecaoDeDps.Selecionar(new QuadroParaDps(2, "QGBT", "F+N", 127m, null, "teste"),
            new EscolhaDoDps(EsquemasDeAterramento.TnS, FinalidadeDoDps.DescargasDiretas, 3), Norma);

        await Assert.That(string.Join("|", resultado.Ligacoes.Select(ligacao => $"{ligacao.Ligacao}:{Inv(ligacao.InMinimoKa)}:{Inv(ligacao.IimpMinimoKa)}")))
            .IsEqualTo("fase-neutro::12.5|neutro-PE::25");
        await Assert.That(resultado.SecaoDoCondutorDeConexaoMm2).IsEqualTo(16m);
    }

    [Test]
    public async Task Duas_fases_tomadas_como_rede_trifasica_no_neutro_PE()
    {
        var resultado = SelecaoDeDps.Selecionar(Trifasico("2F+N", 127m, 254m),
            new EscolhaDoDps(EsquemasDeAterramento.TnS, FinalidadeDoDps.LinhaExterna, 3), Norma);

        var neutroPe = resultado.Ligacoes.Single(ligacao => ligacao.Ligacao == LigacoesDoDps.NeutroPe);
        await Assert.That(neutroPe.InMinimoKa).IsEqualTo(20m);
        await Assert.That(resultado.Memoria!.Passos.Single(passo => passo.Descricao == "Corrente nominal de descarga mínima (In) — neutro–PE").Observacao!)
            .Contains("critério do Ampere");
    }

    [Test]
    public async Task DR_a_montante_dos_DPS_precisa_de_imunidade_de_3_kA()
    {
        var resultado = SelecaoDeDps.Selecionar(Trifasico(), new EscolhaDoDps(EsquemasDeAterramento.Tt, FinalidadeDoDps.LinhaExterna, 2, AJusanteDeDr: true), Norma);

        await Assert.That(resultado.EsquemaDeConexao).IsEqualTo(2);
        await Assert.That(resultado.Memoria!.Passos.Single(passo => passo.Descricao.StartsWith("Imunidade", StringComparison.Ordinal)).Resultado).IsEqualTo(3m);
        await Assert.That(SelecaoDeDps.EscolheOEsquemaDeConexao("3F+N", EsquemasDeAterramento.Tt, aJusanteDeDr: false)).IsFalse();
        await Assert.That(SelecaoDeDps.EscolheOEsquemaDeConexao("3F+N", EsquemasDeAterramento.Tt, aJusanteDeDr: true)).IsTrue();
        await Assert.That(SelecaoDeDps.EscolheOEsquemaDeConexao("3F", EsquemasDeAterramento.TnS, aJusanteDeDr: false)).IsFalse();
    }

    [Test]
    [Arguments(null, 127, "sem sistema de distribuição")]
    [Arguments("3F+N", 240, "fora da Tabela 31")]
    [Arguments("3F+N", 0, "sem a tensão fase-neutro")]
    public async Task Quadro_sem_o_que_a_selecao_precisa_nao_sai(string? esquema, int uo, string problema)
    {
        var resultado = SelecaoDeDps.Selecionar(new QuadroParaDps(3, "QD", esquema, uo, 400m, "teste"),
            new EscolhaDoDps(EsquemasDeAterramento.TnCS, FinalidadeDoDps.LinhaExterna), Norma);

        await Assert.That(resultado.Memoria).IsNull();
        await Assert.That(string.Join("|", resultado.Problemas)).Contains(problema);
    }

    [Test]
    public async Task Esquema_de_aterramento_desconhecido_nao_sai()
    {
        var resultado = SelecaoDeDps.Selecionar(Trifasico(), new EscolhaDoDps("TN", FinalidadeDoDps.LinhaExterna), Norma);

        await Assert.That(resultado.Problemas.Single()).Contains("esquema de aterramento 'TN'");
    }

    [Test]
    public async Task Resumo_em_poucas_linhas()
    {
        await Assert.That(DoQuadroTt().Resumo()).IsEqualTo(
            "QD1: esquema de conexão 3 (Figura 13), 4 DPS\n" +
            "• 3 × fase–neutro: Uc ≥ 139,7 V · In ≥ 5 kA · Iimp ≥ 12,5 kA\n" +
            "• 1 × neutro–PE: Uc ≥ 127 V · In ≥ 20 kA · Iimp ≥ 50 kA\n" +
            "Up ≤ 1,5 kV · condutor de conexão ao PE ≥ 16 mm² de cobre");
        await Assert.That(SelecaoDeDps.Selecionar(Trifasico(), new EscolhaDoDps("TN", FinalidadeDoDps.LinhaExterna), Norma).Resumo())
            .StartsWith("QD1: os DPS não foram selecionados.\n• esquema de aterramento 'TN'");
    }

    [Test]
    public async Task Mesmas_entradas_mesmo_hash()
    {
        await Assert.That(DoQuadroTt().Memoria!.Hash()).IsEqualTo(DoQuadroTt().Memoria!.Hash());
    }

    private static string Inv(decimal? valor) => valor?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
}
