using Ampere.Core.Parametros;

namespace Ampere.Tests.Core.Parametros;

public class PlanejadorDeInjecao_Teste
{
    private static readonly Guid GuidTeste = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OutroGuid = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private static readonly DefinicaoDeParametro Teste = new(
        GuidTeste,
        "AMP_Teste",
        TipoDeDadoDoParametro.Texto,
        "teste",
        [CategoriaEletrica.Luminarias, CategoriaEletrica.CircuitosEletricos]);

    [Test]
    public async Task Documento_sem_parametros_gera_Criar_para_todo_o_catalogo_na_ordem()
    {
        var catalogo = CatalogoDeParametros.Padrao.Parametros;

        var plano = PlanejadorDeInjecao.Planejar(catalogo, []);

        await Assert.That(plano.Acoes.All(acao => acao is AcaoDeInjecao.Criar)).IsTrue();
        await Assert.That(plano.Acoes.Select(acao => acao.Definicao).SequenceEqual(catalogo)).IsTrue();
        await Assert.That(plano.TemConflitos).IsFalse();
    }

    [Test]
    public async Task Aplicar_o_plano_e_replanejar_nao_gera_mais_acoes()
    {
        var catalogo = CatalogoDeParametros.Padrao.Parametros;
        var documento = Simular(PlanejadorDeInjecao.Planejar(catalogo, []), []);

        var replanejado = PlanejadorDeInjecao.Planejar(catalogo, documento);

        await Assert.That(replanejado.NadaAFazer).IsTrue();
    }

    [Test]
    public async Task Aplicar_sobre_documento_parcial_e_replanejar_nao_gera_mais_acoes()
    {
        ParametroExistente[] parcial = [Vinculado(CategoriaEletrica.CircuitosEletricos)];
        var documento = Simular(PlanejadorDeInjecao.Planejar([Teste], parcial), parcial);

        var replanejado = PlanejadorDeInjecao.Planejar([Teste], documento);

        await Assert.That(replanejado.NadaAFazer).IsTrue();
    }

    [Test]
    public async Task Vinculo_com_todas_as_categorias_fica_JaConforme()
    {
        var plano = PlanejadorDeInjecao.Planejar([Teste], [Vinculado(CategoriaEletrica.Luminarias, CategoriaEletrica.CircuitosEletricos)]);

        await Assert.That(plano.Acoes[0]).IsTypeOf<AcaoDeInjecao.JaConforme>();
    }

    [Test]
    public async Task Vinculo_com_categorias_a_mais_fica_JaConforme_porque_nada_e_removido()
    {
        var plano = PlanejadorDeInjecao.Planejar(
            [Teste],
            [Vinculado(CategoriaEletrica.Luminarias, CategoriaEletrica.CircuitosEletricos, CategoriaEletrica.EquipamentosEletricos)]);

        await Assert.That(plano.Acoes[0]).IsTypeOf<AcaoDeInjecao.JaConforme>();
    }

    [Test]
    public async Task Vinculo_sem_parte_das_categorias_gera_AmpliarCategorias_so_com_as_faltantes()
    {
        var plano = PlanejadorDeInjecao.Planejar([Teste], [Vinculado(CategoriaEletrica.CircuitosEletricos)]);

        await Assert.That(plano.Acoes[0]).IsTypeOf<AcaoDeInjecao.AmpliarCategorias>();
        var ampliar = (AcaoDeInjecao.AmpliarCategorias)plano.Acoes[0];
        await Assert.That(ampliar.Faltantes).IsEquivalentTo([CategoriaEletrica.Luminarias]);
    }

    [Test]
    public async Task Parametro_compartilhado_existente_sem_vinculo_gera_Criar()
    {
        var plano = PlanejadorDeInjecao.Planejar([Teste], [new ParametroExistente("AMP_Teste", GuidTeste, TipoDeDadoDoParametro.Texto, null)]);

        await Assert.That(plano.Acoes[0]).IsTypeOf<AcaoDeInjecao.Criar>();
    }

    [Test]
    public async Task Parametros_alheios_ao_Ampere_sao_ignorados()
    {
        var alheio = new ParametroExistente("Comentários", null, TipoDeDadoDoParametro.Texto,
            new VinculoExistente(true, [CategoriaEletrica.Luminarias]));

        var plano = PlanejadorDeInjecao.Planejar([Teste], [alheio]);

        await Assert.That(plano.Acoes[0]).IsTypeOf<AcaoDeInjecao.Criar>();
    }

