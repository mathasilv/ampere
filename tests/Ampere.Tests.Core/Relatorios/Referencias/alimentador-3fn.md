# Memória de cálculo — circuito Alimentador QD1

- **Perfil normativo:** FICTICIO-TESTE
- **Identificador (AMP_MemoriaCalculoId):** `sha256:aa20c3f8354416a56b670dda9faa662ba5e9cf814eacd402a4e4b01c891de07a`
- **Esquema do documento:** 1
- **Situação:** cálculo completo (17 passos)
- **Atenção:** perfil fictício, só para testes

> Valores arredondados só para leitura: até 4 casas decimais (abaixo de 1, quatro algarismos significativos). O documento JSON da memória guarda os valores completos.

## Passos

### 1. Corrente de projeto

- **Referência:** FICTÍCIO: regra da corrente de projeto
- **Expressão:** `IB = S / (√3 · V)`
- **Valores:** S = 5715,7677 VA; V = 220 V
- **Resultado:** 15 A
- **Observação:** S: √3 · V · I da fase de maior corrente do QD1 (B: 15 A, soma das correntes de linha dos circuitos pela demanda; demanda total 3315 VA), para IB ser a corrente dessa fase; quadro de cargas sha256:95c8fa8b9265b8da932d5830d14fcf08d157ce91ff8cf9e025cdbc46f505fc0e

### 2. Condutores carregados

- **Referência:** FICTÍCIO: condutores carregados
- **Expressão:** `n = condutores carregados (3F+N)`
- **Resultado:** 3 condutores

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
- **Valores:** Smín = 2,5 mm²; IB = 15 A; FCA = 1; FCT = 1
- **Resultado:** 2,5 mm²
- **Observação:** seções nominais: FICTÍCIO: seções

### 7. Resistividade do condutor

- **Referência:** FICTÍCIO: resistividade
- **Expressão:** `ρ = tabela (Cobre)`
- **Resultado:** 0,02 Ω·mm²/m

### 8. Limite de queda de tensão

- **Referência:** FICTÍCIO: queda máxima
- **Expressão:** `ΔV%máx = ΔV%total − ΔV%terminal`
- **Valores:** ΔV%total = 7%; ΔV%terminal = 1,2598%
- **Resultado:** 5,7402%
- **Observação:** total: instalação alimentada em baixa tensão pela distribuidora (a partir do ponto de entrega); terminal: a maior queda dos circuitos do QD1 (TUG-01)

### 9. Capacidade de condução da seção adotada

- **Referência:** FICTÍCIO: capacidade de condução
- **Expressão:** `IZ = IZ₀(S) · FCA · FCT`
- **Valores:** S = 2,5 mm²; IZ₀ = 18 A; FCA = 1; FCT = 1
- **Resultado:** 18 A

### 10. Disjuntor

- **Referência:** FICTÍCIO: regra de coordenação
- **Expressão:** `menor In com IB ≤ In ≤ IZ`
- **Valores:** IB = 15 A; IZ = 18 A
- **Resultado:** 16 A
- **Observação:** correntes nominais: FICTÍCIO: disjuntores

### 11. Queda de tensão

- **Referência:** FICTÍCIO: regra de queda
- **Expressão:** `ΔV% = k · ρ · L · IB / (S · V) · 100`
- **Valores:** k = 1,7321; ρ = 0,02 Ω·mm²/m; L = 30 m; IB = 15 A; S = 2,5 mm²; V = 220 V
- **Resultado:** 2,8343%
- **Observação:** fórmula resistiva (sem reatância), só o circuito terminal; L: AMP_ComprimentoRotaM, informado pelo projetista

### 12. Exigência de IDR

- **Referência:** FICTÍCIO: IDR por local
- **Expressão:** `n = 0 (alimentador de quadro)`
- **Resultado:** 0 pontos
- **Observação:** a tabela de IDR por local vale para os circuitos terminais; IDR no alimentador só por decisão do projetista (AMP_IDR_DecisaoProjetista)

### 13. Condutores no eletroduto

- **Referência:** FICTÍCIO: regra dos condutores no eletroduto
- **Expressão:** `n = 3F + N + PE`
- **Resultado:** 5 condutores
- **Observação:** neutro e proteção com o diâmetro da fase (conservador para a ocupação)

### 14. Diâmetro externo do condutor

- **Referência:** FICTÍCIO: catálogo de condutores
- **Expressão:** `d = catálogo (FIO-TESTE; 2,5 mm²)`
- **Resultado:** 4 mm

### 15. Taxa máxima de ocupação

- **Referência:** FICTÍCIO: ocupação
- **Expressão:** `taxa = tabela (5 condutores: faixa de 3 ou mais)`
- **Resultado:** 40%

### 16. Eletroduto adotado

- **Referência:** FICTÍCIO: catálogo de eletrodutos
- **Expressão:** `menor Di com n · d² / Di² · 100 ≤ taxa`
- **Valores:** n = 5 condutores; d = 4 mm; taxa = 40%
- **Resultado:** 15 mm
- **Observação:** tamanho nominal B (ELETRODUTO-TESTE); acima da taxa: A (80%)

### 17. Ocupação do eletroduto

- **Referência:** FICTÍCIO: ocupação
- **Expressão:** `ocupação = n · d² / Di² · 100`
- **Valores:** n = 5 condutores; d = 4 mm; Di = 15 mm
- **Resultado:** 35,5556%
- **Observação:** só os condutores deste circuito: outros circuitos na mesma tubulação não entram
