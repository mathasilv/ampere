# AGENTS.md — Ampere

Add-in Revit de instalações elétricas BT (NBR 5410) com memória de cálculo auditável.
Especificação-mestre: `E:\ProEletrica_Referencia\10_ESPECIFICACAO_MVP.md` (fonte da verdade).
Prompt de missão: `E:\ProEletrica_Referencia\11_PROMPT_OPUS.md`.

## Comandos

```bash
# Gate antes de qualquer commit: testes do núcleo + build nas TRÊS configurações
powershell -NoProfile -ExecutionPolicy Bypass -File ./ci.ps1

# Passos individuais
dotnet build source/Ampere/Ampere.csproj -c Release.R2025
dotnet build source/Ampere/Ampere.csproj -c Release.R2026
dotnet build source/Ampere/Ampere.csproj -c Release.R2027
dotnet test tests/Ampere.Tests.Core   # motor puro, fora do Revit (TUnit, net8.0 + net10.0)
```

A pós-fixação `.RXXXX` na configuração é exigência do SDK Nice3point.Revit.Sdk.

## Arquitetura (respeitar rigidamente)

- `Ampere.Core` — motor de cálculo **puro**: proibido referenciar `Autodesk.Revit.*`.
  Entradas/saídas são records (DTO). 100% coberto por TDD.
- `Ampere.AddIn` — adapter Revit: ribbon WPF/MVVM, `ElectricalSystem`, `PanelScheduleView`,
  injeção de parâmetros compartilhados, `ExternalEvent` para operações longas.
- `data/` — JSONs UTF-8 com `$meta` (fonte + versão da norma + data). Perfis de norma em
  `data/normas/<norma>/<ano>/perfil.json`.
- GUIDs `AMP_*`: **congelados** (`10_Parametros_Compartilhados_Ampere.json`). Nunca alterar,
  nunca usar GUIDs de terceiros.

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

## Commits

`tipo(escopo): descrição` — ex.: `feat(core): corrente de projeto IB monofásico`.
Commits pequenos; build 3× verde + testes antes de cada commit.
