# 0.1.0 (em desenvolvimento, sem versão publicada)

Primeira versão do Ampere: projeto de instalações elétricas de baixa tensão pela ABNT NBR 5410:2004 no Revit 2027
(compila também para 2025 e 2026), com memória de cálculo auditável. Validação no Revit pelo roteiro
[docs/validacao-no-revit.md](docs/validacao-no-revit.md).

## Parâmetros e dados

- Parâmetros compartilhados `AMP_*` com GUIDs congelados, criados pelo comando "Injetar parâmetros".
- Perfil normativo `data/normas/NBR5410/2004/perfil.json`: capacidade de condução (Tabelas 36 a 39, métodos A1 a G),
  fatores de temperatura (Tabela 40, ar e solo), de agrupamento (Tabelas 42 e 44), seções mínimas (Tabela 47), condutor
  de proteção (Tabela 58), construções admitidas por método (Tabela 33), queda de tensão (6.2.7), ocupação do eletroduto
  e IDR por local (5.1.3.2.2). Cada valor com a referência; o que não tem fonte fica em `data/DATA_GAPS.md`.
- Previsão mínima de cargas dos locais de habitação (9.5.2) e divisão da instalação (9.5.3).
- Seleção dos DPS (6.3.5.2, Figura 13 e Tabelas 31 e 49) em `data/normas/NBR5410/2004/dps.json`.
- Catálogos Prysmian (Superastic Flex, Sintenax Flex unipolar) e Tigre (Tigreflex, Tigreflex Reforçado, PVC rígido
  roscável).
- Distribuidoras: demanda pela CELG CT 04/18 e padrão de entrada pela Equatorial NT.00001.EQTL rev. 09 (Tabelas 1 e 2).

## Comandos

- **Classificar cargas** e **Locais pelos ambientes**: tipo, potência, tensão, fases, aparelho e local de IDR dos pontos,
  inclusive pelos ambientes de modelos vinculados.
- **Previsão de cargas**: confere cada cômodo de habitação contra a previsão mínima e a divisão dos circuitos; as
  categorias ficam guardadas no projeto.
- **Criar circuitos**: agrupa os pontos em circuitos numerados no quadro; com as categorias da previsão, tomadas de
  cozinha e áreas de serviço em circuitos só delas e equipamento acima de 10 A sozinho no circuito.
- **Dimensionar circuitos**: IB, seção, disjuntor, queda de tensão, IDR, neutro, PE e eletroduto, com memória (JSON,
  Markdown e PDF), planilha dos circuitos e lista de materiais; as condições do projeto ficam guardadas no modelo.
- **Montar quadro de cargas**: demanda por tipo de carga, cargas por fase e distribuição de fases sugerida.
- **Dimensionar alimentadores**: o circuito de cada quadro pela demanda e pela queda que sobra do limite total.
- **Demanda da entrada**: demanda pela distribuidora e padrão de entrada pela carga instalada.
- **DPS do quadro**: seleção dos DPS da linha de energia no quadro de entrada ou de distribuição principal (NBR 5410,
  6.3.5.2): esquema de conexão, Uc, Up, In ou Iimp, corrente subsequente, condutor de conexão e imunidade do DR, com
  memória e a lista do que conferir no catálogo.
- **Diagramas unifilares**: um diagrama por quadro numa vista de desenho, com seção, neutro, PE e proteção.
- **Verificar projeto**: o que falta ou mudou desde o último cálculo (inclusive resultado editado à mão), sem alterar
  o modelo.
