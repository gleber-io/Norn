<#
.SYNOPSIS
    Orquestra a matriz completa da campanha (Fase 12, tarefa 3): 4 cenarios x 3 bracos x
    -Repetitions repeticoes, em blocos aleatorizados (Master Plan Sec3: "bloco = um cenario com os
    tres bracos, ordem dos bracos sorteada dentro do bloco, ordem dos blocos sorteada").

.DESCRIPTION
    Gera o manifesto de execucao (docs/experiments/campaign-manifest.csv) antes de rodar qualquer
    coisa -- se o manifesto ja existir, reaproveita a ordem gravada em vez de sortear de novo, para
    que -StartFromRunOrder consiga retomar um lote interrompido sem reordenar o que ja rodou.
    Confirma o checklist de higiene da maquina (tarefa 3a) uma unica vez para o lote inteiro, e
    chama run-experiment.ps1 para cada linha do manifesto a partir de -StartFromRunOrder.

.PARAMETER Repetitions
    Repeticoes por (cenario x braco) -- 5 fecha as 60 execucoes do DoD da Fase 12.

.PARAMETER MasterSeed
    Seed do sorteio de blocos -- gravado em experiment_runs.randomization_seed (Sec3).

.PARAMETER StartFromRunOrder
    Retoma a partir desta posicao do manifesto -- 1 para uma campanha nova.

.PARAMETER BackupAfterEachBlock
    Roda dump-knowledge.ps1 ao fim de cada bloco (3 execucoes) -- tarefa 3b, "o plano nao tinha
    nenhum". Default ligado: perder o Postgres custa refazer a campanha inteira.
#>
param(
    [int]$Repetitions = 5,
    [Parameter(Mandatory)] [int]$MasterSeed,
    [int]$StartFromRunOrder = 1,
    [bool]$BackupAfterEachBlock = $true,
    [int]$DurationMinutes = 25,
    [int]$InjectionPhaseSeconds = 300
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "docs/experiments/campaign-manifest.csv"

function Step($message) {
    Write-Host ""
    Write-Host "==> $message" -ForegroundColor Cyan
}

# --- Manifesto: gera na primeira vez, reaproveita depois (retomada de lote) ----------------
if (-not (Test-Path $manifestPath)) {
    Step "Gerando manifesto da campanha (seed=$MasterSeed, $Repetitions repeticoes)"
    Get-Random -SetSeed $MasterSeed | Out-Null

    $scenarios = @("F1", "F2", "F3", "F5")
    $blocks = @()
    foreach ($scenario in $scenarios) {
        for ($repetition = 1; $repetition -le $Repetitions; $repetition++) {
            $blocks += [pscustomobject]@{ Scenario = $scenario; Repetition = $repetition }
        }
    }

    # Ordem dos blocos sorteada -- nunca braco por braco, nunca cenario por cenario (Sec3).
    $shuffledBlocks = $blocks | Sort-Object { Get-Random }

    $runOrder = 1
    $manifest = foreach ($block in $shuffledBlocks) {
        # Ordem dos bracos sorteada dentro do bloco.
        $arms = @("A", "B", "C") | Sort-Object { Get-Random }
        foreach ($arm in $arms) {
            [pscustomobject]@{
                RunOrder          = $runOrder
                Scenario          = $block.Scenario
                Arm               = $arm
                Repetition        = $block.Repetition
                RandomizationSeed = $MasterSeed
            }
            $runOrder++
        }
    }

    New-Item -ItemType Directory -Force -Path (Split-Path $manifestPath) | Out-Null
    $manifest | Export-Csv -Path $manifestPath -NoTypeInformation
    Write-Host "Manifesto gravado em $manifestPath ($($manifest.Count) execucoes)."
} else {
    Step "Manifesto existente reaproveitado: $manifestPath"
}

$manifestRows = Import-Csv $manifestPath | Sort-Object { [int]$_.RunOrder }
$pendingRows = $manifestRows | Where-Object { [int]$_.RunOrder -ge $StartFromRunOrder }

Write-Host "$($pendingRows.Count) execucoes pendentes, a partir de run_order=$StartFromRunOrder."

# --- Checklist de higiene confirmado uma unica vez para o lote inteiro ---------------------
Write-Host ""
Write-Host "Confirme antes de iniciar um lote desacompanhado (tarefa 3a):"
Write-Host "  [ ] Windows Update pausado pelo periodo do lote"
Write-Host "  [ ] Suspensao/hibernacao desativadas (inclusive com a tampa fechada)"
Write-Host "  [ ] Atualizacao automatica do Docker Desktop desligada"
Write-Host "  [ ] Plano de alto desempenho, na tomada, IDE e navegador fechados"
$confirmation = Read-Host "Os quatro itens acima estao confirmados para o lote inteiro? (s/n)"
if ($confirmation -ne "s") { throw "Lote cancelado -- checklist da tarefa 3a nao confirmado." }

# --- Execucao sequencial do lote ------------------------------------------------------------
$blockCounter = 0
foreach ($row in $pendingRows) {
    Step "Execucao run_order=$($row.RunOrder): $($row.Scenario)/$($row.Arm), repeticao $($row.Repetition)"

    # run-experiment.ps1 sinaliza falha por excecao terminante (throw), nunca por `exit <code>` --
    # `$LASTEXITCODE` so reflete o ultimo comando nativo (kubectl/dotnet) rodado dentro dele, entao
    # checa-lo aqui nunca pegaria a excecao de verdade (ela já teria propagado e derrubado o lote
    # inteiro antes desta linha rodar). O catch e o unico jeito confiavel de dar a mensagem de
    # retomada em vez de um stack trace cru.
    try {
        & (Join-Path $PSScriptRoot "run-experiment.ps1") `
            -Scenario $row.Scenario `
            -Arm $row.Arm `
            -Repetition ([int]$row.Repetition) `
            -RunOrder ([int]$row.RunOrder) `
            -RandomizationSeed ([int]$row.RandomizationSeed) `
            -DurationMinutes $DurationMinutes `
            -InjectionPhaseSeconds $InjectionPhaseSeconds `
            -SkipPreflightConfirmation
    } catch {
        throw "run-experiment.ps1 falhou em run_order=$($row.RunOrder) -- lote interrompido. Retome com -StartFromRunOrder $($row.RunOrder). Causa: $_"
    }

    $blockCounter++
    if ($BackupAfterEachBlock -and ($blockCounter % 3 -eq 0)) {
        Step "Fim de bloco -- backup do Knowledge (tarefa 3b)"
        & (Join-Path $PSScriptRoot "dump-knowledge.ps1")
    }
}

Step "Lote concluido: $($pendingRows.Count) execucoes."
