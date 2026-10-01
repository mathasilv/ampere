using Ampere.Core.Catalogos;
using TUnit.Assertions.Enums;

namespace Ampere.Tests.Core.Catalogos;

public class CatalogoDeCondutores_Teste
{
    private static readonly CatalogoDeCondutores Ficticio = CatalogoDeCondutores.Carregar(CatalogosFicticios.Condutores);

    [Test]
    public async Task Catalogo_oficial_esta_vazio_ate_a_escolha_do_fabricante()
    {
        var diametro = CatalogoDeCondutores.Padrao.DiametroExternoMm("Fio 750V PVC", 2.5m);

        await Assert.That(CatalogoDeCondutores.Padrao.Ficticio).IsFalse();
        await Assert.That(diametro.Disponivel).IsFalse();
        await Assert.That(diametro.Referencia).IsEqualTo("TODO_CATALOGO");
    }

    [Test]
    public async Task Consulta_devolve_diametro_com_a_referencia_do_catalogo()
    {
        var diametro = Ficticio.DiametroExternoMm(CatalogosFicticios.TipoDeCondutor, 2.5m);

        await Assert.That(diametro.Valor).IsEqualTo(4m);
        await Assert.That(diametro.Referencia).IsEqualTo("FICTÍCIO: catálogo de condutores");
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
    public async Task Catalogo_oficial_esta_vazio_ate_a_escolha_do_fabricante()
    {
        var tamanhos = CatalogoDeEletrodutos.Padrao.Tamanhos("PVC rígido roscável");

        await Assert.That(tamanhos.Disponivel).IsFalse();
        await Assert.That(tamanhos.Referencia).IsEqualTo("TODO_CATALOGO");
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
