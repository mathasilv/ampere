using Ampere.Core.Demanda;

namespace Ampere.Tests.Core.Demanda;

/// <summary>Padrão de entrada pela carga instalada (Equatorial NT.00001.EQTL rev. 09, Tabelas 1 e 2).</summary>
[Property("Fonte", "Equatorial NT.00001.EQTL rev. 09, Tabelas 1 e 2")]
public class PadraoDeEntrada_Teste
{
    private static readonly NormaDoPadraoDeEntrada Norma = NormaDoPadraoDeEntrada.EquatorialNt00001Rev09;

    [Test]
    public async Task Tabelas_transcritas_com_as_tensoes_e_os_fornecimentos()
    {
        await Assert.That(string.Join("|", Norma.Tabelas.Select(tabela => tabela.Descricao))).IsEqualTo("Tabela 1 — 220/380 V|Tabela 2 — 127/220 V");
        await Assert.That(string.Join("|", Norma.Tabelas[0].Fornecimentos.Select(fornecimento => fornecimento.Nome))).IsEqualTo("monofásico|trifásico");
        await Assert.That(string.Join("|", Norma.Tabelas[1].Fornecimentos.Select(fornecimento => fornecimento.Nome))).IsEqualTo("monofásico|bifásico|trifásico");
        await Assert.That(Norma.Tabelas[1].Fornecimentos[2].Linhas.Count).IsEqualTo(8);
    }

    [Test]
    [Arguments("3/4", 0.75)]
    [Arguments("1/2", 0.5)]
    [Arguments("1", 1)]
    [Arguments("1.1/2", 1.5)]
    [Arguments("2.1/2", 2.5)]
    [Arguments("3", 3)]
    public async Task Polegadas_como_impressas(string texto, decimal polegadas)
    {
        await Assert.That(NormaDoPadraoDeEntrada.Polegadas(texto)).IsEqualTo(polegadas);
    }

    [Test]
    [Arguments("")]
    [Arguments("1.5")]
    [Arguments("1/0")]
    [Arguments("a/b")]
    public async Task Polegadas_ilegiveis_sao_nulas(string texto)
    {
        await Assert.That(NormaDoPadraoDeEntrada.Polegadas(texto)).IsNull();
    }

    [Test]
    [Arguments("Tabela 1", 4000, "monofásico", 25)]
    [Arguments("Tabela 1", 5000, "monofásico", 25)]
    [Arguments("Tabela 1", 5050, "monofásico", 40)]
    [Arguments("Tabela 1", 12000, "monofásico", 63)]
    [Arguments("Tabela 1", 13000, "trifásico", 40)]
    [Arguments("Tabela 1", 75000, "trifásico", 125)]
    [Arguments("Tabela 2", 9000, "monofásico", 80)]
    [Arguments("Tabela 2", 12000, "bifásico", 63)]
    [Arguments("Tabela 2", 16000, "trifásico", 50)]
    [Arguments("Tabela 2", 50000, "trifásico", 150)]
    public async Task Pela_carga_o_menor_fornecimento_que_atende(string tabela, decimal instaladaVA, string fornecimento, decimal disjuntorA)
    {
        var resultado = PadraoDeEntrada.Calcular(instaladaVA, new EscolhaDoPadrao(tabela), Norma);

        await Assert.That(resultado.Problemas).IsEmpty();
        await Assert.That(resultado.Fornecimento!.Nome).IsEqualTo(fornecimento);
        await Assert.That(resultado.Linha!.DisjuntorA).IsEqualTo(disjuntorA);
    }

