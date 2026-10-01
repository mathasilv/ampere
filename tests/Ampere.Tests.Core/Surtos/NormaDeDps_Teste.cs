using Ampere.Core.Surtos;

namespace Ampere.Tests.Core.Surtos;

/// <summary>O carregador dos dados dos DPS recusa arquivo malformado em vez de adivinhar.</summary>
[Property("Fonte", "NBR 5410:2004, 6.3.5.2 e Tabelas 31 e 49")]
public class NormaDeDps_Teste
{
    [Test]
    public async Task Arquivo_embarcado_carrega()
    {
        var norma = NormaDeDps.Carregar(Recurso());

        await Assert.That(norma.Nome).IsEqualTo("NBR5410:2004");
        await Assert.That(norma.ReferenciaDoUc).StartsWith("NBR 5410:2004, item 6.3.5.2.4, alínea b");
        await Assert.That(norma.CorrenteDeImpulso.NeutroPeTrifasicaKa).IsEqualTo(50m);
    }

    [Test]
    [Arguments("\"uc\": \"√3 Uo\"", "\"uc\": \"raiz de 3\"", "Uc 'raiz de 3' ilegível")]
    [Arguments("\"esquema\": \"TN-C\", \"uc\": \"1,1 Uo\"", "\"esquema\": \"TN-C-S\", \"uc\": \"1,1 Uo\"", "esquema fora das colunas da Tabela 49")]
    [Arguments("\"ligacao\": \"fase-PEN\"", "\"ligacao\": \"fase-terra\"", "ligação fora de")]
    [Arguments("\"sistemas_trifasicos\": [\"400/690\"]", "\"sistemas_trifasicos\": [\"400/690\", \"230/400\"]", "Uo repetido em outra linha")]
    [Arguments("\"sistemas_trifasicos\": [\"400/690\"]", "\"sistemas_trifasicos\": [\"690/400\"]", "sistemas ausentes ou ilegíveis")]
    [Arguments("\"minima_a\": 100", "\"minima_a\": 0", "corrente_subsequente_neutro_pe sem ref ou sem valor positivo")]
    [Arguments("\"por_modo_ka\": 12.5", "\"por_modo_ka\": 12.5, \"extra\": 1", "JSON inválido")]
    [Arguments("\"ref\": \"NBR 5410:2004, item 6.3.5.2.8\"", "\"ref\": \"TODO_NORMA\"", "referência TODO_NORMA")]
    [Arguments("\"ref\": \"NBR 5410:2004, item 6.3.5.2.8\"", "\"ref\": \"Fictício 6.3.5.2.8\"", "arquivo real com referência fictícia")]
    public async Task Arquivo_malformado_e_recusado(string trecho, string troca, string problema)
    {
        var json = Recurso();
        await Assert.That(json).Contains(trecho);

        await Assert.That(() => NormaDeDps.Carregar(json.Replace(trecho, troca))).Throws<NormaDeDpsInvalidaException>().WithMessageContaining(problema);
    }

    [Test]
    [Arguments("1,1 Uo", 1.1, false)]
    [Arguments("Uo", 1, false)]
    [Arguments("U", 1, true)]
    public async Task Uc_da_Tabela_49_como_impresso(string texto, decimal fator, bool entreFases)
    {
        var uc = UcDaTabela.Ler(texto)!;

        await Assert.That(uc.Fator).IsEqualTo(fator);
        await Assert.That(uc.EntreFases).IsEqualTo(entreFases);
    }

    [Test]
    [Arguments("")]
    [Arguments("1.1 Uo")]
    [Arguments("0 Uo")]
    [Arguments("1,1 Un")]
    public async Task Uc_ilegivel_e_nulo(string texto)
    {
        await Assert.That(UcDaTabela.Ler(texto)).IsNull();
    }

    private static string Recurso()
    {
        using var fluxo = typeof(NormaDeDps).Assembly.GetManifestResourceStream("Ampere.Core.Surtos.NBR5410_2004.dps.json")!;
        using var leitor = new StreamReader(fluxo);
        return leitor.ReadToEnd();
    }
}
