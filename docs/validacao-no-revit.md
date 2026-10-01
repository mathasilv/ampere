# Validação no Revit 2027

Este roteiro serve para conferir no Revit o que os testes automáticos não alcançam. O núcleo de cálculo (Core) roda em
qualquer máquina e é coberto pelos testes. Já o adapter do Revit e os comandos só são validados de verdade no Windows,
com o Revit aberto.

## 1. Testes automáticos

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ci.ps1 -Integracao
```

Os testes de integração sobem o Revit 2027 dentro do processo. Os pontos abaixo dependem de suposições sobre a API que
não puderam ser conferidas fora do Revit. Se algum desses testes falhar, envie a mensagem do erro:

| Teste | O que ele confirma |
|---|---|
| `QuadroDeCargasNoRevit_Teste.Le_os_circuitos_do_quadro_com_potencia_esquema_e_tensao` | `ElectricalSystem.PhaseLabel` dá "A", "B" ou "C" para circuitos de 1 polo, e o sistema "120/208 Wye" é lido como 3F+N, 208 V, fase-terra 120 V |
| `QuadroDeCargasNoRevit_Teste.Monta_quadro_completo_com_fatores_informados` | Cargas por fase sem "circuitos sem fase identificada" |
| `DimensionamentoNoRevit_Teste.Condicoes_da_rodada_ficam_nos_circuitos…` | Extensible Storage: condições nos circuitos e num `DataStorage` |
| `AlimentadoresNoRevit_Teste.*` | Quadro ligado a outro quadro por `ElectricalSystem.Create` + `SelectPanel`; o alimentador é o circuito com `BaseEquipment` em outro quadro (o `GetElectricalSystems` do quadro também traz os circuitos dele); IB pela fase do circuito no quadro |
| `DemandaNoRevit_Teste.*` | Leitura de AMP_TipoCarga, AMP_Aparelho e AMP_PotenciaInstaladaVA dos pontos (VA convertido das unidades internas) |
| `VerificacaoNoRevit_Teste.*` | Pontos com conector de força nas categorias do Ampere; conferência das memórias |
| `PrevisaoNoRevit_Teste.*` | `SpatialElement.Area`/`Perimeter` dos Rooms em m²/m; pontos no cômodo pela busca dos locais; categorias num `DataStorage` próprio |

## 2. Roteiro manual

Use um projeto de teste com arquitetura vinculada ou com ambientes (Rooms/Spaces), quadro com sistema de distribuição
220/127 V (ou 380/220 V) e algumas tomadas, luminárias e um chuveiro.

1. **Injetar parâmetros.** O resumo deve mostrar os 35 parâmetros. Rodar de novo não deve criar nada.
2. **Classificar cargas.** Classifique tomadas (TUG), luminárias (Iluminação) e o chuveiro (TUE, 2F 220 V, aparelho
   Chuveiro). Reclassifique o chuveiro como TUG e de volta: o aparelho deve sumir e precisar ser escolhido de novo.
3. **Locais pelos ambientes.** O diálogo deve listar os ambientes com os pontos. Escolha um local para cada ambiente
   (banheiro = "Local com banheira ou chuveiro").
4. **Previsão de cargas.** O diálogo deve listar os nomes de ambiente. Escolha "Sala ou dormitório" para um quarto
   e "Banheiro" para o banheiro. O resumo deve dizer quantos cômodos atendem; um quarto de 12 m² com uma luminária de
   100 VA deve aparecer com "iluminação de 100 VA, abaixo dos 160 VA mínimos". Rode de novo: o diálogo deve abrir com
   as categorias escolhidas (guardadas no projeto), também depois de fechar e reabrir. Em `Documentos\Ampere\{projeto}\Previsao`
   devem sair `previsao.md` e `previsao.csv`. Com a arquitetura em vínculo, os Rooms do vínculo devem aparecer.
5. **Criar circuitos.** Os circuitos devem ser numerados no quadro escolhido.
6. **Dimensionar circuitos** (sem seleção).
   - O diálogo abre vazio na primeira vez. Nas vezes seguintes, abre com as condições guardadas no modelo; feche e
     reabra o projeto para conferir.
   - No diálogo, escolha o condutor (Prysmian Superastic Flex 450/750 V) e o eletroduto (Tigre Tigreflex amarelo). O
     cálculo deve ir até o eletroduto: seção, disjuntor, IDR, queda, `AMP_EletrodutoTipo` (ex.: DN 20) e a ocupação
     aparecem nos parâmetros do circuito. Sem condutor escolhido, o cálculo para no diâmetro do condutor.
   - Com isolação EPR e o Superastic (PVC), o cálculo deve parar dizendo que a isolação não confere.
   - Ponha um circuito no método D (enterrado) com a "Temperatura do solo" vazia: ele deve ficar com o problema "linha
     enterrada sem temperatura do solo". Com 20 °C no campo, a memória deve citar a Tabela 40 (solo) e a Tabela 44.
   - Num circuito no método E (ao ar livre), o cálculo deve terminar no IDR, sem eletroduto (`AMP_EletrodutoTipo`
     vazio), e a memória deve citar a coluna da Tabela 38.
   - Em `Documentos\Ampere\{projeto}\Circuitos` devem aparecer as memórias (JSON, MD e PDF), `circuitos.csv` e
     `materiais.csv`. Abra os CSV no Excel e confira os acentos. `circuitos.csv` traz as seções do neutro e do PE; num
     circuito de 50 mm², o PE deve ser 25 mm², também em `materiais.csv`.
7. **Decisões do projetista.** Num circuito, preencha `AMP_SecaoMinimaProjetistaMm2` = 6 e
   `AMP_JustificativaProjetista`, e rode o dimensionamento selecionando só esse circuito.
   - O resumo deve dizer "rodada só da seleção".
   - A seção deve ser 6 mm², e a memória deve citar a justificativa.
   - `circuitos.csv` não deve mudar; sai `circuitos-selecao.csv`.
8. **Montar quadro de cargas.** Informe os fatores de demanda.
   - O resumo deve mostrar "Fases (demanda): A … · B … · C … · desequilíbrio …%".
   - Se aparecer "fases dos circuitos no quadro não identificadas", o formato do `PhaseLabel` é outro. Anote o valor
     que aparece no parâmetro do circuito e envie.
   - O relatório do quadro deve ter as seções "Cargas por fase" e "Distribuição de fases sugerida". Mova no quadro do
     Revit um circuito que a sugestão indica e monte de novo: a corrente da fase deve mudar como a sugestão disse.
9. **Dimensionar alimentadores.** Ligue o quadro a um quadro geral (crie um circuito com o QD como carga) e informe o
   `AMP_ComprimentoRotaM` do alimentador.
   - Escolha "baixa tensão pela distribuidora".
   - O resumo deve trazer IB, seção, disjuntor e queda do alimentador. A memória (pasta `Alimentadores`) deve mostrar
     "fase de maior corrente" e o limite de queda que sobrou.
   - Mude a potência de um ponto e rode de novo: o alimentador deve ficar "quadro de cargas desatualizado", com os
     resultados apagados.
   - Com tomadas só numa fase, a memória deve trazer o passo "Corrente para a queda de tensão" (IΔV = IB + IN).
   - Um circuito reserva do Revit (Spare) no quadro não deve impedir o alimentador.
10. **Verificar projeto.**
   - Num projeto em dia, só devem aparecer informações ("Onde o cálculo para").
   - Mude o comprimento de um circuito: a memória deve aparecer como desatualizada. "Selecionar no modelo" deve
     selecionar o circuito.
   - Mude o `AMP_ComprimentoRotaM` do alimentador: ele deve aparecer em "Alimentadores desatualizados".
11. **Demanda da entrada.** Escolha "Residências". Com um TUE sem aparelho, o diálogo de erro deve oferecer selecionar o
    ponto no modelo. Com tudo classificado, o resumo traz D e as parcelas (a, b1…), e a memória vai para
    `Documentos\Ampere\{projeto}\Demanda`. Confira uma parcela à mão com a tabela da CT 04/18.
12. **Diagramas unifilares.** Deve sair uma vista de desenho por quadro, com os valores do dimensionamento.

## 3. O que conferir com a norma (pendências do usuário)

- **Tabela 42 (FCA):** as faixas são 9 a 11, 12 a 15, 16 a 19 e 20 ou mais? Ver `DATA_GAPS.md`.
- **GAP-004:** catálogos Prysmian (Superastic, Sintenax unipolar) e Tigre (Tigreflex, roscável) preenchidos. Falta o
  eletroduto soldável (NBR 15465) e, se usar, o de aço.
- **GAP-005:** fatores de demanda (a norma da distribuidora). Hoje são informados no "Montar quadro de cargas".
