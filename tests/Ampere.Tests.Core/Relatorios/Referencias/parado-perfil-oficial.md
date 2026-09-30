# Memória de cálculo — circuito IL-01

- **Perfil normativo:** NBR5410:2004
- **Identificador (AMP_MemoriaCalculoId):** `sha256:9a681dc58b7db83a24f52b2a0b733d53cafaecc38de2287b09d41da9aeac4844`
- **Esquema do documento:** 1
- **Situação:** cálculo interrompido no passo 5 (Seção mínima): tabela secao_minima_mm2 sem dados oficiais (TODO_NORMA)
- **Referências pendentes (TODO_NORMA ou TODO_CATALOGO):** 1 passo (5), sem fonte oficial

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

- **Referência:** TODO_NORMA
- **Expressão:** `Smín = tabela (Iluminacao)`
- **Resultado:** não calculado
- **Observação:** tabela secao_minima_mm2 sem dados oficiais (TODO_NORMA)
