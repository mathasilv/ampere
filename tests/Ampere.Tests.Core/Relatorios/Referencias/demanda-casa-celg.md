# Memória de cálculo — demanda da entrada (CELG CT 04/18)

- **Perfil normativo:** CELG CT 04/18
- **Identificador (AMP_MemoriaCalculoId):** `sha256:9aab5da4927028684cc5b8f350a6b6f9bc28ccb98d0dcfe69a37bd48709f878e`
- **Esquema do documento:** 1
- **Situação:** cálculo completo (9 passos)

> Valores arredondados só para leitura: até 4 casas decimais (abaixo de 1, quatro algarismos significativos). O documento JSON da memória guarda os valores completos.

## Documento da distribuidora

- **Fonte:** CELG Distribuição S.A. (grupo Enel; hoje Equatorial Goiás), Comunicado Técnico nº 04/18 – Fornecimento de Energia Elétrica em Tensão Secundária de Distribuição – Projeto Cabo Concêntrico, jan/2018: item 10 (cálculo da demanda) e Anexo A, Tabelas 2 a 5
- **Situação:** Transcrita pelo usuário e conferida contra o PDF em 01/10/2026 (todos os valores das Tabelas 2 a 5 conferem). Vigência a conferir: o Grupo Equatorial publica a NT.00001.EQTL (fornecimento em baixa tensão); esta fica em uso até ela ser anexada (decisão do usuário, 01/10/2026).

## Parcelas

- **a — iluminação e TUG:** 33 pontos · instalada 4800 VA · demanda 2496 VA
- **b1 — chuveiros:** 2 pontos · instalada 10800 VA · demanda 7344 VA
- **b2 — torneiras:** 1 ponto · instalada 3000 VA · demanda 3000 VA
- **b6b — fornos e fogões acima de 3,5 kW:** 1 ponto · instalada 4000 VA · demanda 3200 VA
- **b8 — micro-ondas:** 1 ponto · instalada 1200 VA · demanda 1200 VA
- **c — ar-condicionado:** 2 pontos · instalada 3300 VA · demanda 3300 VA
- **d — motores:** 1 ponto · instalada 750 VA · demanda 750 VA
- **f — outros aparelhos:** 1 ponto · instalada 2000 VA · demanda 2000 VA
- **Total:** instalada 29850 VA · demanda 23290 VA

## Passos

### 1. a — iluminação e TUG

- **Referência:** CELG CT 04/18, Anexo A, Tabela 2 (p. 15)
- **Expressão:** `a = P · fd(P)`
- **Valores:** P = 4,8 kVA; fd = 0,52
- **Resultado:** 2,496 kVA
- **Observação:** Residências; P: 33 pontos de iluminação e TUG, potência instalada em kVA tomada como kW (decisão do usuário, 01/10/2026); a carga mínima de 30 W/m² não é conferida (o Ampere não lê a área); faixa 4 \< P ≤ 5 kW: 52%: o fator vale para P inteira, como impresso

### 2. b1 — chuveiros

- **Referência:** CELG CT 04/18, Anexo A, Tabela 3 (p. 16)
- **Expressão:** `b1 = fd(n) · P`
- **Valores:** n = 2 aparelhos; P = 10,8 kVA; fd = 0,68
- **Resultado:** 7,344 kVA
- **Observação:** coluna "Chuveiro elétrico"; faixa 1 \< n ≤ 2: 68%; P: potência instalada em kVA tomada como kW (decisão do usuário, 01/10/2026)

### 3. b2 — torneiras

- **Referência:** CELG CT 04/18, Anexo A, Tabela 3 (p. 16)
- **Expressão:** `b2 = fd(n) · P`
- **Valores:** n = 1 aparelho; P = 3 kVA; fd = 1
- **Resultado:** 3 kVA
- **Observação:** coluna "Torneira elétrica, máquina de lavar louça e aquecedor de passagem"; faixa n ≤ 1: 100%; P: potência instalada em kVA tomada como kW (decisão do usuário, 01/10/2026)

### 4. b6b — fornos e fogões acima de 3,5 kW

- **Referência:** CELG CT 04/18, Anexo A, Tabela 5 (p. 18)
- **Expressão:** `b6b = fd(n) · P`
- **Valores:** n = 1 aparelho; P = 4 kVA; fd = 0,8
- **Resultado:** 3,2 kVA
- **Observação:** coluna "superior a 3,5 kW" (cada aparelho pela sua potência); faixa n ≤ 1: 80%; P: potência instalada em kVA tomada como kW (decisão do usuário, 01/10/2026)

### 5. b8 — micro-ondas

- **Referência:** CELG CT 04/18, Anexo A, Tabela 3 (p. 16)
- **Expressão:** `b8 = fd(n) · P`
- **Valores:** n = 1 aparelho; P = 1,2 kVA; fd = 1
- **Resultado:** 1,2 kVA
- **Observação:** coluna "Forno de micro-ondas"; faixa n ≤ 1: 100%; P: potência instalada em kVA tomada como kW (decisão do usuário, 01/10/2026)

### 6. c — ar-condicionado

- **Referência:** CELG CT 04/18, Anexo A, Tabela 4 (p. 17)
- **Expressão:** `c = fd(n) · P`
- **Valores:** n = 2 aparelhos; P = 3,3 kVA; fd = 1
- **Resultado:** 3,3 kVA
- **Observação:** coluna residencial (Residências); faixa n ≤ 10: 100%; unidade central de condicionamento entra com 100% (classifique-a como TUE, aparelho Outro)

### 7. d — motores

- **Referência:** CELG CT 04/18, item 10, d.2 (p. 11): indústrias e outros, fator compatível com a atividade, de responsabilidade do projetista
- **Expressão:** `d = fd · P`
- **Valores:** P = 0,75 kVA; fd = 1
- **Resultado:** 0,75 kVA
- **Observação:** fator do projetista (bomba de recalque, uso contínuo); 1 ponto

### 8. f — outros aparelhos

- **Referência:** Critério do Ampere, não item do documento: aparelho fora das tabelas entra sem fator (a favor da segurança)
- **Expressão:** `f = P`
- **Valores:** P = 2 kVA
- **Resultado:** 2 kVA
- **Observação:** aparelho 'Outro' (fora das tabelas do documento); 1 ponto

### 9. Demanda total

- **Referência:** CELG CT 04/18, item 10 (pp. 10-11)
- **Expressão:** `D = a + b1 + b2 + b6b + b8 + c + d + f`
- **Valores:** a = 2,496 kVA; b1 = 7,344 kVA; b2 = 3 kVA; b6b = 3,2 kVA; b8 = 1,2 kVA; c = 3,3 kVA; d = 0,75 kVA; f = 2 kVA
- **Resultado:** 23,29 kVA
- **Observação:** fórmula do documento: D = a + (b1 + b2 + b3 + b4 + b5 + b6 + b7 + b8) + c + d + e; parcelas sem pontos ficam de fora; reservas não entram
