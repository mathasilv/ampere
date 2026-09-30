# Pendências de dados normativos (DATA_GAPS)

Todo valor ou referência de norma usado pelo Ampere precisa de fonte oficial citada no perfil
(`data/normas/<norma>/<ano>/perfil.json`). Enquanto o texto oficial não estiver disponível, o código
marca `TODO_NORMA` e a pendência é registrada aqui. Um cálculo errado silencioso é o pior defeito
possível deste produto.

Testes que dependem de fonte pendente levam `[Property("Fonte", "TODO_NORMA")]` e são listados com:

```bash
dotnet test tests/Ampere.Tests.Core --list-tests --treenode-filter "/*/*/*/*[Fonte=TODO_NORMA]"
```

## Fechados

- GAP-001 — corrente de projeto IB: item 5.3.4.1 da NBR 5410:2004 (30/09/2026).

## Abertas (em uso no código)

| ID | Pendência | Onde | Para fechar |
|---|---|---|---|
| GAP-001 | ✅ **FECHADO (30/09/2026)** — IB é a corrente de projeto do circuito, definida no item 5.3.4.1 (coordenação IB ≤ In ≤ Iz, I2 ≤ 1,45 Iz; Iz ver 6.2.5). Ref real no perfil. | | |
| GAP-002 | Regras e limites da NBR 5410 para agrupar pontos em circuitos (quantidades e potências por local, circuitos independentes, separação entre tipos de carga) — especificação §3, F1.2 | `RegraDeAgrupamento` e `PlanejadorDeCircuitos`: hoje só limites informados pelo projetista, sem nenhum padrão | Citar itens e valores no perfil `NBR5410/2004`; o diálogo passa a sugeri-los, sempre editáveis |
| GAP-003 | **Parcial (30/09/2026 — pacotes 1 e 2 do texto oficial, E:/NBR-5410.pdf)**: preenchidas queda (6.2.7.1/6.2.7.2), temperatura-ar (Tabela 40, PVC e EPR/XLPE), agrupamento (Tabela 42 linha 1, em feixe) e ocupação (6.2.11.1.6-a: 53/31/40%). Pacote 3: seção mínima (Tabela 47 + item 6.2.6.1.1: iluminação 1,5 / força 2,5 mm² Cu; motor dimensiona seção de condutor até a coordenação com disjuntor). Pacote 2: capacidade (Tabelas 36 e 37, A1-D, cobre/alumínio, PVC/EPR-XLPE, 48 linhas transcritas por coordenadas e validadas contra IEC 60364-5-52), condutores carregados (nota do neutro, 6.2.6.2) e seções nominais (série das tabelas). Pendentes: Tabela 47 (seção mínima — valores fora da faixa de texto extraída), Tabela 40-solo, Tabelas 38-39 (métodos E/F/G), disjuntores e IDR (normas de produto) | `PerfilNormativo.NBR5410_2004`; o motor de dimensionamento para e diz qual tabela falta | Preencher tabela por tabela com o item da norma em `ref`; o carregador recusa valor sem fonte |
| GAP-004 | **Decisão registrada (30/09/2026, delegada ao desenvolvedor):** disjuntores e IDR pelas séries IEC 60898-1/60947-2 e catálogos WEG/Schneider (25–100 A) — preenchidas no perfil; condutores/eletrodutos: fabricante escolhido **Prysmian**, diâmetros externos pendentes de transcrição do catálogo | `data/catalogos/condutores.json` e `eletrodutos.json` (o dimensionamento para no diâmetro do condutor) | Transcrever catálogo Prysmian (Superastic, Sintenax) com a `ref` (linha e edição) |
| GAP-005 | Fatores de demanda por tipo de carga (NBR 5410, quadro de cargas — F1.4) | Tabela `fator_de_demanda_por_tipo` e regra `demanda_do_quadro` do perfil; o motor já aceita fatores informados pelo projetista (que vencem o perfil) enquanto a tabela está vazia | Preencher a tabela com os itens/values oficiais; a memória passa a citar a norma em vez de "valor informado pelo projetista" |

### GAP-003 — tabelas do perfil NBR5410:2004 a preencher

| Tabela no perfil | Conteúdo | Observação |
|---|---|---|
| `secoes_nominais_mm2` | ✅ PREENCHIDA (série das Tabelas 36-39) | Série de seções da norma de condutores (não é tabela da 5410) |
| `correntes_nominais_disjuntor_a` | Correntes nominais de disjuntores | Valores preferenciais da norma de disjuntores / fabricante |
| `condutores_carregados` | ✅ PREENCHIDA (Tabelas 36-41 + 6.2.6.2, sem harmônicas o neutro não conta) | Regra da 5410 para neutro carregado |
| `secao_minima_mm2` | ✅ PREENCHIDA (item 6.2.6.1.1 e Tabela 47) | |
| `capacidade_de_conducao_a` | ✅ PREENCHIDA (Tabelas 36 e 37, A1-D, validada contra IEC) | Especificação cita tabelas 36/37 |
| `fator_de_temperatura` | ✅ PREENCHIDA (Tabela 40, ar; solo pendente) | Especificação cita a Tabela 40; o motor não interpola |
| `fator_de_agrupamento` | ✅ PREENCHIDA (Tabela 42 linha 1; motor agora aceita faixas 9+) | |
| `queda_de_tensao_maxima_pct` | ✅ PREENCHIDA (6.2.7.1/6.2.7.2): circuito_terminal 4, ponto_de_entrega 5, transformador/gerador 7 | A especificação diz "4% instalação total, 7% circuito terminal". **Conferir se não está invertido:** há indício de que o limite menor se aplica aos circuitos terminais |
| `resistividade_ohm_mm2_por_m` | Resistividade do condutor na temperatura de serviço | Para a queda de tensão |
| `ocupacao_maxima_eletroduto_pct` | ✅ PREENCHIDA (6.2.11.1.6-a) | A especificação diz "40% ≥ 2 condutores; 31%/53% nos casos da norma". **Conferir a que número de condutores cada taxa se aplica** |
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