    [Test]
    public async Task Memoria_tem_a_linha_da_tabela_com_a_faixa_e_os_materiais()
    {
        var resultado = PadraoDeEntrada.Calcular(50000m, new EscolhaDoPadrao("Tabela 2"), Norma);

        var passos = resultado.Memoria!.Passos;
        await Assert.That(passos[0].Resultado).IsEqualTo(50m);
        await Assert.That(passos[0].Observacao).Contains("potência instalada em kVA tomada como kW");
        await Assert.That(passos[2].Referencia).EndsWith("Tabela 2 – Dimensionamento do Ramal de Conexão e Entrada das Instalações em 127/220V; trifásico, acima de 44 e até 52 kW");
        await Assert.That(string.Join("|", passos.Skip(2).Select(passo => $"{passo.Descricao}={passo.Resultado}"))).IsEqualTo(string.Join("|",
            "Disjuntor termomagnético=150", "Condutor do cliente — fase=50", "Condutor do cliente — neutro=25", "Eletroduto de aço galvanizado=3",
            "Condutor de aterramento (aço cobreado)=25", "Eletroduto do aterramento=1", "Ramal de conexão até 2 km da orla marítima — cobre multiplexado=50",
            "Ramal de conexão a partir de 2 km da orla marítima — alumínio multiplexado quadruplex=70"));
        await Assert.That(resultado.Memoria.Hash()).IsEqualTo(PadraoDeEntrada.Calcular(50000m, new EscolhaDoPadrao("Tabela 2"), Norma).Memoria!.Hash());
    }

    [Test]
    public async Task Fornecimento_escolhido_acima_do_menor_usa_a_primeira_faixa_com_a_nota()
    {
        var resultado = PadraoDeEntrada.Calcular(10000m, new EscolhaDoPadrao("Tabela 1", "trifásico"), Norma);

        await Assert.That(resultado.Linha!.DisjuntorA).IsEqualTo(20m);
        await Assert.That(resultado.Memoria!.Passos[1].Expressao).IsEqualTo("escolhido pelo projetista");
        await Assert.That(resultado.Memoria.Passos[2].Observacao).StartsWith("Nota 19: nos casos em que a unidade consumidora possuir carga instalada de até 12 kW");
    }

    [Test]
    public async Task Carga_fora_da_tabela_ou_do_fornecimento_escolhido_e_problema()
    {
        await Assert.That(PadraoDeEntrada.Calcular(80000m, new EscolhaDoPadrao("Tabela 1"), Norma).Problemas.Single())
            .IsEqualTo("carga instalada de 80 kW acima da Tabela 1 (até 75 kW): o padrão não sai da tabela");
        await Assert.That(PadraoDeEntrada.Calcular(11000m, new EscolhaDoPadrao("Tabela 2", "monofásico"), Norma).Problemas.Single())
            .IsEqualTo("carga instalada de 11 kW acima do limite do fornecimento monofásico na Tabela 2 (10 kW)");
        await Assert.That(PadraoDeEntrada.Calcular(1000m, new EscolhaDoPadrao("Tabela 2", "bifásico"), Norma).Linha!.DisjuntorA).IsEqualTo(50m);
        await Assert.That(PadraoDeEntrada.Calcular(1000m, new EscolhaDoPadrao("Tabela 3"), Norma).Problemas.Single()).StartsWith("tabela 'Tabela 3' fora do padrão");
        await Assert.That(PadraoDeEntrada.Calcular(0m, new EscolhaDoPadrao("Tabela 1"), Norma).Memoria).IsNull();
    }

    [Test]
    public async Task Arquivo_malformado_e_recusado()
    {
        using var fluxo = typeof(NormaDoPadraoDeEntrada).Assembly.GetManifestResourceStream("Ampere.Core.Demanda.EQTL_NT.00001-09.padrao_de_entrada.json")!;
        using var leitor = new StreamReader(fluxo);
        var json = leitor.ReadToEnd();

        await Assert.That(() => NormaDoPadraoDeEntrada.Carregar(json.Replace("\"ate_kw\": 8,", "\"ate_kw\": 4,")))
            .Throws<PadraoDeEntradaInvalidoException>().WithMessageContaining("fora da ordem crescente de carga");
        await Assert.That(() => NormaDoPadraoDeEntrada.Carregar(json.Replace("\"eletroduto_aco_galvanizado_pol\": \"3/4\"", "\"eletroduto_aco_galvanizado_pol\": \"3/4 pol\"")))
            .Throws<PadraoDeEntradaInvalidoException>().WithMessageContaining("eletroduto sem diâmetro em polegadas legível");
        await Assert.That(() => NormaDoPadraoDeEntrada.Carregar(json.Replace("\"nota\": \"Nota 19\"", "\"nota\": \"Nota 99\"")))
            .Throws<PadraoDeEntradaInvalidoException>().WithMessageContaining("Nota 99 sem texto em 'notas'");
    }
}
