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
| GAP-002 | Regras e limites da NBR 5410 para agrupar pontos em circuitos (quantidades e potências por local, circuitos independentes, separação entre tipos de carga) — especificação §3, F1.2. **01/10/2026:** a previsão mínima de cargas dos locais de habitação (9.5.2) está em `data/normas/NBR5410/2004/previsao_de_cargas.json` (comando 'Previsão de cargas'); a divisão em circuitos (9.5.3.1: equipamento acima de 10 A em circuito próprio; 9.5.3.2: tomadas de cozinhas e áreas de serviço em circuitos só delas) é conferida no mesmo comando, para os pontos nos cômodos de habitação. "Equipamento" = TUE, ar condicionado e motor (critério do Ampere). O 'Criar circuitos' aplica essa divisão quando as categorias dos cômodos já foram escolhidas: tomadas de cozinha e áreas de serviço vão para circuitos só delas e o equipamento acima de 10 A fica sozinho no circuito, com aviso citando o item. Pendente: limites de pontos e de potência por circuito (não são da NBR 5410; ficam com o projetista) | `RegraDeAgrupamento` e `PlanejadorDeCircuitos`: hoje só limites informados pelo projetista, sem nenhum padrão | Citar itens e valores no perfil `NBR5410/2004`; o diálogo passa a sugeri-los, sempre editáveis |
| GAP-003 | **Parcial (30/09/2026 — pacotes 1 e 2 do texto oficial, E:/NBR-5410.pdf)**: preenchidas queda (6.2.7.1/6.2.7.2), temperatura-ar (Tabela 40, PVC e EPR/XLPE), agrupamento (Tabela 42 linha 1, em feixe) e ocupação (6.2.11.1.6-a: 53/31/40%). Pacote 3: seção mínima (Tabela 47 + item 6.2.6.1.1: iluminação 1,5 / força 2,5 mm² Cu; motor dimensiona seção de condutor até a coordenação com disjuntor). IDR por local (item 5.1.3.2.2, alíneas a-e + notas) preenchido no pacote 4 — o motor completa o dimensionamento de circuito terminal com perfil oficial. Pacote 2: capacidade (Tabelas 36 e 37, A1-D, cobre/alumínio, PVC/EPR-XLPE, 48 linhas transcritas por coordenadas e validadas contra IEC 60364-5-52), condutores carregados (nota do neutro, 6.2.6.2) e seções nominais (série das tabelas). Pendentes: Tabela 47 (seção mínima — valores fora da faixa de texto extraída), Tabela 40-solo, Tabelas 38-39 (métodos E/F/G), disjuntores e IDR (normas de produto) | `PerfilNormativo.NBR5410_2004`; o motor de dimensionamento para e diz qual tabela falta | Preencher tabela por tabela com o item da norma em `ref`; o carregador recusa valor sem fonte |
| GAP-004 | **Decisão registrada (30/09/2026, delegada ao desenvolvedor):** disjuntores e IDR pelas séries IEC 60898-1/60947-2 e catálogos WEG/Schneider (25–100 A) — preenchidas no perfil; condutores: **Prysmian** — ✅ 01/10/2026: Superastic Flex 450/750 V (BW_005_02_PT, 1 a 300 mm²) e Sintenax Flex 0,6/1 kV unipolar (LV_006_01_PT, 1,5 a 240 mm²), diâmetro nominal externo, transcritos pelo usuário e conferidos contra os PDFs (pesos da transcrição errados de 16 mm² em diante — não usados); isolação PVC declarada no catálogo (o motor recusa circuito com outra isolação). Multipolares do Sintenax fora (o motor conta condutores isolados). Eletrodutos: **Tigre** — ✅ 01/10/2026: Tigreflex amarelo (FT 09/2025), Tigreflex Reforçado (FT 10/2025) e PVC rígido roscável (FT 10/2013 = Catálogo Eletricidade TG-059-16, p. 62), diâmetro interno impresso. Na transcrição do roscável as fontes estavam trocadas: a série Di 16,4…103,1 é a da ficha oficial; a outra (Di 17,4…) não está em nenhum PDF anexado e ficou de fora | `data/catalogos/condutores.json` e `eletrodutos.json` | Soldável: a Tigre não publica as dimensões (NBR 15465, licenciada: anexar a tabela). Aço: escolher o fabricante. IΔn do IDR (01/10/2026, mesma delegação): série preferencial da NBR NM 61008-1 / IEC 61008-1 (6, 10, 30, 100, 300, 500 mA) em `sensibilidades_nominais_idr_ma`; a IΔn do projetista precisa ser uma delas |
| GAP-005 | Fatores de demanda por tipo de carga (NBR 5410, quadro de cargas — F1.4). **Distribuidora (01/10/2026, decisão do usuário):** CELG CT 04/18 (jan/2018, Anexo A, Tabelas 2 a 5, e item 10) em `data/distribuidoras/CELG/CT-04-2018/demanda.json`, transcrita pelo usuário e conferida contra o PDF; P (kW) da Tabela 2 = potência aparente instalada (kVA) dos pontos; aparelhos pelo AMP_Aparelho. **Vigência a conferir.** **01/10/2026, opção (c) do usuário:** a demanda continua pela CT 04/18 (a NT.00001.EQTL rev. 09 anexada não traz o corpo da seção 6.4, de demanda); o padrão de entrada sai da NT.00001.EQTL rev. 09 pela carga instalada (Tabelas 1 e 2, 220/380 V e 127/220 V) em `data/distribuidoras/EQTL/NT.00001-09/padrao_de_entrada.json`, transcrito e conferido visualmente no PDF. Carga em kW = potência instalada em kVA (a mesma decisão da demanda). Errata da NT: as tabelas citam as notas 23 a 26, impressas como 19 a 22 | Tabela `fator_de_demanda_por_tipo` e regra `demanda_do_quadro` do perfil (quadro de cargas, com fatores informados enquanto vazia); demanda da entrada pela distribuidora | Quadro de cargas: preencher a tabela do perfil com valores oficiais. Demanda: trocar a CT 04/18 quando a Equatorial publicar a seção de demanda |

