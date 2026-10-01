using Ampere.Core.Parametros;
using TUnit.Assertions.Enums;

namespace Ampere.Tests.Core.Parametros;

public class CatalogoDeParametros_Teste
{
    private const string GuidA = "11111111-1111-1111-1111-111111111111";
    private const string GuidB = "22222222-2222-2222-2222-222222222222";

    [Test]
    public async Task Catalogo_padrao_carrega_do_recurso_embarcado()
    {
        var catalogo = CatalogoDeParametros.Padrao;

        await Assert.That(catalogo.GrupoRevit).IsEqualTo("Ampere");
        await Assert.That(catalogo.Parametros.Count).IsEqualTo(27);
    }

    [Test]
    public async Task Local_vai_nos_pontos_de_carga_e_a_memoria_tambem_nos_quadros()
    {
        var local = CatalogoDeParametros.Padrao.Parametros.Single(parametro => parametro.Nome == "AMP_Local");
        var memoria = CatalogoDeParametros.Padrao.Parametros.Single(parametro => parametro.Nome == "AMP_MemoriaCalculoId");
        var tipoDeCarga = CatalogoDeParametros.Padrao.Parametros.Single(parametro => parametro.Nome == "AMP_TipoCarga");

        // O local acompanha os elementos que a classificação aceita (os que recebem AMP_TipoCarga), menos o circuito.
        await Assert.That(local.Categorias).IsEquivalentTo(tipoDeCarga.Categorias.Where(categoria => categoria != CategoriaEletrica.CircuitosEletricos));
        await Assert.That(memoria.Categorias).IsEquivalentTo([CategoriaEletrica.EquipamentosEletricos, CategoriaEletrica.CircuitosEletricos]);
    }

    [Test]
    public async Task Json_valido_e_mapeado_para_definicoes_na_ordem_do_arquivo()
    {
        var catalogo = CatalogoDeParametros.Carregar(Json(
            Parametro(GuidA, "AMP_Texto", categorias: "\"Luminarias\", \"CircuitosEletricos\""),
            Parametro(GuidB, "AMP_Corrente", "CURRENT")));

        var texto = catalogo.Parametros[0];
        await Assert.That(texto.Guid).IsEqualTo(Guid.Parse(GuidA));
        await Assert.That(texto.Nome).IsEqualTo("AMP_Texto");
        await Assert.That(texto.Tipo).IsEqualTo(TipoDeDadoDoParametro.Texto);
        await Assert.That(texto.Descricao).IsEqualTo("teste");
        await Assert.That(texto.Categorias).IsEquivalentTo([CategoriaEletrica.Luminarias, CategoriaEletrica.CircuitosEletricos], CollectionOrdering.Matching);
        await Assert.That(catalogo.Parametros[1].Nome).IsEqualTo("AMP_Corrente");
    }

    [Test]
    [Arguments("TEXT", TipoDeDadoDoParametro.Texto)]
    [Arguments("NUMBER", TipoDeDadoDoParametro.Numero)]
    [Arguments("ELECTRICAL_APPARENT_POWER", TipoDeDadoDoParametro.PotenciaAparente)]
    [Arguments("CURRENT", TipoDeDadoDoParametro.Corrente)]
    [Arguments("LENGTH", TipoDeDadoDoParametro.Comprimento)]
    public async Task Codigo_de_tipo_e_mapeado(string codigo, TipoDeDadoDoParametro esperado)
    {
        var catalogo = CatalogoDeParametros.Carregar(Json(Parametro(tipo: codigo)));

        await Assert.That(catalogo.Parametros[0].Tipo).IsEqualTo(esperado);
    }

    [Test]
    public async Task Nome_sem_prefixo_AMP_e_rejeitado() =>
        await AssertRejeitado(Json(Parametro(nome: "Tipo_Carga")), "Tipo_Carga: nome deve começar com 'AMP_'");

    [Test]
    public async Task GUID_invalido_e_rejeitado() =>
        await AssertRejeitado(Json(Parametro(guid: "nao-e-guid")), "AMP_Teste: GUID inválido 'nao-e-guid'");

