using Ampere.Core.Catalogos;
using TUnit.Assertions.Enums;

namespace Ampere.Tests.Core.Catalogos;

public class CatalogoDeCondutores_Teste
{
    private static readonly CatalogoDeCondutores Ficticio = CatalogoDeCondutores.Carregar(CatalogosFicticios.Condutores);

    [Test]
    [Property("Fonte", "Prysmian BW_005_02_PT e LV_006_01_PT")]
    public async Task Catalogo_oficial_tem_os_cabos_Prysmian_com_a_ficha_de_cada_um()
    {
        var superastic = CatalogoDeCondutores.Padrao.DiametroExternoMm("Prysmian Superastic Flex 450/750 V", 2.5m);
        var sintenax = CatalogoDeCondutores.Padrao.DiametroExternoMm("Prysmian Sintenax Flex 0,6/1 kV unipolar", 2.5m);

        await Assert.That(CatalogoDeCondutores.Padrao.Ficticio).IsFalse();
        await Assert.That(superastic.Valor).IsEqualTo(3.5m);
        await Assert.That(superastic.Referencia).StartsWith("Prysmian, ficha técnica Superastic Flex 450/750 V (rodapé BW_005_02_PT)");
        await Assert.That(sintenax.Valor).IsEqualTo(5.4m);
        await Assert.That(sintenax.Referencia).StartsWith("Prysmian, ficha técnica Sintenax Flex 0,6/1 kV (rodapé LV_006_01_PT)");
        await Assert.That(CatalogoDeCondutores.Padrao.Isolacao("Prysmian Superastic Flex 450/750 V")!.Value.Isolacao).IsEqualTo("PVC");
    }

    [Test]
    public async Task Isolacoes_do_catalogo_oficial_existem_no_perfil()
    {
        var doPerfil = Ampere.Core.Normas.PerfilNormativo.NBR5410_2004.Vocabulario.Isolacoes;

        foreach (var tipo in CatalogoDeCondutores.Padrao.Tipos)
            await Assert.That(doPerfil.Contains(CatalogoDeCondutores.Padrao.Isolacao(tipo)!.Value.Isolacao)).IsTrue();
    }

    [Test]
    public async Task Tipos_dos_catalogos_para_o_dialogo()
    {
        await Assert.That(Ficticio.Tipos).IsEquivalentTo([CatalogosFicticios.TipoDeCondutor]);
        await Assert.That(CatalogoDeEletrodutos.Carregar(CatalogosFicticios.Eletrodutos).Tipos).IsEquivalentTo([CatalogosFicticios.TipoDeEletroduto]);
        await Assert.That(string.Join("|", CatalogoDeCondutores.Padrao.Tipos)).IsEqualTo("Prysmian Superastic Flex 450/750 V|Prysmian Sintenax Flex 0,6/1 kV unipolar");
    }

    [Test]
    public async Task Consulta_devolve_diametro_com_a_referencia_do_catalogo()
    {
        var diametro = Ficticio.DiametroExternoMm(CatalogosFicticios.TipoDeCondutor, 2.5m);

        await Assert.That(diametro.Valor).IsEqualTo(4m);
        await Assert.That(diametro.Referencia).IsEqualTo("FICTÍCIO: catálogo de condutores");
    }

    [Test]
    public async Task Tipo_com_ref_propria_cita_a_ficha_dele()
    {
        var json = CatalogosFicticios.Condutores.Replace("{ \"tipo\": \"FIO-TESTE\",", "{ \"tipo\": \"FIO-TESTE\", \"ref\": \"FICTÍCIO: ficha do FIO-TESTE\",");

        var catalogo = CatalogoDeCondutores.Carregar(json);

        await Assert.That(catalogo.DiametroExternoMm(CatalogosFicticios.TipoDeCondutor, 2.5m).Referencia).IsEqualTo("FICTÍCIO: ficha do FIO-TESTE");
        await Assert.That(catalogo.DiametroExternoMm(CatalogosFicticios.TipoDeCondutor, 35m).Referencia).IsEqualTo("FICTÍCIO: ficha do FIO-TESTE");
    }

    [Test]
    [Arguments("")]
    [Arguments("TODO_CATALOGO")]
    [Arguments("Prysmian, ficha real")]
    public async Task Ref_do_tipo_vazia_pendente_ou_real_em_catalogo_ficticio_e_rejeitada(string referencia)
    {
        var json = CatalogosFicticios.Condutores.Replace("{ \"tipo\": \"FIO-TESTE\",", $"{{ \"tipo\": \"FIO-TESTE\", \"ref\": \"{referencia}\",");

        await Assert.That(() => CatalogoDeCondutores.Carregar(json)).Throws<CatalogoDeProdutoInvalidoException>().WithMessageContaining("FIO-TESTE: ref do tipo");
    }

    [Test]
    public async Task Tipo_ou_secao_fora_do_catalogo_e_ausencia_explicada()
    {
        await Assert.That(Ficticio.DiametroExternoMm("Cabo 1kV EPR", 2.5m).Ausencia).Contains("tipo de condutor 'Cabo 1kV EPR' fora do catálogo");
        await Assert.That(Ficticio.DiametroExternoMm(CatalogosFicticios.TipoDeCondutor, 35m).Ausencia).Contains("sem diâmetro para 35 mm²");
    }

