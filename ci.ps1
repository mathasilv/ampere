<#
.SYNOPSIS
    CI local do Ampere: testes do núcleo + build do add-in nas três configurações Revit.

.DESCRIPTION
    Gate obrigatório antes de cada commit (AGENTS.md). Para no primeiro passo que falhar e
    devolve o código de saída dele.

    O build usa -p:DeployAddin=false: compila exatamente o mesmo código, mas não copia o add-in
    para %AppData%\Autodesk\Revit\Addins — sem efeito colateral na máquina e sem falhar quando
    o Revit está aberto com a DLL do add-in em uso.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\ci.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

$resultados = New-Object System.Collections.Generic.List[object]

function Write-Resumo {
    Write-Host ''
    Write-Host ($resultados | Format-Table -AutoSize | Out-String).TrimEnd()
}

function Invoke-Passo {
    param(
        [Parameter(Mandatory)] [string] $Nome,
        [Parameter(Mandatory)] [string[]] $Argumentos
    )

    Write-Host ''
    Write-Host "==> $Nome" -ForegroundColor Cyan
    Write-Host "    dotnet $($Argumentos -join ' ')" -ForegroundColor DarkGray

    $cronometro = [Diagnostics.Stopwatch]::StartNew()
    & dotnet @Argumentos
    $codigo = $LASTEXITCODE
    $cronometro.Stop()

    $situacao = 'OK'
    if ($codigo -ne 0) { $situacao = "FALHOU ($codigo)" }
    $resultados.Add([pscustomobject]@{
        Passo     = $Nome
        Resultado = $situacao
        Tempo     = '{0:N1} s' -f $cronometro.Elapsed.TotalSeconds
    })

    if ($codigo -ne 0) {
        Write-Resumo
        Write-Host ''
        Write-Host "CI VERMELHO: '$Nome' falhou (código $codigo)." -ForegroundColor Red
        exit $codigo
    }
}

Write-Host "Ampere CI local — .NET SDK $(& dotnet --version)"

Invoke-Passo -Nome 'Testes Ampere.Tests.Core' -Argumentos @('test', 'tests/Ampere.Tests.Core', '-c', 'Release')

foreach ($configuracao in 'Release.R2025', 'Release.R2026', 'Release.R2027') {
    Invoke-Passo -Nome "Build $configuracao" -Argumentos @(
        'build', 'source/Ampere/Ampere.csproj', '-c', $configuracao, '-p:DeployAddin=false', '-nologo')
}

Write-Resumo
Write-Host ''
Write-Host 'CI VERDE: testes e build tripla OK.' -ForegroundColor Green
exit 0