### GAP-003 — tabelas do perfil NBR5410:2004 a preencher

| Tabela no perfil | Conteúdo | Observação |
|---|---|---|
| `secoes_nominais_mm2` | ✅ PREENCHIDA (série das Tabelas 36-39) | Série de seções da norma de condutores (não é tabela da 5410) |
| `correntes_nominais_disjuntor_a` | Correntes nominais de disjuntores | Valores preferenciais da norma de disjuntores / fabricante |
| `condutores_carregados` | ✅ PREENCHIDA (Tabelas 36-41 + 6.2.6.2, sem harmônicas o neutro não conta) | Regra da 5410 para neutro carregado |
| `secao_minima_mm2` | ✅ PREENCHIDA (item 6.2.6.1.1 e Tabela 47) | |
| `capacidade_de_conducao_a` | ✅ PREENCHIDA (Tabelas 36 e 37, A1-D, validada contra IEC; **01/10/2026:** Tabelas 38 e 39, E-F-G, com a coluna citada na `ref` de cada linha) | Especificação cita tabelas 36/37. E/F/G: E = colunas (2)/(3); F = (4) com dois condutores e (5), trifólio, com três (a menor das colunas de F; justapostos no plano, coluna (6), dariam mais); G = (8), espaçados na vertical (a menor; horizontal, coluna (7), daria mais). Conferidas contra as fichas Prysmian (Superastic/Sintenax). **Errata impressa:** na Tabela 38, alumínio 630 mm², as colunas (6) a (8) repetem os valores de 500 mm² (640/775/730); o perfil guarda o impresso (menor que o real, a favor da segurança) e a ref da linha G avisa. A ref da tabela (A1-D) não mudou, para não mudar o texto das memórias de A1 a D por causa de E, F e G. As memórias mudaram de hash mesmo assim, com o neutro e o PE (passos novos, 01/10/2026): depois de atualizar o add-in, o "Verificar projeto" aponta os circuitos para dimensionar de novo |
| `fator_de_temperatura` | ✅ PREENCHIDA (Tabela 40, ar — A1 a C e E, F, G; **01/10/2026:** solo, para D, com `ref` própria: 20 °C = 1,0) | Especificação cita a Tabela 40; o motor não interpola. Não aplicada: a Tabela 41 (solo com resistividade térmica diferente de 2,5 K·m/W) — as capacidades do método D valem para 2,5 K·m/W e 0,7 m de profundidade (6.2.5.1.2, nota 4) |
| `fator_de_agrupamento` | ✅ PREENCHIDA (Tabela 42 linha 1). **Corrigida em 01/10/2026:** as chaves eram 12/16/19/20 ("limite superior"), e a busca exata parava em 9–11, 13–15 e 17–18 circuitos e dava 0,50 para 12 e 0,45 para 16 — acima dos 0,45 e 0,41 das faixas da norma (contra a segurança). Agora a chave é o início da faixa (9, 12, 16, 20) e vale a maior faixa ≤ n; os valores são os mesmos. Em qualquer leitura das faixas, o fator novo nunca é maior que o anterior | ✅ Conferido em 01/10/2026 no texto oficial (NBR anexada pelo usuário): faixas 9 a 11, 12 a 15, 16 a 19 e ≥ 20 (0,50, 0,45, 0,41 e 0,38) |
| `fator_de_agrupamento_enterrado` | ✅ PREENCHIDA (01/10/2026, Tabela 44, distância nula; métodos D) | **Critério do Ampere (a favor da segurança):** a coluna de distância nula da Tabela 44 (cabos diretamente enterrados) também para eletrodutos enterrados — os fatores da Tabela 45 e das distâncias maiores são todos maiores ou iguais. Chave = número exato de circuitos; de 7 em diante a tabela não tem valor e o cálculo para. 1 circuito = sem agrupamento (fator 1) |
| `metodos_com_eletroduto` | ✅ PREENCHIDA (01/10/2026, item 6.2.5.1.2): A1, A2, B1, B2 e D | C, E, F e G são cabos sobre parede ou ao ar livre: o motor não dimensiona eletroduto (a memória termina no IDR). Antes, o eletroduto saía para todo método, inclusive o C |
| `secao_do_condutor_de_protecao_mm2` | ✅ PREENCHIDA (01/10/2026, item 6.4.3.1.3 e Tabela 58) | Por seção nominal de fase: até 16, a da fase; até 35, 16; acima, S/2 na seção padronizada mais próxima, como manda 6.4.3.1.3 (95 → 50, 150 → 70, 400 → 185, 630 → 300); o único empate, 120 → 60 (50 ou 70), fica com a maior. O cálculo pela integral de Joule (6.4.3.1.2), alternativa da norma, não é feito. Na memória, na planilha, na lista de materiais e no unifilar dos circuitos terminais (refeito o cálculo, só quando a memória confere com a gravada); os parâmetros do circuito e o alimentador no unifilar mostram só a seção da fase. O eletroduto continua conservador (neutro e PE com o diâmetro da fase) |
| `construcoes_por_metodo` | ✅ PREENCHIDA (01/10/2026, Tabela 33 e 6.2.5.1.2) | Construção do condutor admitida em cada método: E só cabo multipolar, F só cabo unipolar, A2 só multipolar; condutor isolado no C (perfilado, nota 3) e no D (nota 8) só sob condição — vira aviso. O catálogo diz a construção de cada tipo (Superastic = condutor isolado, Sintenax unipolar = cabo unipolar); construção recusada para o cálculo. Lista de materiais: cabo multipolar ainda sai como condutores separados (não há multipolar no catálogo) |
| `secao_minima_do_pe_fora_do_cabo_mm2` | ✅ PREENCHIDA (01/10/2026, 6.4.3.1.4) | Nos métodos sem eletroduto e fora de cabo multipolar, o PE não está no mesmo cabo nem no mesmo conduto das fases: mínimo de 4 mm² cobre / 16 mm² alumínio. **Critério do Ampere:** a alínea b (sem proteção mecânica), porque a proteção mecânica não é conferida |
| `queda_de_tensao_maxima_pct` | ✅ PREENCHIDA (6.2.7.1/6.2.7.2): circuito_terminal 4, ponto_de_entrega 5, transformador próprio 7, transformador da distribuidora 7 (alínea b, 01/10/2026), gerador 7 | ✅ Conferido em 01/10/2026 no texto oficial: 4% é o máximo dos circuitos terminais (6.2.7.2) e 5%/7% o total da instalação (6.2.7.1) — a especificação estava invertida. Não aplicado (a favor da segurança): a nota 3, que permite +0,005%/m nas linhas principais acima de 100 m (até +0,5%) nas alíneas a, b e d |
| `resistividade_ohm_mm2_por_m` | Resistividade do condutor na temperatura de serviço | Para a queda de tensão |
| `ocupacao_maxima_eletroduto_pct` | ✅ PREENCHIDA (6.2.11.1.6-a) | A especificação diz "40% ≥ 2 condutores; 31%/53% nos casos da norma". **Conferir a que número de condutores cada taxa se aplica** |
| `correntes_nominais_idr_a` | Correntes nominais de IDR | Valores preferenciais da norma do dispositivo / fabricante |
| `protecao_diferencial_por_local` | Locais em que o IDR é exigido: para cada local, os tipos de carga atingidos e a I<sub>Δn</sub> máxima | O vocabulário de locais é do perfil (quem classifica os ambientes do projeto usa esses nomes); local que não exige entra com `tipos_de_carga: []` |
| `regras` (`coordenacao_condutor_protecao`, `queda_de_tensao`, `condutores_no_eletroduto`, `coordenacao_idr_disjuntor`) | Itens que definem a coordenação I<sub>B</sub> ≤ I<sub>n</sub> ≤ I<sub>Z</sub>, o critério de queda de tensão, os condutores que contam na ocupação do eletroduto e a corrente nominal do IDR frente ao disjuntor | Não são tabelas, mas a memória cita a `ref` de cada uma; `corrente_de_projeto` está no GAP-001. ✅ `condutores_no_eletroduto` = 6.2.11.1.6-a (01/10/2026). `coordenacao_idr_disjuntor` continua TODO_NORMA: a NBR 5410 (6.3.3.2) não fixa a corrente nominal do DR frente ao disjuntor — vem da norma de produto (IEC 61008-1/NBR NM 61008), a anexar. `demanda_do_quadro`: a NBR 5410 não tem fatores de demanda por tipo de carga (GAP-005). ✅ `secao_do_neutro` (01/10/2026) = 6.2.6.2.2 a 6.2.6.2.4: neutro com a seção das fases; a seção reduzida da Tabela 48 (6.2.6.2.6, fase acima de 25 mm²) exige circuito equilibrado, harmônicas até 15% e neutro protegido, que o Ampere não verifica — não é aplicada |

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
- ✅ Local pelo ambiente (01/10/2026): comando "Locais pelos ambientes" — o projetista escolhe um local da tabela de IDR
  por nome de ambiente (Room/Space, inclusive de vínculo). Decisão do desenvolvedor: o Ampere **não** deduz o local pelo
  nome do ambiente ("Banho" → banheiro); a correspondência é sempre do projetista.
