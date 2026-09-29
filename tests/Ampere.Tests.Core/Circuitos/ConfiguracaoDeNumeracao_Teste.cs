using Ampere.Core.Cargas;
using Ampere.Core.Circuitos;

namespace Ampere.Tests.Core.Circuitos;

public class ConfiguracaoDeNumeracao_Teste
{
    [Test]
    public async Task Padrao_e_valido()
    {
        await Assert.That(ConfiguracaoDeNumeracao.Padrao.Validar()).IsEmpty();
    }

    [Test]
    public async Task Prefixo_vazio_e_rejeitado()
    {
        var numeracao = ComPrefixo(TipoDeCarga.TUG, " ");

        await Assert.That(string.Join("\n", numeracao.Validar())).Contains("prefixo de TUG vazio");
    }

    [Test]
    public async Task Prefixo_repetido_e_rejeitado_porque_duplicaria_numeros()
    {
        var numeracao = ComPrefixo(TipoDeCarga.TUE, "tug");

        await Assert.That(string.Join("\n", numeracao.Validar())).Contains("prefixo 'tug' repetido em TUG e TUE");
    }

    [Test]
    public async Task Tipo_sem_prefixo_e_rejeitado()
    {
        var prefixos = ConfiguracaoDeNumeracao.Padrao.Prefixos.Where(par => par.Key != TipoDeCarga.Motor).ToDictionary();
        var numeracao = ConfiguracaoDeNumeracao.Padrao with { Prefixos = prefixos };

        await Assert.That(string.Join("\n", numeracao.Validar())).Contains("sem prefixo para Motor");
    }

    [Test]
    [Arguments(0)]
    [Arguments(7)]
    public async Task Digitos_fora_de_1_a_6_sao_rejeitados(int digitos)
    {
        var numeracao = ConfiguracaoDeNumeracao.Padrao with { Digitos = digitos };

        await Assert.That(string.Join("\n", numeracao.Validar())).Contains("dígitos devem estar entre 1 e 6");
    }

    [Test]
    public async Task Planejar_com_numeracao_invalida_e_rejeitado_antes_de_planejar()
    {
        var numeracao = ComPrefixo(TipoDeCarga.TUE, "TUG");
        PontoDeCarga[] pontos = [new(1, TipoDeCarga.TUG, 100m, "120 V · 1 polo")];

        await Assert.That(() => PlanejadorDeCircuitos.Planejar(pontos, new Dictionary<TipoDeCarga, RegraDeAgrupamento>(), numeracao, []))
            .Throws<ArgumentException>();
    }

    private static ConfiguracaoDeNumeracao ComPrefixo(TipoDeCarga tipo, string prefixo)
    {
        var prefixos = ConfiguracaoDeNumeracao.Padrao.Prefixos.ToDictionary();
        prefixos[tipo] = prefixo;
        return ConfiguracaoDeNumeracao.Padrao with { Prefixos = prefixos };
    }
}
