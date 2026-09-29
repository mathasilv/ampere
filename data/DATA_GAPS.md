# Pendências de dados normativos (DATA_GAPS)

Todo valor ou referência de norma usado pelo Ampere precisa de fonte oficial citada no perfil
(`data/normas/<norma>/<ano>/perfil.json`). Enquanto o texto oficial não estiver disponível, o código
marca `TODO_NORMA` e a pendência é registrada aqui. Um cálculo errado silencioso é o pior defeito
possível deste produto.

Testes que dependem de fonte pendente levam `[Property("Fonte", "TODO_NORMA")]` e são listados com:

```bash
dotnet test tests/Ampere.Tests.Core --list-tests --treenode-filter "/*/*/*/*[Fonte=TODO_NORMA]"
```

## Abertas (em uso no código)

| ID | Pendência | Onde | Para fechar |
|---|---|---|---|
| GAP-001 | Item da NBR 5410:2004 que define a corrente de projeto I<sub>B</sub> e a tensão a adotar por esquema (F+N, 2F, 3F) | `CalculoDeCorrente.CorrenteDeProjetoMonofasica`; `CalculoDeCorrente_Teste` (valores de brinquedo) | Conferir no texto oficial, citar o item no perfil e trocar os valores de brinquedo por casos verificáveis |
| GAP-002 | Regras e limites da NBR 5410 para agrupar pontos em circuitos (quantidades e potências por local, circuitos independentes, separação entre tipos de carga) — especificação §3, F1.2 | `RegraDeAgrupamento` e `PlanejadorDeCircuitos`: hoje só limites informados pelo projetista, sem nenhum padrão | Citar itens e valores no perfil `NBR5410/2004`; o diálogo passa a sugeri-los, sempre editáveis |
| GAP-003 | Todas as tabelas do perfil `data/normas/NBR5410/2004/perfil.json` (hoje esqueleto, `ref: TODO_NORMA` e sem valores) — ver lista abaixo | `PerfilNormativo.NBR5410_2004`; o motor de dimensionamento para e diz qual tabela falta | Preencher tabela por tabela com o item da norma em `ref`; o carregador recusa valor sem fonte |

### GAP-003 — tabelas do perfil NBR5410:2004 a preencher

| Tabela no perfil | Conteúdo | Observação |
|---|---|---|
| `secoes_nominais_mm2` | Seções nominais de condutores | Série de seções da norma de condutores (não é tabela da 5410) |
| `correntes_nominais_disjuntor_a` | Correntes nominais de disjuntores | Valores preferenciais da norma de disjuntores / fabricante |
| `condutores_carregados` | Nº de condutores carregados por configuração (F+N, 2F, 3F, 3F+N) | Regra da 5410 para neutro carregado |
| `secao_minima_mm2` | Seção mínima por tipo de circuito (iluminação, força) | |
| `capacidade_de_conducao_a` | Capacidade de condução por método, isolação, material, nº de condutores carregados e seção | Especificação cita tabelas 36/37 |
| `fator_de_temperatura` | FCT por isolação e temperatura ambiente | Especificação cita a Tabela 40; o motor não interpola |
| `fator_de_agrupamento` | FCA por número de circuitos agrupados | |
| `queda_de_tensao_maxima_pct` | Limites de queda de tensão | A especificação diz "4% instalação total, 7% circuito terminal". **Conferir se não está invertido:** há indício de que o limite menor se aplica aos circuitos terminais |
| `resistividade_ohm_mm2_por_m` | Resistividade do condutor na temperatura de serviço | Para a queda de tensão |
| `ocupacao_maxima_eletroduto_pct` | Taxa máxima de ocupação por nº de condutores | A especificação diz "40% ≥ 2 condutores; 31%/53% nos casos da norma". **Conferir a que número de condutores cada taxa se aplica** |

## Previstas (ainda sem código)

- Fatores de demanda por tipo de carga (quadro de cargas, F1.4).
- Referência do exemplo de memória da especificação §6.4 (`"ref": "6410.4.2.1.2"`): o formato não
  corresponde a um item conhecido da norma; não reutilizar sem conferir.
