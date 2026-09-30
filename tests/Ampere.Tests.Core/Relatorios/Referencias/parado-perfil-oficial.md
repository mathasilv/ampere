# Memória de cálculo — circuito IL-01

- **Perfil normativo:** NBR5410:2004
- **Identificador (AMP_MemoriaCalculoId):** `sha256:50b656f4f41a94f596f7489a361df4d318e8a401d8a6720f8aa16f37c99c8b1a`
- **Esquema do documento:** 1
- **Situação:** cálculo interrompido no passo 7 (Correntes nominais de disjuntor): tabela correntes_nominais_disjuntor_a sem dados oficiais (TODO_NORMA)
- **Referências pendentes (TODO_NORMA ou TODO_CATALOGO):** 1 passo (7), sem fonte oficial

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

### 7. Correntes nominais de disjuntor

- **Referência:** TODO_NORMA
- **Expressão:** `In ∈ correntes nominais`
- **Resultado:** não calculado
- **Observação:** tabela correntes_nominais_disjuntor_a sem dados oficiais (TODO_NORMA)
