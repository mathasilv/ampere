# Memória de cálculo — circuito TUG-03

- **Perfil normativo:** FICTICIO-TESTE
- **Identificador (AMP_MemoriaCalculoId):** `sha256:80b8ca477f4a0023407175b0d8995344287267858eaf6e749f94028ea4bbf8e6`
- **Esquema do documento:** 1
- **Situação:** cálculo interrompido no passo 16 (Diâmetro externo do condutor): catálogo de condutores sem dados (TODO_CATALOGO)
- **Referências pendentes (TODO_NORMA ou TODO_CATALOGO):** 1 passo (16), sem fonte oficial
- **Atenção:** perfil fictício, só para testes

> Valores arredondados só para leitura: até 4 casas decimais (abaixo de 1, quatro algarismos significativos). O documento JSON da memória guarda os valores completos.

## Passos

### 1. Corrente de projeto

- **Referência:** FICTÍCIO: regra da corrente de projeto
- **Expressão:** `IB = S / V`
- **Valores:** S = 1270 VA; V = 127 V
- **Resultado:** 10 A

### 2. Condutores carregados

- **Referência:** FICTÍCIO: condutores carregados
- **Expressão:** `n = condutores carregados (F+N)`
- **Resultado:** 2 condutores

### 3. Fator de correção de temperatura

- **Referência:** FICTÍCIO: fator de temperatura
- **Expressão:** `FCT = tabela (PVC; 30 °C)`
- **Valores:** θ = 30 °C
- **Resultado:** 1

### 4. Fator de correção de agrupamento

- **Referência:** FICTÍCIO: fator de agrupamento
- **Expressão:** `FCA = tabela (1 circuito)`
- **Valores:** circuitos = 1
- **Resultado:** 1

### 5. Seção mínima

- **Referência:** FICTÍCIO: seção mínima
- **Expressão:** `Smín = tabela (Forca)`
- **Resultado:** 2,5 mm²

### 6. Seção pela capacidade de condução

- **Referência:** FICTÍCIO: capacidade de condução
- **Expressão:** `menor S ≥ Smín com IZ₀(S) · FCA · FCT ≥ IB`
- **Valores:** Smín = 2,5 mm²; IB = 10 A; FCA = 1; FCT = 1
- **Resultado:** 2,5 mm²
- **Observação:** seções nominais: FICTÍCIO: seções

### 7. Resistividade do condutor

- **Referência:** FICTÍCIO: resistividade
- **Expressão:** `ρ = tabela (Cobre)`
- **Resultado:** 0,02 Ω·mm²/m

### 8. Limite de queda de tensão

- **Referência:** FICTÍCIO: queda máxima
- **Expressão:** `ΔV%máx = tabela (circuito terminal)`
- **Resultado:** 5%

### 9. Capacidade de condução da seção adotada

- **Referência:** FICTÍCIO: capacidade de condução
- **Expressão:** `IZ = IZ₀(S) · FCA · FCT`
- **Valores:** S = 2,5 mm²; IZ₀ = 20 A; FCA = 1; FCT = 1
- **Resultado:** 20 A

### 10. Disjuntor

- **Referência:** FICTÍCIO: regra de coordenação
- **Expressão:** `menor In com IB ≤ In ≤ IZ`
- **Valores:** IB = 10 A; IZ = 20 A
- **Resultado:** 10 A
- **Observação:** correntes nominais: FICTÍCIO: disjuntores

### 11. Queda de tensão

- **Referência:** FICTÍCIO: regra de queda
- **Expressão:** `ΔV% = k · ρ · L · IB / (S · V) · 100`
- **Valores:** k = 2; ρ = 0,02 Ω·mm²/m; L = 10 m; IB = 10 A; S = 2,5 mm²; V = 127 V
- **Resultado:** 1,2598%
- **Observação:** fórmula resistiva (sem reatância), só o circuito terminal

### 12. Exigência de IDR

- **Referência:** FICTÍCIO: IDR por local
- **Expressão:** `n = pontos em locais que exigem IDR para TUG`
- **Valores:** total = 1 ponto
- **Resultado:** 0 pontos
- **Observação:** LOCAL-SECO (1 ponto): não exige

### 13. Seção do neutro

- **Referência:** FICTÍCIO: regra da seção do neutro
- **Expressão:** `SN = S`
- **Valores:** S = 2,5 mm²
- **Resultado:** 2,5 mm²

### 14. Seção do condutor de proteção

- **Referência:** FICTÍCIO: seção do PE
- **Expressão:** `SPE = tabela (S)`
- **Valores:** S = 2,5 mm²
- **Resultado:** 2,5 mm²
- **Observação:** condutor de proteção do mesmo material das fases

### 15. Condutores no eletroduto

- **Referência:** FICTÍCIO: regra dos condutores no eletroduto
- **Expressão:** `n = F + N + PE`
- **Resultado:** 3 condutores
- **Observação:** neutro e proteção com o diâmetro da fase (conservador para a ocupação)

### 16. Diâmetro externo do condutor

- **Referência:** TODO_CATALOGO
- **Expressão:** `d = catálogo (FIO-TESTE; 2,5 mm²)`
- **Resultado:** não calculado
- **Observação:** catálogo de condutores sem dados (TODO_CATALOGO)