    [Test]
    public async Task GUID_vazio_e_rejeitado() =>
        await AssertRejeitado(Json(Parametro(guid: Guid.Empty.ToString())), "AMP_Teste: GUID inválido");

    [Test]
    public async Task GUID_repetido_e_rejeitado() =>
        await AssertRejeitado(
            Json(Parametro(GuidA, "AMP_Um"), Parametro(GuidA, "AMP_Dois")),
            $"GUID {GuidA} repetido em AMP_Um e AMP_Dois");

    [Test]
    public async Task Nome_repetido_sem_diferenciar_maiusculas_e_rejeitado() =>
        await AssertRejeitado(
            Json(Parametro(GuidA, "AMP_Teste"), Parametro(GuidB, "AMP_TESTE")),
            "nome repetido: AMP_TESTE");

    [Test]
    public async Task Tipo_desconhecido_e_rejeitado() =>
        await AssertRejeitado(Json(Parametro(tipo: "VOLTAGE")), "AMP_Teste: tipo desconhecido 'VOLTAGE'");

    [Test]
    public async Task Categoria_desconhecida_e_rejeitada() =>
        await AssertRejeitado(Json(Parametro(categorias: "\"Paredes\"")), "AMP_Teste: categoria desconhecida 'Paredes'");

    [Test]
    public async Task Categoria_numerica_e_rejeitada() =>
        await AssertRejeitado(Json(Parametro(categorias: "\"2\"")), "AMP_Teste: categoria desconhecida '2'");

    [Test]
    public async Task Categoria_repetida_e_rejeitada() =>
        await AssertRejeitado(
            Json(Parametro(categorias: "\"Luminarias\", \"Luminarias\"")),
            "AMP_Teste: categoria repetida 'Luminarias'");

    [Test]
    public async Task Parametro_sem_categoria_e_rejeitado() =>
        await AssertRejeitado(Json(Parametro(categorias: "")), "AMP_Teste: sem categorias");

    [Test]
    public async Task Campo_desconhecido_e_rejeitado_para_pegar_erro_de_digitacao() =>
        await AssertRejeitado(
            Json("""{ "guid": "11111111-1111-1111-1111-111111111111", "nome": "AMP_Teste", "tipo": "TEXT", "descricao": "teste", "categoria": ["Luminarias"] }"""),
            "categoria");

    [Test]
    public async Task Arquivo_sem_meta_e_rejeitado() =>
        await AssertRejeitado(
            $$"""{ "produto": "Ampere", "grupo_revit": "Ampere", "parametros": [{{Parametro()}}] }""",
            "$meta ausente");

    [Test]
    public async Task Todos_os_problemas_sao_relatados_juntos() =>
        await Assert.That(() => CatalogoDeParametros.Carregar(Json(Parametro(nome: "Sem_Prefixo", tipo: "VOLTAGE"))))
            .Throws<CatalogoDeParametrosInvalidoException>()
            .WithMessageContaining("Sem_Prefixo: nome deve começar com 'AMP_'")
            .And
            .WithMessageContaining("Sem_Prefixo: tipo desconhecido 'VOLTAGE'");

    private static async Task AssertRejeitado(string json, string trechoEsperado) =>
        await Assert.That(() => CatalogoDeParametros.Carregar(json))
            .Throws<CatalogoDeParametrosInvalidoException>()
            .WithMessageContaining(trechoEsperado);

    private static string Json(params string[] parametros) =>
        $$"""
        {
          "$meta": { "fonte": "teste" },
          "produto": "Ampere",
          "grupo_revit": "Ampere",
          "parametros": [{{string.Join(", ", parametros)}}]
        }
        """;

    private static string Parametro(
        string guid = GuidA,
        string nome = "AMP_Teste",
        string tipo = "TEXT",
        string categorias = "\"CircuitosEletricos\"") =>
        $$"""{ "guid": "{{guid}}", "nome": "{{nome}}", "tipo": "{{tipo}}", "descricao": "teste", "categorias": [{{categorias}}] }""";
}
