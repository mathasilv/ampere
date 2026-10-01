using Ampere.Core.Cargas;
using Ampere.Core.Demanda;

namespace Ampere.Tests.Core.Demanda;

[Property("Fonte", "CELG CT 04/18")]
public class PerfilDeDemanda_Teste
{
    private static readonly PerfilDeDemanda Celg = PerfilDeDemanda.CelgCt04_18;

    [Test]
    public async Task CT_04_18_carrega_com_as_referencias_das_tabelas()
    {
        await Assert.That(Celg.Nome).IsEqualTo("CELG CT 04/18");
        await Assert.That(Celg.Expressao).IsEqualTo("D = a + (b1 + b2 + b3 + b4 + b5 + b6 + b7 + b8) + c + d + e");
        await Assert.That(Celg.ReferenciaDaIluminacao).IsEqualTo("CELG CT 04/18, Anexo A, Tabela 2 (p. 15)");
        await Assert.That(Celg.ReferenciaDosAparelhos).IsEqualTo("CELG CT 04/18, Anexo A, Tabela 3 (p. 16)");
        await Assert.That(Celg.Edificacoes.Count).IsEqualTo(13);
        await Assert.That(Celg.Situacao).Contains("NT.00001.EQTL");
    }

    [Test]
    [Arguments(1, 100)]
    [Arguments(2, 68)]
    [Arguments(10, 30)]
    [Arguments(11, 30)]
    [Arguments(12, 29)]
    [Arguments(65, 24)]
    [Arguments(66, 23)]
    [Arguments(500, 23)]
    public async Task Chuveiros_pela_quantidade_com_o_limite_de_cima_incluido(int quantidade, int fatorPct)
    {
        var chuveiro = Celg.ColunaDe(Aparelho.Chuveiro)!;

        await Assert.That(new TabelaDeFaixas(Celg.ReferenciaDosAparelhos, chuveiro.Faixas).Para(quantidade).FatorPct).IsEqualTo(fatorPct);
    }

    [Test]
    public async Task Torneira_lava_loucas_e_aquecedor_de_passagem_dividem_a_coluna()
    {
        var coluna = Celg.ColunaDe(Aparelho.Torneira)!;

        await Assert.That(Celg.ColunaDe(Aparelho.LavaLoucas)).IsSameReferenceAs(coluna);
        await Assert.That(Celg.ColunaDe(Aparelho.AquecedorDePassagem)).IsSameReferenceAs(coluna);
        await Assert.That(Celg.ColunaDe(Aparelho.FornoOuFogao)).IsNull();
        await Assert.That(Celg.ColunaDe(Aparelho.Outro)).IsNull();
    }

    [Test]
    [Arguments(0.5, 86)]
    [Arguments(1, 86)]
    [Arguments(1.01, 75)]
    [Arguments(10, 27)]
    [Arguments(10.5, 24)]
    public async Task Residencias_pela_potencia_total_em_kW(double potenciaKw, int fatorPct)
    {
        var residencias = Celg.Edificacoes.Single(edificacao => edificacao.Residencial);

        await Assert.That(new TabelaDeFaixas(Celg.ReferenciaDaIluminacao, residencias.PorFaixa!).Para((decimal)potenciaKw).FatorPct).IsEqualTo(fatorPct);
    }

    [Test]
    public async Task Ar_condicionado_fornos_motores_e_solda()
    {
        await Assert.That(Celg.ArCondicionadoResidencial.Para(11).FatorPct).IsEqualTo(86m);
        await Assert.That(Celg.ArCondicionadoComercial.Para(101).FatorPct).IsEqualTo(75m);
        await Assert.That(Celg.LimiteDosFornosKw).IsEqualTo(3.5m);
        await Assert.That(Celg.FornosAcimaDoLimite.Para(16).FatorPct).IsEqualTo(26m);
        await Assert.That(Celg.FornosAteOLimite.Para(61).FatorPct).IsEqualTo(30m);
        await Assert.That(Celg.Motores.MaiorPct).IsEqualTo(80m);
        await Assert.That(string.Join("|", Celg.MaquinasDeSolda.PorOrdemPct)).IsEqualTo("100|70|40");
        await Assert.That(Celg.MaquinasDeSolda.DemaisPct).IsEqualTo(30m);
    }

    [Test]
    public async Task Escolas_escalonadas()
    {
        var escolas = Celg.Edificacoes.Single(edificacao => edificacao.Nome == "Escolas e semelhantes");

        await Assert.That(string.Join("|", escolas.Escalonado!.Select(faixa => $"{faixa.Ate}:{faixa.FatorPct}"))).IsEqualTo("12:100|:50");
    }

    [Test]
    [Arguments("[[1, 100], [2, 68], [3, 56]", "[[1, 100], [3, 68], [2, 56]", "faixas fora de ordem (3 e 2)")]
    [Arguments("[[1, 100], [2, 68], [3, 56]", "[[1, 100], [2, 0], [3, 56]", "fator fora de (0; 100]")]
    [Arguments("[null, 23]] },\n      { \"nome\": \"Torneira", "[66, 23]] },\n      { \"nome\": \"Torneira", "só a última faixa é aberta")]
    [Arguments("\"aparelhos\": [\"Chuveiro\"]", "\"aparelhos\": [\"Geladeira\"]", "aparelho 'Geladeira' desconhecido")]
    [Arguments("\"aparelhos\": [\"Micro-ondas\"]", "\"aparelhos\": [\"Chuveiro\"]", "Chuveiro: em 2 colunas")]
    [Arguments("\"fd_pct\": 86", "\"fd_pct\": 86, \"escalonado\": [[1, 50], [null, 40]]", "exatamente uma regra")]
    [Arguments("\"residencial\": [[10, 100], [20, 86]", "\"residencial\": [[10.5, 100], [20, 86]", "quantidade fracionária (10,5)")]
    [Arguments("\"aparelhos\": [\"Micro-ondas\"]", "\"aparelhos\": []", "coluna sem aparelhos")]
    public async Task Tabela_malformada_e_recusada(string trecho, string troca, string problema)
    {
        var json = ArquivoOficial();
        await Assert.That(json.Contains(trecho)).IsTrue();

        await Assert.That(() => PerfilDeDemanda.Carregar(ReplaceFirst(json, trecho, troca)))
            .Throws<PerfilDeDemandaInvalidoException>()
            .WithMessageContaining(problema);
    }

    private static string ArquivoOficial()
    {
        var pasta = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(pasta, "Ampere.sln"))) pasta = Path.GetDirectoryName(pasta)!;
        return File.ReadAllText(Path.Combine(pasta, "data", "distribuidoras", "CELG", "CT-04-2018", "demanda.json"));
    }

    private static string ReplaceFirst(string texto, string trecho, string troca)
    {
        var indice = texto.IndexOf(trecho, StringComparison.Ordinal);
        return texto[..indice] + troca + texto[(indice + trecho.Length)..];
    }
}