    [Test]
    public async Task Homonimo_com_outro_GUID_gera_Conflito()
    {
        var plano = PlanejadorDeInjecao.Planejar([Teste], [new ParametroExistente("AMP_Teste", OutroGuid, TipoDeDadoDoParametro.Texto, null)]);

        await Assert.That(await MotivoDoConflito(plano)).Contains("outro GUID");
    }

    [Test]
    public async Task Homonimo_nao_compartilhado_gera_Conflito()
    {
        var plano = PlanejadorDeInjecao.Planejar([Teste], [new ParametroExistente("AMP_Teste", null, TipoDeDadoDoParametro.Texto, null)]);

        await Assert.That(await MotivoDoConflito(plano)).Contains("não compartilhado");
    }

    [Test]
    public async Task Homonimo_com_maiusculas_diferentes_gera_Conflito()
    {
        var plano = PlanejadorDeInjecao.Planejar([Teste], [new ParametroExistente("amp_teste", OutroGuid, TipoDeDadoDoParametro.Texto, null)]);

        await Assert.That(await MotivoDoConflito(plano)).Contains("outro GUID");
    }

    [Test]
    public async Task Mesmo_GUID_com_outro_nome_gera_Conflito()
    {
        var plano = PlanejadorDeInjecao.Planejar([Teste], [new ParametroExistente("OutroNome", GuidTeste, TipoDeDadoDoParametro.Texto, null)]);

        await Assert.That(await MotivoDoConflito(plano)).Contains("já pertence ao parâmetro 'OutroNome'");
    }

    [Test]
    public async Task Mesmo_GUID_com_outro_tipo_de_dado_gera_Conflito()
    {
        var plano = PlanejadorDeInjecao.Planejar([Teste], [new ParametroExistente("AMP_Teste", GuidTeste, TipoDeDadoDoParametro.Numero, null)]);

        await Assert.That(await MotivoDoConflito(plano)).Contains("outro tipo de dado");
    }

    [Test]
    public async Task Mesmo_GUID_vinculado_por_tipo_gera_Conflito()
    {
        var porTipo = new ParametroExistente("AMP_Teste", GuidTeste, TipoDeDadoDoParametro.Texto,
            new VinculoExistente(false, [CategoriaEletrica.Luminarias, CategoriaEletrica.CircuitosEletricos]));

        var plano = PlanejadorDeInjecao.Planejar([Teste], [porTipo]);

        await Assert.That(await MotivoDoConflito(plano)).Contains("parâmetro de tipo");
    }

    private static ParametroExistente Vinculado(params CategoriaEletrica[] categorias) =>
        new("AMP_Teste", GuidTeste, TipoDeDadoDoParametro.Texto, new VinculoExistente(true, categorias));

    private static async Task<string> MotivoDoConflito(PlanoDeInjecao plano)
    {
        await Assert.That(plano.TemConflitos).IsTrue();
        await Assert.That(plano.Acoes[0]).IsTypeOf<AcaoDeInjecao.Conflito>();
        return ((AcaoDeInjecao.Conflito)plano.Acoes[0]).Motivo;
    }

    /// <summary>
    ///     Efeito esperado da aplicação de um plano sem conflitos. O efeito real no Revit é verificado nos testes de
    ///     integração do adapter.
    /// </summary>
    private static List<ParametroExistente> Simular(PlanoDeInjecao plano, IReadOnlyCollection<ParametroExistente> antes)
    {
        var depois = antes.ToList();
        foreach (var acao in plano.Acoes)
        {
            var definicao = acao.Definicao;
            switch (acao)
            {
                case AcaoDeInjecao.Criar:
                    depois.RemoveAll(existente => existente.Guid == definicao.Guid);
                    depois.Add(new ParametroExistente(definicao.Nome, definicao.Guid, definicao.Tipo,
                        new VinculoExistente(true, definicao.Categorias)));
                    break;
                case AcaoDeInjecao.AmpliarCategorias ampliar:
                    var atual = depois.Single(existente => existente.Guid == definicao.Guid);
                    depois.Remove(atual);
                    depois.Add(atual with
                    {
                        Vinculo = atual.Vinculo! with { CategoriasAmpere = [.. atual.Vinculo.CategoriasAmpere, .. ampliar.Faltantes] }
                    });
                    break;
            }
        }

        return depois;
    }
}