- ✅ Decisões do projetista no circuito (01/10/2026, catálogo 0.3, GUIDs novos por decisão delegada ao desenvolvedor —
  "carta branca"): `AMP_SecaoMinimaProjetistaMm2`, `AMP_DisjuntorProjetistaA`, `AMP_IDR_DecisaoProjetista`
  (Exigir/Dispensar), `AMP_IDR_SensibilidadeProjetistaMa`, `AMP_JustificativaProjetista`, `AMP_TemperaturaAmbienteC` e
  `AMP_CircuitosAgrupados`. Só de entrada: o dimensionamento nunca os escreve. Numérico 0 = sem decisão.
  O motor **verifica** cada decisão: a seção do projetista é piso (abaixo da mínima da norma, vale a da norma, com
  aviso; fora das seções nominais, para); o disjuntor do projetista é fixo, precisa estar na série e ser ≥ I<sub>B</sub>,
  e a seção sobe até I<sub>Z</sub> ≥ I<sub>n</sub>; o IDR dispensado contra a tabela gera aviso. A justificativa vai para
  cada passo decidido na memória. Decisão incoerente (ex.: "Exigir" sem sensibilidade, texto desconhecido, agrupamento
  fracionário) é problema de dados, nunca interpretada.
- ✅ Seleção (01/10/2026): com elementos selecionados, o comando dimensiona só os circuitos do Ampere da seleção (o
  circuito selecionado, os circuitos que um quadro selecionado alimenta e o circuito de um ponto selecionado). A
  planilha dessa rodada vai para `circuitos-selecao.csv`, sem trocar a do projeto todo (`circuitos.csv`).

