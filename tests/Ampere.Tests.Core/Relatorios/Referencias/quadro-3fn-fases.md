# Memória de cálculo — quadro QD-04

- **Perfil normativo:** FICTICIO-TESTE
- **Identificador (AMP_MemoriaCalculoId):** `sha256:ddaad9f6a0e939dfb828fa7da7451125aacda41fd7d7525d22d97ea2f4ca2c32`
- **Esquema do documento:** 1
- **Situação:** cálculo completo (5 passos)
- **Atenção:** perfil fictício, só para testes

> Valores arredondados só para leitura: até 4 casas decimais (abaixo de 1, quatro algarismos significativos). O documento JSON da memória guarda os valores completos.

## Circuitos do quadro

- **IL-01 (Iluminação):** instalada 600 VA · fd 0,8 · demanda 480 VA
- **IL-02 (Iluminação):** instalada 400 VA · fd 0,8 · demanda 320 VA
- **TUG-01 (TUG):** instalada 1000 VA · fd 0,5 · demanda 500 VA
- **TUE-01 (TUE):** instalada 2000 VA · fd 1 · demanda 2000 VA
- **Total do quadro:** instalada 4000 VA · demanda 3300 VA · corrente 8,6603 A

## Cargas por fase

- **Fase A:** instalada 1600 VA · demanda 1480 VA · corrente 12,8704 A
- **Fase B:** instalada 1400 VA · demanda 1320 VA · corrente 11,6106 A
- **Fase C:** instalada 1000 VA · demanda 500 VA · corrente 3,937 A
- **Desequilíbrio:** 66,2162% = (maior − menor) / maior, pela demanda; fase mais carregada: A. Indicador para distribuir os circuitos: a NBR 5410 não fixa limite

## Passos

### 1. Demanda do tipo Iluminação

- **Referência:** FICTÍCIO: fatores de demanda
- **Expressão:** `D = ΣPI × fd`
- **Valores:** PI_Iluminacao = 1000 VA; fd = 0,8
- **Resultado:** 800 VA

### 2. Demanda do tipo TUG

- **Referência:** FICTÍCIO: fatores de demanda
- **Expressão:** `D = ΣPI × fd`
- **Valores:** PI_TUG = 1000 VA; fd = 0,5
- **Resultado:** 500 VA

### 3. Demanda do tipo TUE

- **Referência:** FICTÍCIO: fatores de demanda
- **Expressão:** `D = ΣPI × fd`
- **Valores:** PI_TUE = 2000 VA; fd = 1
- **Resultado:** 2000 VA

### 4. Demanda total do quadro

- **Referência:** FICTÍCIO: regra de demanda do quadro
- **Expressão:** `D_total = Σ D_tipo`
- **Valores:** D_Iluminacao = 800 VA; D_TUG = 500 VA; D_TUE = 2000 VA
- **Resultado:** 3300 VA

### 5. Corrente de demanda do quadro

- **Referência:** FICTÍCIO: regra da corrente de projeto
- **Expressão:** `I = D_total / (√3 × V)`
- **Valores:** D_total = 3300 VA; V = 220 V
- **Resultado:** 8,6603 A
- **Observação:** alimentação 3F+N 220 V: sistema de distribuição '220/127 Y' do quadro; corrente média, com as cargas supostas equilibradas entre as fases
