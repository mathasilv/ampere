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
| GAP-003 | **Parcial (30/09/2026 — pacotes 1 e 2 do texto oficial, E:/NBR-5410.pdf)**: preenchidas queda (6.2.7.1/6.2.7.2), temperatura-ar (Tabela 40, PVC e EPR/XLPE), agrupamento (Tabela 42 linha 1, em feixe) e ocupação (6.2.11.1.6-a: 53/31/40%). Pacote 3: seção mínima (Tabela 47 + item 6.2.6.1.1: iluminação 1,5 / força 2,5 mm² Cu; motor dimensiona seção de condutor até a coordenação com disjuntor). IDR por local (item 5.1.3.2.2, alíneas a-e + notas) preenchido no pacote 4 — o motor completa o dimensionamento de circuito terminal com perfil oficial. Pacote 2: capacidade (Tabelas 36 e 37, A1-D, cobre/alumínio, PVC/EPR-XLPE, 48 linhas transcritas por coordenadas e validadas contra IEC 60364-5-52), condutores carregados (nota do neutro, 6.2.6.2) e seções nominais (série das tabelas). Pendentes: Tabela 47 (seção mínima — valores fora da faixa de texto extraída), Tabela 40-solo, Tabelas 38-39 (métodos E/F/G), disjuntores e IDR (normas de produto) | `PerfilNormativo.NBR5410_2004`; o motor de dimensionamento para e diz qual tabela falta | Preencher tabela por tabela com o item da norma em `ref`; o carregador recusa valor sem fonte |
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
- Comprimento L: AMP_ComprimentoRotaM do circuito (projetista) ou, vazio, o "Comprimento" que o Revit calcula para o
  circuito no modo de caminho dele (padrão: do quadro ao ponto mais distante; "todos os pontos" ou caminho editado
  também valem e a memória registra qual). Arredondado ao milímetro. Comprimento zero do Revit é problema de dados.
- Tipo de condutor ou de eletroduto não informado não bloqueia o circuito: o cálculo vai até o IDR e para no
  eletroduto, explicando o que falta (enquanto os catálogos estão vazios, para sempre ali — GAP-004).
- Ponto sem local ou local fora da tabela interrompe o cálculo, a menos que o projetista decida. A decisão do
  projetista prevalece; a memória registra o que a tabela daria e o resultado traz um aviso quando divergem.
- O IDR é calculado antes do eletroduto: um impedimento no IDR deixa o eletroduto sem cálculo.

## Comando "Dimensionar" — decisões e pendências (01/10/2026)

Resolvido (decisões do usuário):

- ✅ Local do ponto: parâmetro novo `AMP_Local` (catálogo 0.2, GUID novo registrado no `$meta`), preenchido no
  "Classificar cargas" com os locais da tabela de IDR do perfil.
- ✅ Comprimento: `AMP_ComprimentoRotaM` informado prevalece; vazio, vale o calculado pelo Revit. O Ampere nunca grava o
  comprimento de volta.
- ✅ `AMP_MemoriaCalculoId`: no circuito, a memória do dimensionamento; no quadro (categoria ampliada no catálogo 0.2), a
  memória do quadro de cargas.
- ✅ Adapter `DocumentoDeDimensionamentoRevit` e comando "Dimensionar circuitos".

Comportamento do modelo a validar pelo projetista:

- Resultado que deixou de ser calculado (IDR que deixou de ser exigido, circuito que perdeu um dado, cálculo que parou
  antes) apaga o valor anterior. O Revit não devolve parâmetro compartilhado numérico a "sem valor" (`ClearValue` exige
  HideWhenNoValue, que a injeção não usa): o valor anterior vira **0**; parâmetro que nunca teve valor continua vazio.
- Seção, I<sub>Z</sub>, disjuntor, queda, IDR e eletroduto só são gravados quando o IDR foi decidido por completo
  (exigência e, se exigido, a corrente nominal); se o cálculo parou antes ou dentro do IDR (ex.: ponto sem AMP_Local, ou
  disjuntor acima da maior corrente de IDR do perfil), ficam vazios/0 e só a corrente de projeto, os fatores e o hash da
  memória são gravados. Leitura das tabelas: **IDR vazio ou 0 ao lado de um disjuntor gravado = sem IDR**; sem disjuntor
  gravado, a proteção não foi decidida (ver a memória).
- AMP_ComprimentoRotaM = 0 conta como vazio (é o único jeito de "esvaziar" o parâmetro): vale o comprimento do Revit.
- Ponto com AMP_TipoCarga diferente do circuito (reclassificado depois, ou posto no circuito pelo Revit) é problema de
  dados: o tipo decide a seção mínima e o IDR, e o motor não escolhe um deles.
- Quadro de cargas: circuito que fica fora do quadro (sem tipo ou sem potência) perde o fator da montagem anterior;
  circuito desconectado do quadro perde também o AMP_Quadro (sai da tabela do quadro); o AMP_Quadro do circuito passa
  a acompanhar o quadro em que ele está. Quadro que ficou sem circuitos perde o hash (mesmo sem nenhum quadro montado);
  quadro em grupo ou vínculo fica com o hash que tinha e o resumo avisa quando ele não é o atual.

Pendente:

- **Decisão do projetista sobre o IDR** (`DecisaoDeIdr`, já no Core) não tem fonte no modelo: o comando passa sempre
  "sem decisão" e a tabela por local decide. Precisa de um campo que o motor nunca escreve (AMP_IDR_SensibilidadeMa é
  resultado — ler de volta transformaria o cálculo em "decisão"): parâmetro novo (GUID novo, decisão do usuário) ou
  campo no diálogo por circuito.
- Seleção: o comando dimensiona todos os circuitos do Ampere no projeto; dimensionar só os selecionados fica para depois.
- **Método D (enterrado) bloqueado**: as linhas da Tabela 40 do perfil são do ar e declaram `"metodos": [A1…C]`; para D
  o motor para no fator de temperatura até a Tabela 40-solo (e as Tabelas 44/45 de agrupamento enterrado) serem
  transcritas. D também sai da lista do diálogo.
- **Alumínio fora do diálogo**: o perfil só tem resistividade do cobre, e a seção mínima (Tabela 47) está com os valores
  do cobre (a ref já cita 16 mm² para alumínio). Para liberar: resistividade do alumínio com fonte e seção mínima por
  material no perfil.

## Previstas (ainda sem código)

- Referência do exemplo de memória da especificação §6.4 (`"ref": "6410.4.2.1.2"`): o formato não
  corresponde a um item conhecido da norma; não reutilizar sem conferir.