- ✅ Condições no modelo (01/10/2026, decisão do desenvolvedor): temperatura, agrupamento, material e padrões usados
  na rodada ficam em **cada circuito** dela (Extensible Storage, JSON versionado), na mesma transação dos resultados —
  quem abrir o projeto depois reproduz a memória de cada um, e o "Verificar projeto" confere cada circuito com as
  condições da rodada que o dimensionou. A rodada **completa** também as guarda como as do projeto (abrem o diálogo);
  a rodada só da seleção não as troca. Modelo compartilhado: o armazenamento do projeto emprestado a outro usuário, ou
  alterado no central desde a última sincronização, não é gravado (o Revit desfaria os resultados junto) e o resumo
  avisa; texto igual ao guardado não é regravado.

- ✅ Lista de materiais (01/10/2026): sai de cada rodada do "Dimensionar" (`materiais.csv`; da seleção,
  `materiais-selecao.csv`), com os circuitos de proteção decidida — os demais vão para o fim da planilha com o motivo.
  Simplificações a validar: condutores = comprimento do circuito × condutores, **sem sobras nem emendas**; neutro e PE
  com a **seção da fase** (a redução permitida pela norma não é aplicada — a Tabela 58 não está no perfil); disjuntor
  com um polo por fase e IDR com um polo por condutor vivo; curva, capacidade de interrupção e tipo do IDR não são
  decididos; **eletrodutos fora da lista** (circuitos dividem trechos; o quantitativo é o dos eletrodutos modelados).

