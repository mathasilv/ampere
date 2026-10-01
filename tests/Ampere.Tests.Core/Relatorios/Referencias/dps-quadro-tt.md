# Memória de cálculo — DPS do quadro QD1

- **Perfil normativo:** NBR5410:2004
- **Identificador da memória (não gravado no modelo):** `sha256:dac755a3b3b7c8df962d9a71cf3840cfe66adfd5574c598090385593ba6cb41c`
- **Esquema do documento:** 1
- **Situação:** cálculo completo (12 passos)

> Valores arredondados só para leitura: até 4 casas decimais (abaixo de 1, quatro algarismos significativos). O documento JSON da memória guarda os valores completos.

## Quadro

- **Quadro:** QD1
- **Alimentação:** 3F+N 220/127 V (sistema de distribuição '220/127 Y' do quadro)
- **Esquema de aterramento:** TT
- **Finalidade:** as duas: sobretensões transmitidas pela linha externa e de manobra, e descargas atmosféricas diretas
- **Dispositivo DR:** os DPS ficam a montante dos DR

## Localização

- **Referência:** NBR 5410:2004, item 6.3.5.2.1
- **Sobretensões transmitidas pela linha externa e de manobra:** junto ao ponto de entrada da linha na edificação ou no quadro de distribuição principal, localizado o mais próximo possível do ponto de entrada (alínea a)
- **Descargas atmosféricas diretas:** no ponto de entrada da linha na edificação (alínea b)

## A conferir no DPS escolhido

- **NBR 5410:2004, item 6.3.5.2.4, alínea c:** sobretensões temporárias: o DPS atende aos ensaios da IEC 61643-1
- **NBR 5410:2004, item 6.3.5.2.4, alínea e:** suportabilidade à corrente de curto-circuito, com o dispositivo de proteção que o integra ou que o fabricante especifica, igual ou superior à corrente de curto-circuito presumida no ponto de instalação; com centelhador, a capacidade de interrupção de corrente subsequente também
- **NBR 5410:2004, item 6.3.5.2.4, alínea f:** coordenação com os DPS a montante e a jusante, pelas instruções do fabricante
- **NBR 5410:2004, item 6.3.5.2.5:** proteção contra sobrecorrentes para a falha do DPS (na conexão do DPS ou no circuito), com corrente nominal inferior ou no máximo igual à indicada pelo fabricante do DPS
- **NBR 5410:2004, item 6.3.5.2.8:** indicação do estado do DPS (indicador próprio ou o dispositivo de proteção à parte)

## Passos

### 1. Esquema de conexão dos DPS

- **Referência:** NBR 5410:2004, item 6.3.5.2.2 e Figura 13; NBR 5410:2004, item 6.3.5.2.6, alínea b (esquema TT com os DPS a montante dos dispositivos DR)
- **Expressão:** `TT com os DPS a montante do DR → esquema 3`
- **Resultado:** 3
- **Observação:** DPS entre cada fase e o neutro e entre o neutro e o PE

### 2. Quantidade de DPS

- **Referência:** NBR 5410:2004, item 6.3.5.2.2 e Figura 13
- **Expressão:** `n = fases + 1 (neutro–PE)`
- **Valores:** fases = 3
- **Resultado:** 4 DPS

### 3. Uc mínimo — fase–neutro

- **Referência:** NBR 5410:2004, item 6.3.5.2.4, alínea b, e Tabela 49 (TT)
- **Expressão:** `Uc ≥ 1,1 · Uo`
- **Valores:** Uo = 127 V
- **Resultado:** 139,7 V
- **Observação:** mínimo da tabela; os valores adequados podem ser bem maiores (nota 4)

### 4. Uc mínimo — neutro–PE

- **Referência:** NBR 5410:2004, item 6.3.5.2.4, alínea b, e Tabela 49 (TT)
- **Expressão:** `Uc ≥ Uo`
- **Valores:** Uo = 127 V
- **Resultado:** 127 V

### 5. Nível de proteção máximo (Up)

- **Referência:** NBR 5410:2004, item 6.3.5.2.4, alínea a, e Tabela 31 (categoria II de suportabilidade a impulsos); linha 120/208, 127/220, 115-230, 120-240, 127-254
- **Expressão:** `Up ≤ tabela (Uo), categoria II`
- **Valores:** Uo = 127 V
- **Resultado:** 1,5 kV
- **Observação:** nível global, entre fase e PE (esquema 3); proteção de modo comum; DPS adicionais para equipamentos entre fase e neutro precisam de nível menor (alínea a, nota 1)

### 6. Corrente nominal de descarga mínima (In) — fase–neutro

- **Referência:** NBR 5410:2004, item 6.3.5.2.4, alínea d, primeira situação (sobretensões de origem atmosférica transmitidas pela linha externa e de manobra; onda 8/20 µs)
- **Expressão:** `In ≥ 5 kA por modo de proteção`
- **Resultado:** 5 kA

### 7. Corrente nominal de descarga mínima (In) — neutro–PE

- **Referência:** NBR 5410:2004, item 6.3.5.2.4, alínea d, primeira situação (sobretensões de origem atmosférica transmitidas pela linha externa e de manobra; onda 8/20 µs)
- **Expressão:** `In ≥ 20 kA (neutro–PE no esquema 3, rede trifásica)`
- **Resultado:** 20 kA

### 8. Corrente de impulso mínima (Iimp) — fase–neutro

- **Referência:** NBR 5410:2004, item 6.3.5.2.4, alínea d, segunda situação (descargas atmosféricas diretas sobre a edificação ou em suas proximidades, quando a corrente não puder ser determinada pela IEC 61312-1)
- **Expressão:** `Iimp ≥ 12,5 kA por modo de proteção`
- **Resultado:** 12,5 kA
- **Observação:** sem a corrente determinada pela IEC 61312-1, que vale quando puder ser determinada

### 9. Corrente de impulso mínima (Iimp) — neutro–PE

- **Referência:** NBR 5410:2004, item 6.3.5.2.4, alínea d, segunda situação (descargas atmosféricas diretas sobre a edificação ou em suas proximidades, quando a corrente não puder ser determinada pela IEC 61312-1)
- **Expressão:** `Iimp ≥ 50 kA (neutro–PE no esquema 3, rede trifásica)`
- **Resultado:** 50 kA
- **Observação:** sem a corrente determinada pela IEC 61312-1, que vale quando puder ser determinada

### 10. Corrente subsequente interrompida mínima — neutro–PE

- **Referência:** NBR 5410:2004, item 6.3.5.2.4, alínea e (DPS entre neutro e PE, esquema TN ou TT, quando incorporar centelhador)
- **Expressão:** `Ifi ≥ 100 A`
- **Resultado:** 100 A
- **Observação:** só para DPS com centelhador

### 11. Seção mínima do condutor de conexão ao PE

- **Referência:** NBR 5410:2004, item 6.3.5.2.9 e Figura 15
- **Expressão:** `S ≥ 16 mm²`
- **Resultado:** 16 mm²
- **Observação:** cobre ou equivalente; DPS contra descargas atmosféricas diretas

### 12. Comprimento total dos condutores de conexão

- **Referência:** NBR 5410:2004, item 6.3.5.2.9 e Figura 15
- **Expressão:** `a + b ≤ 0,5 m`
- **Resultado:** 0,5 m
- **Observação:** o mais curto possível, sem curvas ou laços; se a + b não puder ficar abaixo disso, o esquema da Figura 15-b
