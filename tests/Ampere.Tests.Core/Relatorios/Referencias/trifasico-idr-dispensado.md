# Memória de cálculo — circuito TUE-01

- **Perfil normativo:** FICTICIO-TESTE
- **Identificador (AMP_MemoriaCalculoId):** `sha256:9486bd1d639422c8091a923e22c877fdd35d6a0f502e17b32aec291e72883982`
- **Esquema do documento:** 1
- **Situação:** cálculo completo (19 passos)
- **Atenção:** perfil fictício, só para testes

> Valores arredondados só para leitura: até 4 casas decimais (abaixo de 1, quatro algarismos significativos). O documento JSON da memória guarda os valores completos.

## Passos

### 1. Corrente de projeto

- **Referência:** FICTÍCIO: regra da corrente de projeto
- **Expressão:** `IB = S / (√3 · V)`
- **Valores:** S = 6600 VA; V = 220 V
- **Resultado:** 17,3205 A

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
- **Valores:** Smín = 2,5 mm²; IB = 17,3205 A; FCA = 1; FCT = 1
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
- **Valores:** S = 4 mm²; IZ₀ = 27 A; FCA = 1; FCT = 1
- **Resultado:** 27 A
- **Observação:** seção elevada de 2,5 para 4 mm²: nenhum disjuntor entre IB = 17,3205 A e IZ = 18 A

### 10. Disjuntor

- **Referência:** FICTÍCIO: regra de coordenação
- **Expressão:** `menor In com IB ≤ In ≤ IZ`
- **Valores:** IB = 17,3205 A; IZ = 27 A
- **Resultado:** 20 A
- **Observação:** correntes nominais: FICTÍCIO: disjuntores

### 11. Queda de tensão

- **Referência:** FICTÍCIO: regra de queda
- **Expressão:** `ΔV% = k · ρ · L · IB / (S · V) · 100`
- **Valores:** k = 1,7321; ρ = 0,02 Ω·mm²/m; L = 15 m; IB = 17,3205 A; S = 4 mm²; V = 220 V
- **Resultado:** 1,0227%
- **Observação:** fórmula resistiva (sem reatância), só o circuito terminal

### 12. Exigência de IDR

- **Referência:** FICTÍCIO: IDR por local
- **Expressão:** `n = 0 (IDR dispensado pelo projetista)`
- **Valores:** total = 1 ponto
- **Resultado:** 0 pontos
- **Observação:** decisão do projetista, prevalece sobre a tabela (motivo: motivo de teste); pela tabela: LOCAL-MOLHADO (1 ponto): exige IΔn ≤ 30 mA; ATENÇÃO: a tabela exige IDR em 1 ponto

### 13. Seção do neutro

- **Referência:** FICTÍCIO: regra da seção do neutro
- **Expressão:** `SN = S`
- **Valores:** S = 4 mm²
- **Resultado:** 4 mm²
- **Observação:** premissa: sem harmônicas significativas, como nos condutores carregados; a seção reduzida permitida acima de 25 mm² não é aplicada

### 14. Seção do condutor de proteção

- **Referência:** FICTÍCIO: seção do PE
- **Expressão:** `SPE = tabela (S)`
- **Valores:** S = 4 mm²
- **Resultado:** 4 mm²
- **Observação:** condutor de proteção do mesmo material das fases

### 15. Condutores no eletroduto

- **Referência:** FICTÍCIO: regra dos condutores no eletroduto
- **Expressão:** `n = 3F + N + PE`
- **Resultado:** 5 condutores
- **Observação:** neutro e proteção com o diâmetro da fase (conservador para a ocupação)

### 16. Diâmetro externo do condutor

- **Referência:** FICTÍCIO: catálogo de condutores
- **Expressão:** `d = catálogo (FIO-TESTE; 4 mm²)`
- **Resultado:** 5 mm

### 17. Taxa máxima de ocupação

- **Referência:** FICTÍCIO: ocupação
- **Expressão:** `taxa = tabela (5 condutores: faixa de 3 ou mais)`
- **Resultado:** 40%

### 18. Eletroduto adotado

- **Referência:** FICTÍCIO: catálogo de eletrodutos
- **Expressão:** `menor Di com n · d² / Di² · 100 ≤ taxa`
- **Valores:** n = 5 condutores; d = 5 mm; taxa = 40%
- **Resultado:** 20 mm
- **Observação:** tamanho nominal C (ELETRODUTO-TESTE); acima da taxa: A (125%), B (55,5556%)

### 19. Ocupação do eletroduto

- **Referência:** FICTÍCIO: ocupação
- **Expressão:** `ocupação = n · d² / Di² · 100`
- **Valores:** n = 5 condutores; d = 5 mm; Di = 20 mm
- **Resultado:** 31,25%
- **Observação:** só os condutores deste circuito: outros circuitos na mesma tubulação não entram