- ✅ Verificar projeto (01/10/2026): comando só de leitura que lista pontos sem classificação, fora de circuito ou sem
  local (quando o circuito não tem decisão do projetista sobre o IDR), circuitos criados fora do Ampere, com dados
  faltando, não dimensionados ou com a memória gravada diferente da que o modelo dá hoje (o cálculo é refeito com as
  condições guardadas pela última rodada), avisos e onde o cálculo para. Salva `verificacao.md` e seleciona no modelo
  os elementos de uma pendência. Cada memória é refeita com as condições da rodada que a gerou, pelo mesmo cálculo do
  "Dimensionar". Ponto com Reserva conta como sem classificação. Quadros: sem quadro de cargas montado, ou com o hash
  gravado diferente do quadro refeito com os fatores guardados na montagem. Resultado editado à mão (memória em dia,
  mas um AMP_* de resultado diferente do que ela justifica — ex.: disjuntor trocado no parâmetro) é aviso.

- ✅ Alimentação do quadro (01/10/2026): esquema e tensão do quadro de cargas vêm do sistema de distribuição atribuído
  ao painel no Revit (3 fases e 4 fios = 3F+N; 3 fios = 3F; monofásico 2 fios = F+N; monofásico 3 fios = 2F+N, que o
  quadro de cargas ainda não calcula). Antes, vinham dos circuitos e precisavam ser uniformes: o quadro residencial
  típico (TUG e iluminação F+N 127 V com chuveiro 2F 220 V) ficava sem corrente de demanda. Circuito cuja tensão ou
  esquema a alimentação não fornece (ex.: F+N 220 V num quadro 220/127 V; folga de 2% para 380/220 V) é problema
  apontado. Sem sistema de distribuição, vale a regra antiga (esquema comum dos circuitos). A corrente de demanda da
  memória segue equilibrada (D / √3·V).
