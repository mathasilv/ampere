# Memória de cálculo — circuito TUG-01

- **Perfil normativo:** NBR5410:2004
- **Identificador (AMP_MemoriaCalculoId):** `sha256:32917aa6e893eec48604c5d7d040bc1037b300cb9dc42fa3aff79847f318cfc5`
- **Esquema do documento:** 1
- **Situação:** cálculo completo (17 passos)
- **Referências pendentes (TODO_NORMA ou TODO_CATALOGO):** 1 passo (13), sem fonte oficial

> Valores arredondados só para leitura: até 4 casas decimais (abaixo de 1, quatro algarismos significativos). O documento JSON da memória guarda os valores completos.

## Passos

### 1. Corrente de projeto

- **Referência:** NBR 5410:2004, item 5.3.4.1 (IB é a corrente de projeto do circuito; Iz ver 6.2.5)
- **Expressão:** `IB = S / V`
- **Valores:** S = 1270 VA; V = 127 V
- **Resultado:** 10 A

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

- **Referência:** NBR 5410:2004, Tabela 42, linha 1 (em feixe); chave = início da faixa (9 a 11, 12 a 15, 16 a 19, 20 ou mais)
- **Expressão:** `FCA = tabela (1 circuito)`
- **Valores:** circuitos = 1
- **Resultado:** 1

### 5. Seção mínima

- **Referência:** NBR 5410:2004, item 6.2.6.1.1 e Tabela 47 (instalacoes fixas, condutores e cabos isolados, cobre; aluminio 16 mm2)
- **Expressão:** `Smín = tabela (Forca)`
- **Resultado:** 2,5 mm²

### 6. Seção pela capacidade de condução

- **Referência:** NBR 5410:2004, Tabelas 36 e 37 (A1 a D; transcrita do texto oficial por coordenadas, validada contra IEC 60364-5-52)
- **Expressão:** `menor S ≥ Smín com IZ₀(S) · FCA · FCT ≥ IB`
- **Valores:** Smín = 2,5 mm²; IB = 10 A; FCA = 1; FCT = 1
- **Resultado:** 2,5 mm²
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
- **Valores:** S = 2,5 mm²; IZ₀ = 24 A; FCA = 1; FCT = 1
- **Resultado:** 24 A

### 10. Disjuntor

- **Referência:** NBR 5410:2004, item 5.3.4.1 (IB \<= In \<= Iz e I2 \<= 1,45 Iz)
- **Expressão:** `menor In com IB ≤ In ≤ IZ`
- **Valores:** IB = 10 A; IZ = 24 A
- **Resultado:** 10 A
- **Observação:** correntes nominais: Series preferenciais de disjuntores: ABNT NBR IEC 60898-1 (6 a 63 A, minidisjuntor) e IEC 60947-2 (caixa moldada, acima), adotadas pelos fabricantes nacionais (WEG, Schneider); decisao de produto registrada em data/DATA_GAPS.md (GAP-004)

### 11. Queda de tensão

- **Referência:** NBR 5410:2004, itens 6.2.7.1 e 6.2.7.2
- **Expressão:** `ΔV% = k · ρ · L · IB / (S · V) · 100`
- **Valores:** k = 2; ρ = 0,0179 Ω·mm²/m; L = 12 m; IB = 10 A; S = 2,5 mm²; V = 127 V
- **Resultado:** 1,3531%
- **Observação:** fórmula resistiva (sem reatância), só o circuito terminal

### 12. Exigência de IDR

- **Referência:** NBR 5410:2004, item 5.1.3.2.2 (alineas a-e: DR In \<= 30 mA; nota 1: tomadas ate 32 A; casos da secao 9 verificados em 9.1); locais nao listados nao exigem
- **Expressão:** `n = pontos em locais que exigem IDR para TUG`
- **Valores:** total = 1 ponto
- **Resultado:** 0 pontos
- **Observação:** Demais locais internos (1 ponto): não exige

### 13. Condutores no eletroduto

- **Referência:** TODO_NORMA
- **Expressão:** `n = F + N + PE`
- **Resultado:** 3 condutores
- **Observação:** neutro e proteção com o diâmetro da fase (conservador para a ocupação)

### 14. Diâmetro externo do condutor

- **Referência:** Prysmian, ficha técnica Superastic Flex 450/750 V (rodapé BW_005_02_PT), p. 4: diâmetro nominal externo
- **Expressão:** `d = catálogo (Prysmian Superastic Flex 450/750 V; 2,5 mm²)`
- **Resultado:** 3,5 mm
- **Observação:** isolação do condutor: PVC, a do circuito

### 15. Taxa máxima de ocupação

- **Referência:** NBR 5410:2004, item 6.2.11.1.6, alinea a
- **Expressão:** `taxa = tabela (3 condutores)`
- **Resultado:** 40%

### 16. Eletroduto adotado

- **Referência:** Tigre, Ficha Técnica Tigreflex (setembro/2025), p. 1: diâmetro interno
- **Expressão:** `menor Di com n · d² / Di² · 100 ≤ taxa`
- **Valores:** n = 3 condutores; d = 3,5 mm; taxa = 40%
- **Resultado:** 15 mm
- **Observação:** tamanho nominal DN 20 (Tigre Tigreflex amarelo)

### 17. Ocupação do eletroduto

- **Referência:** NBR 5410:2004, item 6.2.11.1.6, alinea a
- **Expressão:** `ocupação = n · d² / Di² · 100`
- **Valores:** n = 3 condutores; d = 3,5 mm; Di = 15 mm
- **Resultado:** 16,3333%
- **Observação:** só os condutores deste circuito: outros circuitos na mesma tubulação não entram
