namespace Ampere.Tests.Core.Normas;

/// <summary>
///     Perfil normativo FICTÍCIO, só para testar o motor. Os números são propositalmente redondos e NÃO são da
///     NBR 5410 — nenhum deles pode ir para o produto.
/// </summary>
internal static class PerfilFicticio
{
    public const string Json = """
        {
          "$meta": { "fonte": "fictício, só para testes", "versao": "0", "data": "2026-09-29", "ficticio": true },
          "perfil": "FICTICIO-TESTE",
          "regras": {
            "corrente_de_projeto": "FICTÍCIO: regra da corrente de projeto",
            "coordenacao_condutor_protecao": "FICTÍCIO: regra de coordenação",
            "queda_de_tensao": "FICTÍCIO: regra de queda",
            "condutores_no_eletroduto": "FICTÍCIO: regra dos condutores no eletroduto",
            "coordenacao_idr_disjuntor": "FICTÍCIO: regra IDR x disjuntor"
          },
          "tabelas": {
            "secoes_nominais_mm2": { "ref": "FICTÍCIO: seções", "valores": [1.5, 2.5, 4, 6, 10, 16, 25] },
            "correntes_nominais_disjuntor_a": { "ref": "FICTÍCIO: disjuntores", "valores": [10, 16, 20, 25, 32, 40, 50, 63] },
            "condutores_carregados": { "ref": "FICTÍCIO: condutores carregados", "valores": { "F+N": 2, "2F": 2, "3F": 3, "3F+N": 3 } },
            "secao_minima_mm2": { "ref": "FICTÍCIO: seção mínima", "valores": { "Iluminacao": 1.5, "Forca": 2.5 } },
            "capacidade_de_conducao_a": {
              "ref": "FICTÍCIO: capacidade de condução",
              "valores": [
                { "metodo": "B1", "isolacao": "PVC", "material": "Cobre", "condutores_carregados": 2,
                  "por_secao_mm2": { "1.5": 10, "2.5": 20, "4": 30, "6": 40, "10": 60, "16": 80, "25": 100 } },
                { "metodo": "B1", "isolacao": "PVC", "material": "Cobre", "condutores_carregados": 3,
                  "por_secao_mm2": { "1.5": 9, "2.5": 18, "4": 27, "6": 36, "10": 54, "16": 72, "25": 90 } }
              ]
            },
            "fator_de_temperatura": {
              "ref": "FICTÍCIO: fator de temperatura",
              "valores": [ { "isolacao": "PVC", "por_temperatura_c": { "30": 1, "40": 0.8 } } ]
            },
            "fator_de_agrupamento": { "ref": "FICTÍCIO: fator de agrupamento", "valores": { "1": 1, "2": 0.8, "3": 0.7 } },
            "queda_de_tensao_maxima_pct": { "ref": "FICTÍCIO: queda máxima", "valores": { "circuito_terminal": 5 } },
            "resistividade_ohm_mm2_por_m": { "ref": "FICTÍCIO: resistividade", "valores": { "Cobre": 0.02 } },
            "ocupacao_maxima_eletroduto_pct": { "ref": "FICTÍCIO: ocupação", "valores": { "1": 50, "2": 30, "3": 40 } },
            "correntes_nominais_idr_a": { "ref": "FICTÍCIO: correntes de IDR", "valores": [25, 40, 63] },
            "protecao_diferencial_por_local": {
              "ref": "FICTÍCIO: IDR por local",
              "valores": [
                { "local": "LOCAL-SECO", "tipos_de_carga": [] },
                { "local": "LOCAL-MOLHADO", "tipos_de_carga": ["Iluminacao", "TUG", "TUE"], "sensibilidade_maxima_ma": 30 },
                { "local": "LOCAL-EXTERNO", "tipos_de_carga": ["TUG"], "sensibilidade_maxima_ma": 30 },
                { "local": "LOCAL-ESPECIAL", "tipos_de_carga": ["TUG", "TUE"], "sensibilidade_maxima_ma": 10 }
              ]
            }
          }
        }
        """;
}
