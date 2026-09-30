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
| GAP-001 | Item da NBR 5410:2004 que define a corrente de projeto I<sub>B</sub> e a tensão a adotar por esquema (F+N, 2F, 3F) | `CalculoDeCorrente.CorrenteDeProjetoMonofasica`; `CalculoDeCorrente_Teste` (valores de brinquedo); regra `corrente_de_projeto` do perfil, usada pelo `DimensionamentoDeCircuito` | Conferir no texto oficial, citar o item no perfil e trocar os valores de brinquedo por casos verificáveis |
| GAP-002 | Regras e limites da NBR 5410 para agrupar pontos em circuitos (quantidades e potências por local, circuitos independentes, separação entre tipos de carga) — especificação §3, F1.2 | `RegraDeAgrupamento` e `PlanejadorDeCircuitos`: hoje só limites informados pelo projetista, sem nenhum padrão | Citar itens e valores no perfil `NBR5410/2004`; o diálogo passa a sugeri-los, sempre editáveis |
| GAP-003 | Todas as tabelas do perfil `data/normas/NBR5410/2004/perfil.json` (hoje esqueleto, `ref: TODO_NORMA` e sem valores) — ver lista abaixo | `PerfilNormativo.NBR5410_2004`; o motor de dimensionamento para e diz qual tabela falta | Preencher tabela por tabela com o item da norma em `ref`; o carregador recusa valor sem fonte |
| GAP-004 | Catálogos de fabricante: diâmetro externo dos condutores por tipo e seção; tamanhos nominais e diâmetros internos dos eletrodutos | `data/catalogos/condutores.json` e `eletrodutos.json` (hoje `ref: TODO_CATALOGO` e sem tipos); o dimensionamento para no diâmetro do condutor | Usuário escolhe os fabricantes; preencher com a `ref` (fabricante, linha e edição do catálogo); o carregador recusa valor sem fonte |
| GAP-005 | Fatores de demanda por tipo de carga (NBR 5410, quadro de cargas — F1.4) | Tabela `fator_de_demanda_por_tipo` e regra `demanda_do_quadro` do perfil; o motor já aceita fatores informados pelo projetista (que vencem o perfil) enquanto a tabela está vazia | Preencher a tabela com os itens/values oficiais; a memória passa a citar a norma em vez de "valor informado pelo projetista" |

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
| `correntes_nominais_idr_a` | Correntes nominais de IDR | Valores preferenciais da norma do dispositivo / fabricante |
| `protecao_diferencial_por_local` | Locais em que o IDR é exigido: para cada local, os tipos de carga atingidos e a I<sub>Δn</sub> máxima | O vocabulário de locais é do perfil (quem classifica os ambientes do projeto usa esses nomes); local que não exige entra com `tipos_de_carga: []` |
| `regras` (`coordenacao_condutor_protecao`, `queda_de_tensao`, `condutores_no_eletroduto`, `coordenacao_idr_disjuntor`) | Itens que definem a coordenação I<sub>B</sub> ≤ I<sub>n</sub> ≤ I<sub>Z</sub>, o critério de queda de tensão, os condutores que contam na ocupação do eletroduto e a corrente nominal do IDR frente ao disjuntor | Não são tabelas, mas a memória cita a `ref` de cada uma; `corrente_de_projeto` está no GAP-001 |

## Simplificações do motor de dimensionamento (a validar pelo projetista)

Não são valores de norma, mas decisões de engenharia do `DimensionamentoDeCircuito` que precisam de aval:

- I<sub>B</sub> pela potência aparente instalada: `S / V` (F+N e 2F) e `S / (√3 · V)` (3F e 3F+N); fator de
  demanda não aplicado no circuito terminal.
- 2F+N não modelado (a divisão das cargas entre as fases é desconhecida): o cálculo para e explica.
- Queda de tensão pela fórmula resistiva `k · ρ · L · IB / (S · V)`, k = 2 (F+N, 2F) ou √3 (3F), sem reatância,
  só do circuito terminal (o trecho do alimentador ainda não entra).
- Fator de temperatura sem interpolação: temperatura não tabelada interrompe o cálculo.
- Coordenação: menor I<sub>n</sub> nominal com I<sub>B</sub> ≤ I<sub>n</sub> ≤ I<sub>Z</sub>; sem ela, a seção sobe.
- Eletroduto: menor tamanho do catálogo com ocupação `n · d² / Di² · 100` ≤ taxa máxima (área dos condutores sobre a
  área interna, com todos os condutores iguais). Contam só os condutores do próprio circuito: F+N e 2F = 3,
  3F = 4, 3F+N = 5 (fases, neutro e um condutor de proteção por circuito). Outros circuitos na mesma tubulação ainda
  não entram.
- Neutro e proteção com o diâmetro da fase: conservador, porque seção menor só reduziria a ocupação.
- Um tipo de eletroduto por projeto (condição do projeto). O tipo de condutor vem de `AMP_TipoCondutor` do circuito
  ou do padrão do projeto.
- IDR por circuito: cada circuito que exige recebe o seu (proteção de um grupo de circuitos por um só IDR fica para o
  quadro, F1.4). I<sub>Δn</sub> = a menor máxima entre os locais dos pontos que exigem; corrente nominal = a menor da
  série com In(IDR) ≥ In(disjuntor).
- Ponto sem local ou local fora da tabela interrompe o cálculo, a menos que o projetista decida. A decisão do
  projetista prevalece; a memória registra o que a tabela daria e o resultado traz um aviso quando divergem.
- O IDR é calculado antes do eletroduto: um impedimento no IDR deixa o eletroduto sem cálculo.

## Previstas (ainda sem código)

- Referência do exemplo de memória da especificação §6.4 (`"ref": "6410.4.2.1.2"`): o formato não
  corresponde a um item conhecido da norma; não reutilizar sem conferir.
