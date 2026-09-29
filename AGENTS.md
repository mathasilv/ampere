# AGENTS.md — Ampere

Add-in Revit de instalações elétricas BT (NBR 5410) com memória de cálculo auditável.
Especificação-mestre: `E:\ProEletrica_Referencia\10_ESPECIFICACAO_MVP.md` (fonte da verdade).
Prompt de missão: `E:\ProEletrica_Referencia\11_PROMPT_OPUS.md`.

## Comandos

```bash
# Gate antes de qualquer commit: testes do núcleo + build nas TRÊS configurações
powershell -NoProfile -ExecutionPolicy Bypass -File ./ci.ps1
# + testes de integração dentro do Revit 2027 (obrigatório ao mexer no adapter Ampere.Revit)
powershell -NoProfile -ExecutionPolicy Bypass -File ./ci.ps1 -Integracao

# Passos individuais
dotnet build source/Ampere/Ampere.csproj -c Release.R2025
dotnet build source/Ampere/Ampere.csproj -c Release.R2026
dotnet build source/Ampere/Ampere.csproj -c Release.R2027
dotnet test tests/Ampere.Tests.Core                        # motor puro, fora do Revit (net8.0 + net10.0)
dotnet test tests/Ampere.Tests.Revit -c Release.R2027      # integração: sobe o Revit 2027 no processo

# Regenerar os golden files da memória e do relatório (só com justificativa; revise o diff dos .md antes do commit)
AMPERE_ATUALIZAR_REFERENCIAS=1 dotnet test --project tests/Ampere.Tests.Core --framework net10.0 --treenode-filter "/*/*/ReferenciasDeMemoria_Teste/*"
```

A pós-fixação `.RXXXX` na configuração é exigência do SDK Nice3point.Revit.Sdk.
Decisão do usuário (29/09/2026): desenvolvimento, testes de integração e testes manuais **só no Revit 2027**;
o build nas três configurações continua obrigatório (rede de segurança barata). API exclusiva do 2027 que
quebre R2025/R2026: perguntar antes de aposentar essas configurações.

## Arquitetura (respeitar rigidamente)

- `Ampere.Core` — motor de cálculo **puro**: proibido referenciar `Autodesk.Revit.*`.
  Entradas/saídas são records (DTO). 100% coberto por TDD.
  Fala com o Revit só por portas (interfaces, ex.: `IParametrosDoDocumento`).
- `Ampere.Revit` — adapters das portas do Core sobre a API do Revit, sem UI e sem deploy.
  Coberto por `Ampere.Tests.Revit` (Nice3point.TUnit.Revit, dentro do Revit real).
- `Ampere` (add-in; na especificação, `Ampere.AddIn`) — ribbon, comandos, WPF/MVVM, `ExternalEvent`.
  Só orquestra: regra no Core, API do Revit no `Ampere.Revit`.
- `data/` — JSONs UTF-8 com `$meta` (fonte + versão da norma + data). Perfis de norma em
  `data/normas/<norma>/<ano>/perfil.json`.
- GUIDs `AMP_*`: **congelados** — fonte versionada `data/parametros/parametros_compartilhados_ampere.json`
  (cópia do `10_Parametros_Compartilhados_Ampere.json`), guardada por `GuidsCongelados_Teste`.
  Nunca alterar, nunca usar GUIDs de terceiros.

## Regras de domínio

- Valor normativo sem fonte oficial = `TODO_NORMA` no código + pendência em `data/DATA_GAPS.md`
  + `[Property("Fonte", "TODO_NORMA")]` no teste.
  Nunca inventar capacidade de condução, fator de demanda ou limite de norma.
- Memória de cálculo determinística: mesmas entras ⇒ mesmo hash. Sem timestamps no cálculo.
- Siglas: IB, IZ, In, FCA, FCT, TUG, TUE, IDR, DPS (NBR 5410).

## Regras de código Revit (desempenho)

- Uma `Transaction` por lote — nunca por elemento.
- `FilteredElementCollector` sempre com filtro de categoria.
- Nunca chamar `doc.Regenerate()` em loop.
- Orçamento: 400 pontos dimensionados em < 5 s.

## Armadilhas conhecidas

- Encoding: todos os arquivos novos em UTF-8 (os `.DAT` do produto concorrente têm encodings
  mistos UTF-8/cp1252 — se for ler, detectar por arquivo).
- API Revit incerta: verificar em revitapidocs.com e isolar atrás de interface no adapter.
- `E:\pro_elet` (instalação do concorrente): **somente leitura de estudo; nunca modificar.**
- `DeployAddin=true`: todo build do add-in copia para `%AppData%\Autodesk\Revit\Addins\<ano>` e falha
  se o Revit estiver aberto com a DLL em uso. O `ci.ps1` compila com `-p:DeployAddin=false`.
- `dotnet sln add` mapeia `Release.R*` → `Debug` em projetos novos: corrigir para `Release` no `.sln`.
- Revit fora de `C:\Program Files\Autodesk`: defina `RevitInstallDir` (testes) e `StartProgram` (F5) em
  `*.csproj.user` git-ignorados, ou a variável de ambiente `RevitInstallDir`.
- Projetos WPF perdem `System.IO` dos usings implícitos; `Autodesk.Revit.UI` nunca é implícito.
  `BindingMap` não tem indexador C#: use `get_Item(definicao)`.
- Teste de integração novo: declarar `[DependsOn(typeof(DesempenhoDaInjecao_Teste), ProceedOnFailure = true)]` —
  o teste de desempenho precisa medir a primeira injeção da sessão.
- Cenários de circuito usam o template elétrico PTB da Autodesk (`RevitTemplateEletrico`): famílias hospedadas em
  face e quadro sem sistema de distribuição (o `CenarioEletrico` define 120/208 Y; sem isso, `SelectPanel` falha).
- Números digitados: só o separador decimal da cultura; separador de milhar é recusado (`NumeroDigitado`).
- C# 14: `array.Reverse()` resolve para `Span.Reverse` (in-place, `void`); use `Enumerable.Reverse(array)`.
- Commit com mensagem de várias linhas: pelo Bash com heredoc (`git commit -F - <<'EOF'`); no PowerShell o
  here-string vira argumento e o git o lê como caminho.
- Literal com escape `\uXXXX` (ex.: JSON canônico fixado em teste): as ferramentas de escrita de arquivo
  decodificam o escape; confira o arquivo gravado e, se preciso, ajuste por script.
- `JsonSerializerContext` (leitura gerada em compilação): o nome da classe precisa ser único no assembly
  mesmo em namespaces diferentes — nomes repetidos derrubam o gerador de todos os contextos (CS8785).
- Golden files (`tests/Ampere.Tests.Core/Relatorios/Referencias`): comparados byte a byte, com LF forçado no
  `.gitattributes`. Regenere num framework só (os dois processos de teste escreveriam o mesmo arquivo ao mesmo tempo) e
  depois rode a suíte normal: net8.0 e net10.0 precisam gerar os mesmos bytes.
- Parâmetro AMP_* que o dimensionamento grava (ex.: `AMP_IDR_SensibilidadeMa`) não pode ser lido de volta como
  entrada: na rodada seguinte o valor calculado viraria "decisão do projetista". Decisão manual precisa de fonte
  própria (ex.: `DecisaoDeIdr` do Core, alimentada por um campo que o motor nunca escreve).

## Commits

`tipo(escopo): descrição` — ex.: `feat(core): corrente de projeto IB monofásico`.
Commits pequenos; build 3× verde + testes antes de cada commit.