- ✅ Cargas por fase (01/10/2026): cada circuito ocupa as fases que o Revit lhe dá no quadro (`PhaseLabel`, com os
  rótulos das configurações elétricas, conferido com o número de polos — rótulo que não bate fica "sem fase
  identificada"); a carga dele se divide igualmente entre elas. Por fase: instalada, demanda e corrente (fase-neutro);
  desequilíbrio = (maior − menor) / maior. É **indicador**, fora da memória de cálculo: a NBR 5410 não fixa limite.
  Aparece no resumo e numa seção do relatório do quadro. A validar no Revit: o formato do `PhaseLabel` (teste de
  integração do quadro). Corrente por fase = soma das correntes de linha dos circuitos (S / V em F+N e 2F, S / √3·V em
  3F, S / 2·V fase-neutro em 2F+N); tensão fase-neutro = a fase-terra do sistema de distribuição do Revit.

- ✅ Alimentadores dos quadros (01/10/2026, decisões do desenvolvedor): comando "Dimensionar alimentadores", com o mesmo
  motor dos terminais. IB pela demanda do quadro de cargas — refeito com os fatores guardados no quadro na montagem e
  conferido pelo hash gravado (quadro desatualizado = não dimensiona) —; em 3F/3F+N com a corrente de todas as fases
  conhecida, pela **fase de maior corrente** (soma aritmética das correntes de linha, a favor da segurança). Queda: o
  alimentador fica com o que sobra do limite total do perfil para a origem escolhida (ponto de entrega 5%,
  transformador ou gerador próprio 7%) depois da **maior queda dos circuitos do quadro**. IDR: a tabela por local é dos
  terminais; no alimentador, só por decisão do projetista. **Fora desta versão** (o comando diz o motivo): alimentação
  em cascata (QGBT → QD1 → QD2) e quadro que alimenta outros quadros — falta somar demandas e quedas em série.
- ✅ Alimentadores, correções da revisão (01/10/2026, decisões do desenvolvedor; critérios do Ampere, não itens da norma):
  - IB em 3F/3F+N = maior corrente de fase **mais** a dos circuitos sem fase identificada no Revit (podem estar todos
    nessa fase); sem nenhuma fase identificada, o alimentador trifásico não é dimensionado. IB entra na memória com a
    conta (antes, S = √3·V·I voltava 1 ulp acima de I e recusava o disjuntor igual a ela).
  - Queda em 3F/3F+N com carga desequilibrada: a da carga mais desfavorecida. Fase-neutro: ΔV ≤ R·(IB + IN), com
    k = V / VFN (√3 na estrela; 2 no delta com neutro 240/120) e IN ≤ maior − menor corrente fase-neutro das fases mais
    a dos circuitos fase-neutro sem fase; entre fases: ΔV ≤ R·(I1 + I2) ≤ 2·R·IB (vale sempre). Fica a maior. Só cargas
    trifásicas: equilibrada (k = √3, IB).
  - **Premissa, não a favor da segurança:** o limite de IN vale com as cargas fase-neutro no mesmo fator de potência
    (fasores a 120°). Com fatores diferentes, IN passa dele (ex.: 3 × 10 A, uma carga com FP 0,85: IN ≈ 5,5 A, não 0 A),
    e a queda do alimentador sai menor que a real. A memória declara a premissa no passo "Corrente para a queda de
    tensão", com a referência "critério do Ampere". Para conferir: um limite que não depende do FP (ex.: somar
    2·sen(Δφ/2)·ΣIFN para a faixa de FP do projeto) ou a conta fasorial com o FP de cada carga.
  - Limite: o total do perfil é tomado na **origem do alimentador** (o equipamento de onde ele sai); queda antes dela
    (ramal de entrada, medição → QGBT fora do modelo) não é contada — a memória diz isso.
  - Circuito reserva (AMP_TipoCarga) e reserva/espaço do Revit ficam fora da maior queda terminal; quadro de cargas com
    pendência (circuito sem tipo ou sem potência, incompatível com a alimentação) e terminal com a memória gravada
    diferente da refeita (modelo mudou desde o "Dimensionar circuitos") impedem o alimentador.
  - O adapter só aceita como alimentador o circuito que sai de outro equipamento (com quadro de origem) e tem o quadro
    entre os membros (o `GetElectricalSystems` do quadro também traz os que ele alimenta). Impedem: transformador
    (primário noutra tensão), mais de um alimentador (todos ficam apagados) e alimentador com outras cargas. Circuito é
    alimentador quando um membro distribui (tipo da família quadro, painel, QGBT ou transformador, ou com circuitos);
    seccionadora no circuito de um motor continua circuito terminal. Quadro de três fases precisa das três conhecidas
    (rótulos do sistema de distribuição). Terminal sem a proteção decidida (parado no IDR) impede o alimentador.
  - ✅ 6.2.7.1-b conferida no texto oficial (01/10/2026): ponto de entrega nos terminais secundários do transformador
    MT/BT da distribuidora, 7% — nova origem no "Dimensionar alimentadores".

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

- ✅ **Método D (enterrado) liberado em 01/10/2026**: Tabela 40 do solo e Tabela 44. A temperatura da linha enterrada é a
  do solo: a "Temperatura do solo" das condições do projeto (campo opcional do diálogo, guardado no JSON das condições
  sem mudar a versão) ou o AMP_TemperaturaAmbienteC do circuito; sem nenhuma das duas, o circuito enterrado fica com
  problema de dados (a temperatura do ar nunca vale como a do solo).
- Agrupamento em mais de uma camada (Tabela 43) e as linhas 2 a 5 da Tabela 42 (camada única) não são aplicados: o
  perfil usa a linha 1 (feixe), a de menores fatores, para todos os métodos não enterrados — também E, F e G (6.2.5.5.4).
- **Alumínio fora do diálogo**: o perfil só tem resistividade do cobre, e a seção mínima (Tabela 47) está com os valores
  do cobre (a ref já cita 16 mm² para alumínio). Para liberar: resistividade do alumínio com fonte e seção mínima por
  material no perfil.

## Previstas (ainda sem código)

- Referência do exemplo de memória da especificação §6.4 (`"ref": "6410.4.2.1.2"`): o formato não
  corresponde a um item conhecido da norma; não reutilizar sem conferir.