    [Test]
    [Arguments(null)]
    [Arguments(" ")]
    public async Task Tipo_de_condutor_nao_informado_e_ausencia_explicada(string? tipo)
    {
        var diametro = Ficticio.DiametroExternoMm(tipo, 2.5m);

        await Assert.That(diametro.Ausencia).IsEqualTo("tipo de condutor não informado (no circuito ou nas condições do projeto)");
        await Assert.That(diametro.Referencia).IsEqualTo("FICTÍCIO: catálogo de condutores");
    }

    [Test]
    public async Task Catalogo_TODO_com_valores_e_rejeitado()
    {
        var json = CatalogosFicticios.Condutores.Replace("\"ref\": \"FICTÍCIO: catálogo de condutores\"", "\"ref\": \"TODO_CATALOGO\"");

        await Assert.That(() => CatalogoDeCondutores.Carregar(json))
            .Throws<CatalogoDeProdutoInvalidoException>()
            .WithMessageContaining("tem valores mas a ref é TODO_CATALOGO");
    }

    [Test]
    public async Task Catalogo_real_nao_pode_citar_referencia_ficticia()
    {
        var json = CatalogosFicticios.Condutores.Replace("\"ficticio\": true", "\"ficticio\": false");

        await Assert.That(() => CatalogoDeCondutores.Carregar(json))
            .Throws<CatalogoDeProdutoInvalidoException>()
            .WithMessageContaining("catálogo real com referência fictícia");
    }
}

public class CatalogoDeEletrodutos_Teste
{
    private static readonly CatalogoDeEletrodutos Ficticio = CatalogoDeEletrodutos.Carregar(CatalogosFicticios.Eletrodutos);

    [Test]
    [Property("Fonte", "Tigre: FT Tigreflex 09/2025, FT Tigreflex Reforçado 10/2025, FT Roscável 10/2013")]
    public async Task Catalogo_oficial_tem_os_eletrodutos_Tigre_com_a_ficha_de_cada_um()
    {
        var roscavel = CatalogoDeEletrodutos.Padrao.Tamanhos("Tigre PVC rígido roscável");

        await Assert.That(string.Join("|", CatalogoDeEletrodutos.Padrao.Tipos)).IsEqualTo("Tigre Tigreflex amarelo|Tigre Tigreflex Reforçado|Tigre PVC rígido roscável");
        await Assert.That(roscavel.Valor[0]).IsEqualTo(new TamanhoDeEletroduto("½\"", 16.4m));
        await Assert.That(roscavel.Referencia).StartsWith("Tigre, Ficha Técnica Eletroduto de PVC Rígido Roscável (outubro/2013)");
        await Assert.That(CatalogoDeEletrodutos.Padrao.Tamanhos("Tigre Tigreflex amarelo").Valor.Select(tamanho => tamanho.DiametroInternoMm))
            .IsEquivalentTo([15m, 19.5m, 25.7m], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Tamanhos_vem_em_ordem_crescente_de_diametro_interno()
    {
        var tamanhos = Ficticio.Tamanhos(CatalogosFicticios.TipoDeEletroduto);

        await Assert.That(tamanhos.Valor.Select(tamanho => tamanho.Nominal)).IsEquivalentTo(["A", "B", "C", "D"], CollectionOrdering.Matching);
        await Assert.That(tamanhos.Referencia).IsEqualTo("FICTÍCIO: catálogo de eletrodutos");
    }

    [Test]
    public async Task Tipo_fora_do_catalogo_e_ausencia_explicada()
    {
        await Assert.That(Ficticio.Tamanhos("Aço galvanizado").Ausencia).Contains("tipo de eletroduto 'Aço galvanizado' fora do catálogo");
    }

    [Test]
    public async Task Tipo_com_ref_propria_cita_a_ficha_dele()
    {
        var json = CatalogosFicticios.Eletrodutos.Replace("{ \"tipo\": \"ELETRODUTO-TESTE\",", "{ \"tipo\": \"ELETRODUTO-TESTE\", \"ref\": \"FICTÍCIO: ficha do eletroduto\",");

        await Assert.That(CatalogoDeEletrodutos.Carregar(json).Tamanhos(CatalogosFicticios.TipoDeEletroduto).Referencia).IsEqualTo("FICTÍCIO: ficha do eletroduto");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    public async Task Tipo_de_eletroduto_nao_informado_e_ausencia_explicada(string? tipo)
    {
        await Assert.That(Ficticio.Tamanhos(tipo).Ausencia).IsEqualTo("tipo de eletroduto não informado (nas condições do projeto)");
    }

    [Test]
    public async Task Tamanho_nominal_repetido_e_rejeitado()
    {
        var json = CatalogosFicticios.Eletrodutos.Replace("\"nominal\": \"B\"", "\"nominal\": \"A\"");

        await Assert.That(() => CatalogoDeEletrodutos.Carregar(json))
            .Throws<CatalogoDeProdutoInvalidoException>()
            .WithMessageContaining("tamanho nominal 'A' repetido");
    }
}
