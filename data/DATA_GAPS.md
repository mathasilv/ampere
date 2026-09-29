# Pendências de dados normativos (DATA_GAPS)

Todo valor ou referência de norma usado pelo Ampere precisa de fonte oficial citada no perfil
(`data/normas/<norma>/<ano>/perfil.json`). Enquanto o texto oficial não estiver disponível, o código
marca `TODO_NORMA` e a pendência é registrada aqui. Um cálculo errado silencioso é o pior defeito
possível deste produto.

Testes que dependem de fonte pendente levam `[Property("Fonte", "TODO_NORMA")]` e são listados com:

```bash
dotnet test tests/Ampere.Tests.Core --list-tests --treenode-filter "/*/*/*/*[Fonte=TODO_NORMA]"
```

## Abertas (em uso no código)

| ID | Pendência | Onde | Para fechar |
|---|---|---|---|
| GAP-001 | Item da NBR 5410:2004 que define a corrente de projeto I<sub>B</sub> e a tensão a adotar por esquema (F+N, 2F, 3F) | `CalculoDeCorrente.CorrenteDeProjetoMonofasica`; `CalculoDeCorrente_Teste` (valores de brinquedo) | Conferir no texto oficial, citar o item no perfil e trocar os valores de brinquedo por casos verificáveis |

## Previstas (F1.3/F1.4 — ainda sem código)

Valores citados na especificação (§3) que só entram no produto com fonte no perfil:

- Capacidade de condução por método de instalação (tabelas citadas como 36/37) e fatores de correção
  FCT/FCA (citada a Tabela 40).
- Limites de queda de tensão — a especificação diz "4% instalação total, 7% circuito terminal".
  **Conferir se não está invertido:** há indício de que o limite menor se aplica aos circuitos terminais.
- Taxa máxima de ocupação de eletroduto — a especificação diz "40% ≥ 2 condutores; 31%/53% nos casos
  da norma". **Conferir a que número de condutores cada taxa se aplica.**
- Fatores de demanda por tipo de carga (quadro de cargas, F1.4).
- Referência do exemplo de memória da especificação §6.4 (`"ref": "6410.4.2.1.2"`): o formato não
  corresponde a um item conhecido da norma; não reutilizar sem conferir.
