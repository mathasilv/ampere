# Memória de cálculo — circuito TUG-04

- **Perfil normativo:** FICTICIO-TESTE
- **Identificador (AMP_MemoriaCalculoId):** `sha256:1462506db6993ce3de3b427e55d3b51a4b08f44df403fa187a1ce2735571eabe`
- **Esquema do documento:** 1
- **Situação:** cálculo completo (21 passos)
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
- **Expressão:** `FCT = tabela (PVC; 40 °C)`
- **Valores:** θ = 40 °C
- **Resultado:** 0,8
- **Observação:** θ: AMP_TemperaturaAmbienteC do circuito (o projeto usa 30 °C; justificativa: circuito no forro, padrão da obra)

### 4. Fator de correção de agrupamento

- **Referência:** FICTÍCIO: fator de agrupamento
- **Expressão:** `FCA = tabela (2 circuitos)`
- **Valores:** circuitos = 2
- **Resultado:** 0,8
- **Observação:** circuitos: AMP_CircuitosAgrupados do circuito (o projeto usa 1; justificativa: circuito no forro, padrão da obra)

### 5. Seção mínima

- **Referência:** FICTÍCIO: seção mínima
- **Expressão:** `Smín = tabela (Forca)`
- **Resultado:** 2,5 mm²

### 6. Seção mínima do projetista

- **Referência:** FICTÍCIO: seções
- **Expressão:** `S ≥ S do projetista`
- **Resultado:** 4 mm²
- **Observação:** decisão do projetista (justificativa: circuito no forro, padrão da obra); o cálculo pode adotar seção maior, nunca menor

### 7. Disjuntor do projetista

- **Referência:** FICTÍCIO: regra de coordenação
- **Expressão:** `IB ≤ In (IZ ≥ In verificado na seção)`
- **Valores:** IB = 10 A
- **Resultado:** 25 A
- **Observação:** decisão do projetista (justificativa: circuito no forro, padrão da obra); a seção sobe até IZ ≥ In

### 8. Seção pela capacidade de condução

- **Referência:** FICTÍCIO: capacidade de condução
- **Expressão:** `menor S ≥ Smín com IZ₀(S) · FCA · FCT ≥ In do projetista`
- **Valores:** Smín = 4 mm²; In = 25 A; FCA = 0,8; FCT = 0,8
- **Resultado:** 6 mm²
- **Observação:** seções nominais: FICTÍCIO: seções

### 9. Resistividade do condutor

- **Referência:** FICTÍCIO: resistividade
- **Expressão:** `ρ = tabela (Cobre)`
- **Resultado:** 0,02 Ω·mm²/m

### 10. Limite de queda de tensão

- **Referência:** FICTÍCIO: queda máxima
- **Expressão:** `ΔV%máx = tabela (circuito terminal)`
- **Resultado:** 5%

### 11. Capacidade de condução da seção adotada

- **Referência:** FICTÍCIO: capacidade de condução
- **Expressão:** `IZ = IZ₀(S) · FCA · FCT`
- **Valores:** S = 6 mm²; IZ₀ = 40 A; FCA = 0,8; FCT = 0,8
- **Resultado:** 25,6 A

### 12. Disjuntor

- **Referência:** FICTÍCIO: regra de coordenação
- **Expressão:** `In do projetista, com IB ≤ In ≤ IZ`
- **Valores:** IB = 10 A; IZ = 25,6 A
- **Resultado:** 25 A
- **Observação:** decisão do projetista, verificada (justificativa: circuito no forro, padrão da obra)

### 13. Queda de tensão

- **Referência:** FICTÍCIO: regra de queda
- **Expressão:** `ΔV% = k · ρ · L · IB / (S · V) · 100`
- **Valores:** k = 2; ρ = 0,02 Ω·mm²/m; L = 20 m; IB = 10 A; S = 6 mm²; V = 127 V
- **Resultado:** 1,0499%
- **Observação:** fórmula resistiva (sem reatância), só o circuito terminal; L: AMP_ComprimentoRotaM, informado pelo projetista

### 14. Exigência de IDR

- **Referência:** FICTÍCIO: IDR por local
- **Expressão:** `n = todos os pontos (IDR exigido pelo projetista)`
- **Valores:** total = 2 pontos
- **Resultado:** 2 pontos
- **Observação:** decisão do projetista, prevalece sobre a tabela (motivo: circuito no forro, padrão da obra); pela tabela: LOCAL-MOLHADO (1 ponto): exige IΔn ≤ 30 mA; LOCAL-SECO (1 ponto): não exige

### 15. Sensibilidade do IDR

- **Referência:** FICTÍCIO: IDR por local
- **Expressão:** `IΔn = informada pelo projetista`
- **Resultado:** 30 mA

### 16. Corrente nominal do IDR

- **Referência:** FICTÍCIO: regra IDR x disjuntor
- **Expressão:** `menor In(IDR) ≥ In(disjuntor)`
- **Valores:** In = 25 A
- **Resultado:** 25 A
- **Observação:** correntes nominais de IDR: FICTÍCIO: correntes de IDR

### 17. Condutores no eletroduto

- **Referência:** FICTÍCIO: regra dos condutores no eletroduto
- **Expressão:** `n = F + N + PE`
- **Resultado:** 3 condutores
- **Observação:** neutro e proteção com o diâmetro da fase (conservador para a ocupação)

### 18. Diâmetro externo do condutor

- **Referência:** FICTÍCIO: catálogo de condutores
- **Expressão:** `d = catálogo (FIO-TESTE; 6 mm²)`
- **Resultado:** 6 mm

### 19. Taxa máxima de ocupação

- **Referência:** FICTÍCIO: ocupação
- **Expressão:** `taxa = tabela (3 condutores)`
- **Resultado:** 40%

### 20. Eletroduto adotado

- **Referência:** FICTÍCIO: catálogo de eletrodutos
- **Expressão:** `menor Di com n · d² / Di² · 100 ≤ taxa`
- **Valores:** n = 3 condutores; d = 6 mm; taxa = 40%
- **Resultado:** 20 mm
- **Observação:** tamanho nominal C (ELETRODUTO-TESTE); acima da taxa: A (108%), B (48%)

### 21. Ocupação do eletroduto

- **Referência:** FICTÍCIO: ocupação
- **Expressão:** `ocupação = n · d² / Di² · 100`
- **Valores:** n = 3 condutores; d = 6 mm; Di = 20 mm
- **Resultado:** 27%
- **Observação:** só os condutores deste circuito: outros circuitos na mesma tubulação não entram
