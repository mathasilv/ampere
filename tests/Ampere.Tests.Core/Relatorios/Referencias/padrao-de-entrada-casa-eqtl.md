# Memória de cálculo — padrão de entrada (Equatorial NT.00001.EQTL rev. 09)

- **Documento da distribuidora:** Equatorial NT.00001.EQTL rev. 09
- **Identificador da memória (não gravado no modelo):** `sha256:d8bf256f0bb5e60e2bc613ceb2c8b9571ef0fc898de77445893e21041a8b3830`
- **Esquema do documento:** 1
- **Situação:** cálculo completo (10 passos)

> Valores arredondados só para leitura: até 4 casas decimais (abaixo de 1, quatro algarismos significativos). O documento JSON da memória guarda os valores completos.

## Documento da distribuidora

- **Fonte:** Equatorial Energia, NT.00001.EQTL – Fornecimento de Energia Elétrica em Baixa Tensão, revisão 09, homologada em 22/05/2025 (PDF anexado pelo usuário, não versionado); transcrito e conferido visualmente em 01/10/2026
- **Situação:** Opção (c) do usuário (01/10/2026): a demanda continua pela CELG CT 04/18; o padrão de entrada sai daqui, pela carga instalada (Tabelas 1 e 2). Errata da edição: as tabelas citam as notas 23 a 26, mas as notas impressas logo abaixo delas são a 19 a 22 (o texto de cada uma confere com a linha ou a coluna que a cita, com 4 a menos); a premissa dos cálculos é a nota 23 impressa. Aqui as notas vão com o número impresso.

## Escolha

- **Tabela:** Tabela 1 — 220/380 V
- **Fornecimento:** trifásico

## Passos

### 1. Carga instalada

- **Referência:** Equatorial NT.00001.EQTL rev. 09, Tabelas 1 e 2 (método de cálculo: carga instalada, em kW)
- **Expressão:** `C = ΣP / 1000`
- **Valores:** ΣP = 29850 VA
- **Resultado:** 29,85 kW
- **Observação:** pontos classificados, sem as reservas; potência instalada em kVA tomada como kW (decisão do usuário, 01/10/2026: a mesma da demanda)

### 2. Tipo de fornecimento

- **Referência:** Equatorial NT.00001.EQTL rev. 09 (homologada em 22/05/2025), Tabela 1 – Dimensionamento do Ramal de Conexão e Entrada das Instalações em 220/380V
- **Expressão:** `o primeiro fornecimento da Tabela 1 que vai até C`
- **Valores:** C = 29,85 kW
- **Resultado:** 3 fases
- **Observação:** trifásico em 220/380 V; premissas da distribuidora: fator de potência 0,92, fator de demanda 80% e queda de tensão de 2% na medição

### 3. Disjuntor termomagnético

- **Referência:** Equatorial NT.00001.EQTL rev. 09 (homologada em 22/05/2025), Tabela 1 – Dimensionamento do Ramal de Conexão e Entrada das Instalações em 220/380V; trifásico, acima de 24 e até 38 kW
- **Expressão:** `In = tabela (C)`
- **Valores:** C = 29,85 kW
- **Resultado:** 63 A

### 4. Condutor do cliente — fase

- **Referência:** Equatorial NT.00001.EQTL rev. 09 (homologada em 22/05/2025), Tabela 1 – Dimensionamento do Ramal de Conexão e Entrada das Instalações em 220/380V; trifásico, acima de 24 e até 38 kW
- **Expressão:** `S = tabela (C)`
- **Resultado:** 10 mm²
- **Observação:** cobre isolado, mínimo; Nota 22: o condutor do cliente é dimensionado considerando uma isolação de 1 kV e temperatura de serviço de 90 °C; para características de isolação diferentes deve ser verificada qual a seção adequada

### 5. Condutor do cliente — neutro

- **Referência:** Equatorial NT.00001.EQTL rev. 09 (homologada em 22/05/2025), Tabela 1 – Dimensionamento do Ramal de Conexão e Entrada das Instalações em 220/380V; trifásico, acima de 24 e até 38 kW
- **Expressão:** `SN = tabela (C)`
- **Resultado:** 10 mm²
- **Observação:** cobre isolado, mínimo

### 6. Eletroduto de aço galvanizado

- **Referência:** Equatorial NT.00001.EQTL rev. 09 (homologada em 22/05/2025), Tabela 1 – Dimensionamento do Ramal de Conexão e Entrada das Instalações em 220/380V; trifásico, acima de 24 e até 38 kW
- **Expressão:** `Ø = tabela ("1.1/2")`
- **Resultado:** 1,5 pol
- **Observação:** diâmetro nominal

### 7. Condutor de aterramento (aço cobreado)

- **Referência:** Equatorial NT.00001.EQTL rev. 09 (homologada em 22/05/2025), Tabela 1 – Dimensionamento do Ramal de Conexão e Entrada das Instalações em 220/380V; trifásico, acima de 24 e até 38 kW
- **Expressão:** `S = tabela (C)`
- **Resultado:** 10 mm²

### 8. Eletroduto do aterramento

- **Referência:** Equatorial NT.00001.EQTL rev. 09 (homologada em 22/05/2025), Tabela 1 – Dimensionamento do Ramal de Conexão e Entrada das Instalações em 220/380V; trifásico, acima de 24 e até 38 kW
- **Expressão:** `Ø = tabela ("1")`
- **Resultado:** 1 pol
- **Observação:** diâmetro nominal

### 9. Ramal de conexão até 2 km da orla marítima — cobre multiplexado

- **Referência:** Equatorial NT.00001.EQTL rev. 09 (homologada em 22/05/2025), Tabela 1 – Dimensionamento do Ramal de Conexão e Entrada das Instalações em 220/380V; trifásico, acima de 24 e até 38 kW
- **Expressão:** `S = tabela (C)`
- **Resultado:** 16 mm²

### 10. Ramal de conexão a partir de 2 km da orla marítima — alumínio multiplexado quadruplex

- **Referência:** Equatorial NT.00001.EQTL rev. 09 (homologada em 22/05/2025), Tabela 1 – Dimensionamento do Ramal de Conexão e Entrada das Instalações em 220/380V; trifásico, acima de 24 e até 38 kW
- **Expressão:** `S = tabela (C)`
- **Resultado:** 25 mm²
