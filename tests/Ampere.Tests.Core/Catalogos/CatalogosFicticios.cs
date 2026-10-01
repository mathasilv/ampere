namespace Ampere.Tests.Core.Catalogos;

/// <summary>
///     Catálogos FICTÍCIOS, só para testar o motor. Os diâmetros são propositalmente redondos e NÃO são de nenhum
///     fabricante.
/// </summary>
internal static class CatalogosFicticios
{
    public const string TipoDeCondutor = "FIO-TESTE";
    public const string TipoDeEletroduto = "ELETRODUTO-TESTE";

    public const string Condutores = """
        {
          "$meta": { "fonte": "fictício, só para testes", "versao": "0", "data": "2026-09-29", "ficticio": true },
          "catalogo": "condutores",
          "ref": "FICTÍCIO: catálogo de condutores",
          "tipos": [
            { "tipo": "FIO-TESTE", "diametro_externo_mm_por_secao_mm2": { "1.5": 3, "2.5": 4, "4": 5, "6": 6, "10": 7, "16": 8, "25": 10 } }
          ]
        }
        """;

    /// <summary>Catálogos sem dados (TODO_CATALOGO), como os oficiais antes da escolha do fabricante.</summary>
    public static readonly Ampere.Core.Catalogos.CatalogosDeProduto Vazios = new(
        Ampere.Core.Catalogos.CatalogoDeCondutores.Carregar(Vazio("condutores")),
        Ampere.Core.Catalogos.CatalogoDeEletrodutos.Carregar(Vazio("eletrodutos")));

    private static string Vazio(string catalogo) =>
        $$"""{ "$meta": { "fonte": "sem dados", "versao": "0", "data": "2026-09-29", "ficticio": false }, "catalogo": "{{catalogo}}", "ref": "TODO_CATALOGO", "tipos": [] }""";

    public const string Eletrodutos = """
        {
          "$meta": { "fonte": "fictício, só para testes", "versao": "0", "data": "2026-09-29", "ficticio": true },
          "catalogo": "eletrodutos",
          "ref": "FICTÍCIO: catálogo de eletrodutos",
          "tipos": [
            { "tipo": "ELETRODUTO-TESTE", "tamanhos": [
              { "nominal": "C", "diametro_interno_mm": 20 },
              { "nominal": "A", "diametro_interno_mm": 10 },
              { "nominal": "B", "diametro_interno_mm": 15 },
              { "nominal": "D", "diametro_interno_mm": 30 }
            ] }
          ]
        }
        """;
}
