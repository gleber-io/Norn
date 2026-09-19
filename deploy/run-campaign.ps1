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

.PARAMETER SkipWorkerManagement
    Nao sobe nem derruba um Norn.Worker -- use quando ja houver um rodando por fora (ex.: sessao de
    depuracao). Sem isso, o campaign sobe um Worker proprio, unico para o lote inteiro inteiro (Fase
    12, achado ao vivo: reiniciar o Worker a cada execucao perderia o warmup do detector, ~150s de
    historico -- o Worker precisa sobreviver as 60 execucoes, so o estado no Redis muda por execucao).

.PARAMETER SkipApiManagement
    Nao sobe nem derruba a Norn.API -- use quando ja houver uma rodando por fora. Sem isso, o
    campaign sobe a API sozinha, na porta 5080 (a 5000 default costuma estar ocupada pelo
    wslrelay.exe do WSL2). Achado ao escrever o runbook da campanha (docs/experiments/plano-campanha.md):
    sem a API no ar, todo screenshot do dashboard em tools/PanelCapture/capture.js falha em
    silencio (best-effort por design) -- as 60 execucoes rodariam sem nenhuma figura do dashboard
    pro TCC, e ninguem perceberia ate revisar os resultados depois de ~25h.
#>
param(
    [int]$Repetitions = 5,
    [Parameter(Mandatory)] [int]$MasterSeed,
    [int]$StartFromRunOrder = 1,
    [bool]$BackupAfterEachBlock = $true,
    [int]$DurationMinutes = 25,
    [int]$InjectionPhaseSeconds = 300,
    [switch]$SkipWorkerManagement,
    [switch]$SkipApiManagement
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

# --- Norn.Worker: um so processo para o lote inteiro (achado ao vivo, piloto F1/C) ---------
$workerProcess = $null
if (-not $SkipWorkerManagement) {
    $existingWorker = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
        Where-Object { $_.CommandLine -like "*Norn.Worker*" }
    if ($existingWorker) {
        throw "Ja existe um processo Norn.Worker rodando (PID $($existingWorker.ProcessId)) -- pare-o antes, ou rode com -SkipWorkerManagement se for intencional. Dois Workers contra o mesmo Redis/Postgres disputariam a mesma decisao (ADR-04)."
    }

    Step "Subindo Norn.Worker (unico para os $($pendingRows.Count) execucoes deste lote)"
    $workerLogPath = "C:\git\norn-results\logs\worker-campaign.log"
    New-Item -ItemType Directory -Force -Path (Split-Path $workerLogPath) | Out-Null
    $workerProcess = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "src/Platform/Norn.Worker" `
        -WorkingDirectory $repoRoot -RedirectStandardOutput $workerLogPath -RedirectStandardError "$workerLogPath.err" `
        -WindowStyle Hidden -PassThru

    Step "Aguardando warmup do detector (~150s, historico minimo antes do primeiro cenario)"
    Start-Sleep -Seconds 150

    if ($workerProcess.HasExited) {
        throw "Norn.Worker encerrou sozinho durante o warmup -- veja $workerLogPath antes de tentar de novo."
    }
}

# --- Norn.API: um so processo para o lote inteiro, so pros screenshots do dashboard ---------
# Diferente do Worker, nao decide nada (ADR-04 nao se aplica) -- uma instancia ja rodando por
# fora nao e um risco de correcao, so reaproveitada em vez de recusada.
$apiProcess = $null
if (-not $SkipApiManagement) {
    $existingApi = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
        Where-Object { $_.CommandLine -like "*Norn.API*" }
    if ($existingApi) {
        Write-Host "Norn.API ja rodando (PID $($existingApi.ProcessId)) -- reaproveitando, nao subindo outra."
    } else {
        Step "Subindo Norn.API (unica para o lote inteiro, so pros screenshots do dashboard)"
        $apiLogPath = "C:\git\norn-results\logs\api-campaign.log"
        New-Item -ItemType Directory -Force -Path (Split-Path $apiLogPath) | Out-Null
        $apiProcess = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "src/Platform/Norn.API", "--urls", "http://localhost:5080" `
            -WorkingDirectory $repoRoot -RedirectStandardOutput $apiLogPath -RedirectStandardError "$apiLogPath.err" `
            -WindowStyle Hidden -PassThru

        $apiReady = $false
        for ($attempt = 1; $attempt -le 30 -and -not $apiReady; $attempt++) {
            if ($apiProcess.HasExited) {
                throw "Norn.API encerrou sozinha ao subir -- veja $apiLogPath antes de tentar de novo."
            }
            try {
                Invoke-RestMethod -Method Get -Uri "http://localhost:5080/health/ready" -TimeoutSec 2 -ErrorAction Stop | Out-Null
                $apiReady = $true
            } catch {
                Start-Sleep -Seconds 2
            }
        }
        if (-not $apiReady) {
            # Nao bloqueia o lote -- so os screenshots do dashboard ficam sem efeito (best-effort
            # por design em capture.js). Perder a API nao pode custar as 60 execucoes de dado real.
            Write-Warning "Norn.API nao respondeu /health/ready em 60s -- screenshots do dashboard vao falhar (nao bloqueante). Veja $apiLogPath."
        }
    }
}

try {
    # --- Execucao sequencial do lote --------------------------------------------------------
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

            # Captura dos paineis do Grafana (visao mais longa que o Prometheus por execucao ja
            # cobre) -- best-effort, mesmo motivo do capture da run-experiment.ps1: nao pode
            # derrubar um lote de 60 execucoes por causa de um screenshot.
            try {
                node (Join-Path $repoRoot "tools/PanelCapture/capture.js") `
                    --out "C:\git\norn-results\screenshots\blocks" `
                    --mode block `
                    --label "bloco-$blockCounter"
            } catch {
                Write-Warning "Captura de paineis do bloco falhou (nao bloqueante): $_"
            }
        }
    }

    Step "Lote concluido: $($pendingRows.Count) execucoes."

    # --- Consolidacao final: copia os artefatos da campanha para fora do repositorio ---------
    # C:\git\norn-results e o unico lugar que reune tudo que o TCC precisa depois (logs, CSVs,
    # screenshots, backups do Postgres) -- os CSVs continuam vivendo em tools/analysis/data/
    # tambem (fonte de verdade, versionada no git quando a campanha fechar); isto e so uma copia.
    # try/catch (achado do code-reviewer antes do commit): sem isso, uma falha aqui (permissao,
    # disco) e erro de cmdlet -- respeita o $ErrorActionPreference = "Stop" do topo do script,
    # ao contrario de node/ollama -- e reportaria falha no fim de um lote de ~25h que na pratica
    # terminou com sucesso, so por causa de uma copia de conveniencia.
    Step "Copiando CSVs da campanha para C:\git\norn-results\campaign-data"
    try {
        $resultsDataDir = "C:\git\norn-results\campaign-data"
        New-Item -ItemType Directory -Force -Path $resultsDataDir | Out-Null
        foreach ($csv in @("labeled-runs.csv", "discarded-runs.csv")) {
            $source = Join-Path $repoRoot "tools/analysis/data/$csv"
            if (Test-Path $source) { Copy-Item $source $resultsDataDir -Force }
        }
        if (Test-Path $manifestPath) { Copy-Item $manifestPath $resultsDataDir -Force }
        $loadReportsSource = Join-Path $repoRoot "tools/analysis/data/load-reports"
        if (Test-Path $loadReportsSource) {
            Copy-Item $loadReportsSource (Join-Path $resultsDataDir "load-reports") -Recurse -Force
        }
    } catch {
        Write-Warning "Copia final dos CSVs falhou (nao bloqueante -- originais continuam em tools/analysis/data/): $_"
    }
}
finally {
    if ($workerProcess -and -not $workerProcess.HasExited) {
        Step "Encerrando Norn.Worker do lote"
        Stop-Process -Id $workerProcess.Id -Force
    }
    if ($apiProcess -and -not $apiProcess.HasExited) {
        Step "Encerrando Norn.API do lote"
        Stop-Process -Id $apiProcess.Id -Force
    }
}
