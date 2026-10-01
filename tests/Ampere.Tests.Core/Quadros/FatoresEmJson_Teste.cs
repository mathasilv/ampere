using Ampere.Core.Cargas;
using Ampere.Core.Quadros;

namespace Ampere.Tests.Core.Quadros;

public class FatoresEmJson_Teste
{
    [Test]
    public async Task Texto_canonico_e_ida_e_volta()
    {
        var fatores = new Dictionary<TipoDeCarga, decimal> { [TipoDeCarga.TUG] = 0.5m, [TipoDeCarga.Iluminacao] = 1m };

        var json = FatoresEmJson.Escrever(fatores);

        await Assert.That(json).IsEqualTo("""{"versao":1,"fatores":{"Iluminacao":1,"TUG":0.5}}""");
        await Assert.That(string.Join(";", FatoresEmJson.Ler(json)!.OrderBy(par => par.Key).Select(par => $"{par.Key}={par.Value}"))).IsEqualTo("Iluminacao=1;TUG=0.5");
    }

    [Test]
    public async Task Sem_fatores_informados_e_um_objeto_vazio_e_nao_nulo()
    {
        await Assert.That(FatoresEmJson.Ler(FatoresEmJson.Escrever(null))!.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("{")]
    [Arguments("""{"versao":2,"fatores":{}}""")]
    [Arguments("""{"versao":1}""")]
    [Arguments("""{"versao":1,"fatores":{"Chuveiro":0.5}}""")]
    [Arguments("""{"versao":1,"fatores":{"TUG":1.5}}""")]
    [Arguments("""{"versao":1,"fatores":{"TUG":0}}""")]
    [Arguments("""{"versao":1,"fatores":{"TUG":"0.5"}}""")]
    public async Task Texto_invalido_nao_vira_fator(string? json)
    {
        await Assert.That(FatoresEmJson.Ler(json)).IsNull();
    }

    [Test]
    public async Task GUID_do_esquema_esta_congelado()
    {
        await Assert.That(Guid.Parse(FatoresEmJson.GuidDoEsquema)).IsEqualTo(Guid.Parse("7957990d-8c18-437e-ba7e-e508a8c9f418"));
    }
}
