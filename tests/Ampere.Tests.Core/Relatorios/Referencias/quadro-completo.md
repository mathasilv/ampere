# Memória de cálculo — quadro QD-01

- **Perfil normativo:** FICTICIO-TESTE
- **Identificador (AMP_MemoriaCalculoId):** `sha256:1e480faea17f7e0954ea7f377cd67b8a30a97ffc1bd6b5c7a2e448ad60ad098d`
- **Esquema do documento:** 1
- **Situação:** cálculo completo (5 passos)
- **Atenção:** perfil fictício, só para testes

> Valores arredondados só para leitura: até 4 casas decimais (abaixo de 1, quatro algarismos significativos). O documento JSON da memória guarda os valores completos.

## Circuitos do quadro

- **IL-01 (Iluminação):** instalada 600 VA · fd 0,8 · demanda 480 VA
- **IL-02 (Iluminação):** instalada 400 VA · fd 0,8 · demanda 320 VA
- **TUG-01 (TUG):** instalada 1000 VA · fd 0,5 · demanda 500 VA
- **TUE-01 (TUE):** instalada 2000 VA · fd 1 · demanda 2000 VA
- **Total do quadro:** instalada 4000 VA · demanda 3300 VA · corrente 25,9843 A

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
- **Expressão:** `I = D_total / V`
- **Valores:** D_total = 3300 VA; V = 127 V
- **Resultado:** 25,9843 A
