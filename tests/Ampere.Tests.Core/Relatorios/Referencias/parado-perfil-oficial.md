# Memória de cálculo — circuito IL-01

- **Perfil normativo:** NBR5410:2004
- **Identificador (AMP_MemoriaCalculoId):** `sha256:035a59237237820a8afd2c4552479fd8e1d134fe458a54d56b57176351317063`
- **Esquema do documento:** 1
- **Situação:** cálculo interrompido no passo 12 (Exigência de IDR): tabela protecao_diferencial_por_local sem dados oficiais (TODO_NORMA)
- **Referências pendentes (TODO_NORMA ou TODO_CATALOGO):** 1 passo (12), sem fonte oficial

> Valores arredondados só para leitura: até 4 casas decimais (abaixo de 1, quatro algarismos significativos). O documento JSON da memória guarda os valores completos.

## Passos

### 1. Corrente de projeto

- **Referência:** NBR 5410:2004, item 5.3.4.1 (IB é a corrente de projeto do circuito; Iz ver 6.2.5)
- **Expressão:** `IB = S / V`
- **Valores:** S = 200 VA; V = 127 V
- **Resultado:** 1,5748 A

### 2. Condutores carregados

- **Referência:** NBR 5410:2004, Tabelas 36-41 e 6.2.6.2 (neutro conta como carregado so com harmonicas \>15%; sem harmonicas, nao conta)
- **Expressão:** `n = condutores carregados (F+N)`
- **Resultado:** 2 condutores

### 3. Fator de correção de temperatura

- **Referência:** NBR 5410:2004, Tabela 40 (ar; 30 C = 1,0 e a referencia)
- **Expressão:** `FCT = tabela (PVC; 30 °C)`
- **Valores:** θ = 30 °C
- **Resultado:** 1

### 4. Fator de correção de agrupamento

- **Referência:** NBR 5410:2004, Tabela 42, linha 1 (em feixe); faixas por limite superior
- **Expressão:** `FCA = tabela (1 circuito)`
- **Valores:** circuitos = 1
- **Resultado:** 1

### 5. Seção mínima

- **Referência:** NBR 5410:2004, item 6.2.6.1.1 e Tabela 47 (instalacoes fixas, condutores e cabos isolados, cobre; aluminio 16 mm2)
- **Expressão:** `Smín = tabela (Iluminacao)`
- **Resultado:** 1,5 mm²

### 6. Seção pela capacidade de condução

- **Referência:** NBR 5410:2004, Tabelas 36 e 37 (A1 a D; transcrita do texto oficial por coordenadas, validada contra IEC 60364-5-52)
- **Expressão:** `menor S ≥ Smín com IZ₀(S) · FCA · FCT ≥ IB`
- **Valores:** Smín = 1,5 mm²; IB = 1,5748 A; FCA = 1; FCT = 1
- **Resultado:** 1,5 mm²
- **Observação:** seções nominais: NBR 5410:2004, Tabelas 36-39 (serie de secoes listada)

### 7. Resistividade do condutor

- **Referência:** Condutividade do cobre eletrolitico a 20 C (1/56 O.mm2/m), conforme ABNT NBR IEC 60228; valor a 20 C — o uso do valor de servico (condutor aquecido) e decisao do projetista, registrada nas Simplificacoes do data/DATA_GAPS.md
- **Expressão:** `ρ = tabela (Cobre)`
- **Resultado:** 0,0179 Ω·mm²/m

### 8. Limite de queda de tensão

- **Referência:** NBR 5410:2004, itens 6.2.7.1 e 6.2.7.2
- **Expressão:** `ΔV%máx = tabela (circuito terminal)`
- **Resultado:** 4%

### 9. Capacidade de condução da seção adotada

- **Referência:** NBR 5410:2004, Tabelas 36 e 37 (A1 a D; transcrita do texto oficial por coordenadas, validada contra IEC 60364-5-52)
- **Expressão:** `IZ = IZ₀(S) · FCA · FCT`
- **Valores:** S = 1,5 mm²; IZ₀ = 17,5 A; FCA = 1; FCT = 1
- **Resultado:** 17,5 A

### 10. Disjuntor

- **Referência:** NBR 5410:2004, item 5.3.4.1 (IB \<= In \<= Iz e I2 \<= 1,45 Iz)
- **Expressão:** `menor In com IB ≤ In ≤ IZ`
- **Valores:** IB = 1,5748 A; IZ = 17,5 A
- **Resultado:** 6 A
- **Observação:** correntes nominais: Series preferenciais de disjuntores: ABNT NBR IEC 60898-1 (6 a 63 A, minidisjuntor) e IEC 60947-2 (caixa moldada, acima), adotadas pelos fabricantes nacionais (WEG, Schneider); decisao de produto registrada em data/DATA_GAPS.md (GAP-004)

### 11. Queda de tensão

- **Referência:** NBR 5410:2004, itens 6.2.7.1 e 6.2.7.2
- **Expressão:** `ΔV% = k · ρ · L · IB / (S · V) · 100`
- **Valores:** k = 2; ρ = 0,0179 Ω·mm²/m; L = 8 m; IB = 1,5748 A; S = 1,5 mm²; V = 127 V
- **Resultado:** 0,2368%
- **Observação:** fórmula resistiva (sem reatância), só o circuito terminal

### 12. Exigência de IDR

- **Referência:** TODO_NORMA
- **Expressão:** `n = pontos em locais que exigem IDR para Iluminação`
- **Resultado:** não calculado
- **Observação:** tabela protecao_diferencial_por_local sem dados oficiais (TODO_NORMA)
