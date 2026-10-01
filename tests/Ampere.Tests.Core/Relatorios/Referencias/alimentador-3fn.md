# Memória de cálculo — circuito Alimentador QD1

- **Perfil normativo:** FICTICIO-TESTE
- **Identificador (AMP_MemoriaCalculoId):** `sha256:016987fd52292a14a99baab61cb5e0925efd0244d6f4a3a78b498cff7e5e4951`
- **Esquema do documento:** 1
- **Situação:** cálculo completo (20 passos)
- **Atenção:** perfil fictício, só para testes

> Valores arredondados só para leitura: até 4 casas decimais (abaixo de 1, quatro algarismos significativos). O documento JSON da memória guarda os valores completos.

## Passos

### 1. Corrente de projeto

- **Referência:** FICTÍCIO: regra da corrente de projeto
- **Expressão:** `IB = máx(I(A); I(B); I(C))`
- **Valores:** I(A) = 3,7795 A; I(B) = 15 A; I(C) = 10 A
- **Resultado:** 15 A
- **Observação:** corrente de cada fase do QD1: soma das correntes de linha dos circuitos nela, pela demanda (F+N e 2F: S / V; 3F e 3F+N: S / (√3 · V); 2F+N: S / (2 · V fase-neutro)), a favor da segurança; demanda total 3315 VA; quadro de cargas sha256:95c8fa8b9265b8da932d5830d14fcf08d157ce91ff8cf9e025cdbc46f505fc0e

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
- **Observação:** total: instalação alimentada em baixa tensão pela distribuidora (a partir do ponto de entrega), tomado na origem do alimentador (QGBT): o trecho antes dela não está no modelo; terminal: a maior queda dos circuitos do QD1 (TUG-01)

### 9. Corrente para a queda de tensão

- **Referência:** Critério do Ampere, não item da norma (data/DATA_GAPS.md, alimentadores)
- **Expressão:** `IΔV = IB + IN; IN = máx(IFN) − mín(IFN)`
- **Valores:** IB = 15 A; IFN(A) = 3,7795 A; IFN(B) = 5 A; IFN(C) = 0 A
- **Resultado:** 20 A
- **Observação:** carga fase-neutro na fase de maior corrente, com o retorno pelo neutro (IFN: corrente dos circuitos F+N e 2F+N em cada fase). Premissa: as cargas fase-neutro no mesmo fator de potência; com fatores diferentes, IN pode passar da diferença entre as fases. k = V / VFN = 1,7321 dá a queda sobre a tensão fase-neutro; entre fases, 2 · IB = 30 A, menor que k · IΔV

### 10. Capacidade de condução da seção adotada

- **Referência:** FICTÍCIO: capacidade de condução
- **Expressão:** `IZ = IZ₀(S) · FCA · FCT`
- **Valores:** S = 2,5 mm²; IZ₀ = 18 A; FCA = 1; FCT = 1
- **Resultado:** 18 A

### 11. Disjuntor

- **Referência:** FICTÍCIO: regra de coordenação
- **Expressão:** `menor In com IB ≤ In ≤ IZ`
- **Valores:** IB = 15 A; IZ = 18 A
- **Resultado:** 16 A
- **Observação:** correntes nominais: FICTÍCIO: disjuntores

### 12. Queda de tensão

- **Referência:** FICTÍCIO: regra de queda
- **Expressão:** `ΔV% = k · ρ · L · IΔV / (S · V) · 100`
- **Valores:** k = 1,7321; ρ = 0,02 Ω·mm²/m; L = 30 m; IΔV = 20 A; S = 2,5 mm²; V = 220 V
- **Resultado:** 3,779%
- **Observação:** fórmula resistiva (sem reatância), só o alimentador; L: AMP_ComprimentoRotaM, informado pelo projetista

### 13. Exigência de IDR

- **Referência:** FICTÍCIO: IDR por local
- **Expressão:** `sem IDR no alimentador (sem decisão do projetista)`
- **Resultado:** 0 IDR
- **Observação:** a tabela de IDR por local vale para os circuitos terminais; IDR no alimentador só por decisão do projetista (AMP_IDR_DecisaoProjetista)

### 14. Seção do neutro

- **Referência:** FICTÍCIO: regra da seção do neutro
- **Expressão:** `SN = S`
- **Valores:** S = 2,5 mm²
- **Resultado:** 2,5 mm²
- **Observação:** premissa: sem harmônicas significativas, como nos condutores carregados; a seção reduzida permitida acima de 25 mm² não é aplicada

### 15. Seção do condutor de proteção

- **Referência:** FICTÍCIO: seção do PE
- **Expressão:** `SPE = tabela (S)`
- **Valores:** S = 2,5 mm²
- **Resultado:** 2,5 mm²
- **Observação:** condutor de proteção do mesmo material das fases

### 16. Condutores no eletroduto

- **Referência:** FICTÍCIO: regra dos condutores no eletroduto
- **Expressão:** `n = 3F + N + PE`
- **Resultado:** 5 condutores
- **Observação:** neutro e proteção com o diâmetro da fase (conservador para a ocupação)

### 17. Diâmetro externo do condutor

- **Referência:** FICTÍCIO: catálogo de condutores
- **Expressão:** `d = catálogo (FIO-TESTE; 2,5 mm²)`
- **Resultado:** 4 mm

### 18. Taxa máxima de ocupação

- **Referência:** FICTÍCIO: ocupação
- **Expressão:** `taxa = tabela (5 condutores: faixa de 3 ou mais)`
- **Resultado:** 40%

### 19. Eletroduto adotado

- **Referência:** FICTÍCIO: catálogo de eletrodutos
- **Expressão:** `menor Di com n · d² / Di² · 100 ≤ taxa`
- **Valores:** n = 5 condutores; d = 4 mm; taxa = 40%
- **Resultado:** 15 mm
- **Observação:** tamanho nominal B (ELETRODUTO-TESTE); acima da taxa: A (80%)

### 20. Ocupação do eletroduto

- **Referência:** FICTÍCIO: ocupação
- **Expressão:** `ocupação = n · d² / Di² · 100`
- **Valores:** n = 5 condutores; d = 4 mm; Di = 15 mm
- **Resultado:** 35,5556%
- **Observação:** só os condutores deste circuito: outros circuitos na mesma tubulação não entram
