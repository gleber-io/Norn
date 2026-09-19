<#
.SYNOPSIS
    Executa uma única célula da campanha (Fase 12, tarefa 2): reset -> warmup -> carga -> injeção
    -> observação -> coleta -> teardown, para um (cenário, braço, repetição).

.DESCRIPTION
    Orçamento de tempo fixo (§3): em vez de o script detectar o onset ao vivo, ele roda por
    -DurationMinutes de relógio (warmup + rampa + janela de 10 min + margem) e desliga tudo ao fim.
    O Norn.Labeler, chamado depois do teardown, é o único lugar que implementa onset/recuperação —
    consulta todo o intervalo de Prometheus da execução. Decisão registrada na sessão de
    implementação da Fase 12: mantém a detecção em um único lugar (KISS/DRY), ao custo de alguns
    minutos de margem por execução. As 3 execuções piloto exigidas pelo DoD da Fase 12 são o que
    calibra -DurationMinutes de verdade — o valor default aqui é uma estimativa, não medição.

    F1/F5 miram Catalog.API (NodePort direto), F2 mira Order.API (NodePort direto), F3 mira
    Payment.API — sem NodePort (D9), então o script abre um `kubectl port-forward` próprio para
    esse cenário e o fecha no teardown.

    container_oom_events_total não é observável neste ambiente (docs/metrics-matrix.md) — para F1,
    o instante do OOMKilled é lido de `status.containerStatuses[].lastState.terminated` via kubectl
    logo após o teardown, enquanto o campo ainda está fresco, e repassado ao Labeler.

    PRÉ-REQUISITO NÃO AUTOMATIZADO AQUI (achado ao vivo, piloto F1/C): este script reseta o braço
    no Redis, mas não sobe um `Norn.Worker` — sem um rodando, nada decide nada, e o cenário fica sem
    cura nenhuma reagindo (`run-campaign.ps1` já sobe um Worker próprio para o lote inteiro; para
    rodar este script isolado, suba um `Norn.Worker` manualmente antes e deixe-o com warmup
    completo, ~150s de histórico).

.PARAMETER Scenario
    F1 | F2 | F3 | F5.

.PARAMETER Arm
    A | B | C.

.PARAMETER Repetition
    1-5, identifica a célula (§3).

.PARAMETER RunOrder
    Posição desta execução na sequência aleatorizada da campanha (gravado em experiment_runs).

.PARAMETER RandomizationSeed
    Seed do sorteio de blocos que produziu esta ordem (§3) — mesmo valor em todas as execuções da mesma campanha.

.PARAMETER DurationMinutes
    Orçamento total de relógio por execução. Default 25 (5 warmup + ~10 rampa/margem + 10 janela) — recalibrar após os pilotos.

.PARAMETER InjectionPhaseSeconds
    Instante, em segundos desde o início, em que a injeção de caos dispara — sempre o mesmo entre execuções (§3, "mesma fase do ciclo senoidal"). Default 300 (fim do warmup de 5 min).

.PARAMETER SkipPreflightConfirmation
    Pula a confirmação interativa do checklist da tarefa 3a — usado pelo run-campaign.ps1, que já confirmou uma vez para o lote inteiro.
#>
param(
    [Parameter(Mandatory)] [ValidateSet("F1", "F2", "F3", "F5")] [string]$Scenario,
    [Parameter(Mandatory)] [ValidateSet("A", "B", "C")] [string]$Arm,
    [Parameter(Mandatory)] [int]$Repetition,
    [Parameter(Mandatory)] [int]$RunOrder,
    [Parameter(Mandatory)] [int]$RandomizationSeed,
    [int]$ChaosSeed = (Get-Random -Maximum 1000000),
    [int]$LoadSeed = (Get-Random -Maximum 1000000),
    [double]$TargetBaseRps = 2,
    [double]$TargetPeakRps = 20,
    [int]$DurationMinutes = 25,
    [int]$InjectionPhaseSeconds = 300,
    [switch]$Forecast,
    [int]$HorizonMinutes = 5,
    [switch]$SkipPreflightConfirmation
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {

function Step($message) {
    Write-Host ""
    Write-Host "==> $message" -ForegroundColor Cyan
}

# --- Alvo por cenário (§3) --------------------------------------------------------------
$targets = @{
    F1 = @{ Deployment = "catalog-api"; AdminBaseUrl = "http://localhost:8080"; ExportedJob = "Norn.Shop.Catalog.API" }
    F2 = @{ Deployment = "order-api";   AdminBaseUrl = "http://localhost:8081"; ExportedJob = "Norn.Shop.Order.API" }
    F3 = @{ Deployment = "payment-api"; AdminBaseUrl = "http://localhost:8082"; ExportedJob = "Norn.Shop.Payment.API" }
    F5 = @{ Deployment = "catalog-api"; AdminBaseUrl = "http://localhost:8080"; ExportedJob = "Norn.Shop.Catalog.API" }
}
$target = $targets[$Scenario]
$targetRps = ($TargetBaseRps + $TargetPeakRps) / 2

# --- Tarefa 3a: higiene da máquina antes de um lote desacompanhado -----------------------
Step "Checklist de higiene da máquina (tarefa 3a)"
$powerScheme = (powercfg /getactivescheme)
$onBattery = $false
try {
    $battery = Get-CimInstance -ClassName Win32_Battery -ErrorAction SilentlyContinue
    if ($battery -and $battery.BatteryStatus -ne 2) { $onBattery = $true }
} catch { }

Write-Host "Plano de energia ativo: $powerScheme"
Write-Host "Na tomada: $(-not $onBattery)"
Write-Host "Confirme manualmente antes de prosseguir:"
Write-Host "  [ ] Windows Update pausado pelo período do lote"
Write-Host "  [ ] Suspensão/hibernação desativadas (inclusive com a tampa fechada)"
Write-Host "  [ ] Atualização automática do Docker Desktop desligada"
Write-Host "  [ ] Plano de alto desempenho, IDE e navegador fechados"

if ($onBattery) {
    throw "Máquina na bateria — recusando iniciar (tarefa 3a). Conecte na tomada."
}

if (-not $SkipPreflightConfirmation) {
    $confirmation = Read-Host "Os quatro itens acima estão confirmados? (s/n)"
    if ($confirmation -ne "s") { throw "Execução cancelada — checklist da tarefa 3a não confirmado." }
}

# Achado ao vivo (piloto F3/B): a primeira chamada real ao Ollama depois de um restart da máquina
# carrega o modelo na GPU (~15s) antes de sequer começar a avaliar o prompt — mais que o
# OllamaTimeout de 10s (Norn.Worker/appsettings.json) configurado na chamada real. Sem aquecer
# antes, a primeira (e só a primeira) decisão do braço B sempre cai no timeout e cai para o
# RuleEngine via fallback — não é falha de lógica, é a janela fria nunca terminando dentro do
# timeout. Só vale para o braço B (RuleEngine, braço C, nunca chama o Ollama) e é best-effort —
# se falhar, o próprio fallback do §5.5 já cobre o caso, então não bloqueia a execução.
if ($Arm -eq "B") {
    Step "Aquecendo o Ollama (braço B) — carrega norn-qwen na GPU antes do laço real"
    try {
        $warmupOutput = ollama run norn-qwen "Responda apenas com a palavra ok." 2>&1
        # $ErrorActionPreference = "Stop" não converte código de saída de processo nativo em
        # exceção — sem checar $LASTEXITCODE, uma falha interna do `ollama run` (não "comando não
        # encontrado") passaria batido como "Ollama aquecido." (achado da revisão de código).
        if ($LASTEXITCODE -ne 0) { throw "ollama run saiu com código $LASTEXITCODE`: $warmupOutput" }
        Write-Host "Ollama aquecido."
    } catch {
        Write-Warning "Aquecimento do Ollama falhou (não bloqueante) — a primeira decisão real do braço B pode cair no fallback por timeout: $_"
    }
}

# --- Reset de estado (tarefa 2a) ---------------------------------------------------------
Step "Reset: flags em false, modo/plannerBackend conforme o braço $Arm"
$resetOutput = dotnet run --project tools/Norn.Labeler -- reset --arm $Arm 2>&1
Write-Host $resetOutput
if ($LASTEXITCODE -ne 0) { throw "Norn.Labeler reset falhou" }

$expectedMode = if ($Arm -eq "A") { "Observe" } else { "Active" }
$confirmedMode = ($resetOutput | Select-String "^mode=(.+)$").Matches.Groups[1].Value
if ($confirmedMode -ne $expectedMode) {
    throw "Reset não confirmou o modo esperado: pedido=$expectedMode, lido=$confirmedMode. Execução descartada, refazer (§3, tarefa 2a)."
}

Step "Reset: réplicas de volta ao baseline (1 por serviço do Shop)"
foreach ($deployment in @("catalog-api", "order-api", "payment-api")) {
    kubectl scale "deployment/$deployment" -n norn-shop --replicas=1 | Out-Null
}
foreach ($deployment in @("catalog-api", "order-api", "payment-api")) {
    kubectl rollout status "deployment/$deployment" -n norn-shop --timeout=120s | Out-Null
}

# --- Registro da execução (tarefa 1a) -----------------------------------------------------
$runId = [guid]::NewGuid()
$startedAtUtc = [DateTimeOffset]::UtcNow
$gitCommitSha = (git rev-parse HEAD).Trim()
$llmModelDigest = $null
try {
    $modelfile = ollama show norn-qwen --modelfile 2>$null
    $digestLine = $modelfile | Select-String "FROM.*sha256:([0-9a-f]+)"
    if ($digestLine) { $llmModelDigest = $digestLine.Matches.Groups[1].Value }
} catch { }

Step "Registrando experiment_runs ($runId)"
$initArgs = @(
    "run", "--project", "tools/Norn.Labeler", "--",
    "init-run",
    "--run-id", $runId,
    "--scenario", $Scenario,
    "--arm", $Arm,
    "--repetition", $Repetition,
    "--run-order", $RunOrder,
    "--randomization-seed", $RandomizationSeed,
    "--target-rps", $targetRps,
    "--chaos-seed", $ChaosSeed,
    "--load-seed", $LoadSeed,
    "--injection-phase", $InjectionPhaseSeconds,
    "--mode", $confirmedMode,
    "--forecast-enabled", $Forecast.IsPresent.ToString().ToLower(),
    "--started-at-utc", $startedAtUtc.ToString("o"),
    "--wsl-memory-gb", 8,
    "--wsl-processors", 10,
    "--git-commit-sha", $gitCommitSha
)
if ($Forecast) { $initArgs += @("--forecast-horizon-minutes", $HorizonMinutes) }
if ($llmModelDigest) { $initArgs += @("--llm-model-digest", $llmModelDigest) }
dotnet @initArgs
if ($LASTEXITCODE -ne 0) { throw "Norn.Labeler init-run falhou" }

# --- Carga + injeção -----------------------------------------------------------------------
$loadReportPath = Join-Path $repoRoot "tools/analysis/data/load-reports/$runId.csv"
New-Item -ItemType Directory -Force -Path (Split-Path $loadReportPath) | Out-Null

Step "Iniciando Norn.LoadGenerator (seed=$LoadSeed, $DurationMinutes min)"
$loadGenJob = Start-Job -ScriptBlock {
    param($repoRoot, $loadSeed, $durationMinutes, $baseRps, $peakRps, $reportPath)
    Set-Location $repoRoot
    dotnet run --project tools/Norn.LoadGenerator -- `
        --catalog-url "http://localhost:8080" `
        --order-url "http://localhost:8081" `
        --seed $loadSeed --duration-minutes $durationMinutes `
        --base-rps $baseRps --peak-rps $peakRps --out $reportPath
} -ArgumentList $repoRoot, $LoadSeed, $DurationMinutes, $TargetBaseRps, $TargetPeakRps, $loadReportPath

$portForwardJob = $null

# Achado ao vivo (piloto F1/C, 18/09/2026): sem este try/finally, qualquer falha entre ativar o
# caos e desativá-lo (inclusive um bug de sintaxe do PowerShell ao chamar kubectl) derrubava o
# script inteiro e deixava o caos rodando de verdade contra o pod, sem ninguém desligar. O caos só
# é ativado dentro deste bloco, e o finally roda sempre — sucesso ou exceção.
$chaosActivated = $false
$oomKilledAtUtc = "none"
$f5KillAtUtc = "none"
$cpuClockAvgMhz = $null
$observationEndUtc = $null
$injectionAtUtc = $null
try {
    if ($Scenario -eq "F3") {
        Step "Abrindo port-forward para Payment.API (sem NodePort, D9)"
        # Achado ao vivo (piloto F3/B): a causa real da primeira falha aqui não foi timing — foi a
        # porta errada. O Service `payment-api` expõe a porta 80 (`targetPort: 8080` é só o
        # container); `kubectl port-forward svc/payment-api <local>:8080` falha na hora com "Service
        # payment-api does not have a service port 8080" (confirmado ao vivo), nunca chegando a
        # abrir o túnel — daí "Impossível conectar-se ao servidor remoto" na ativação do caos, em
        # qualquer tempo de espera. Porta corrigida para 80 (a do Service, não a do container). Bloco
        # movido para dentro do try/finally que já existe (antes vivia fora dele).
        $portForwardJob = Start-Job -ScriptBlock {
            kubectl port-forward svc/payment-api 8082:80 -n norn-shop
        }

        $portForwardReady = $false
        for ($attempt = 1; $attempt -le 15 -and -not $portForwardReady; $attempt++) {
            try {
                Invoke-RestMethod -Method Get -Uri "$($target.AdminBaseUrl)/health/live" -TimeoutSec 2 -ErrorAction Stop | Out-Null
                $portForwardReady = $true
            } catch {
                Start-Sleep -Seconds 2
            }
        }
        if (-not $portForwardReady) {
            throw "Port-forward para Payment.API não respondeu em $(($attempt - 1) * 2)s — abortando antes de tentar ativar o caos (tarefa 3, F3)."
        }
    }

    Step "Aguardando o instante de injeção (fase $InjectionPhaseSeconds s do ciclo)"
    Start-Sleep -Seconds $InjectionPhaseSeconds

    # Capturado *antes* do POST — é o limite entre "alvo saudável" e "alvo sob caos" que o
    # Labeler usa pra medir a capacidade do gerador só na janela em que isso é mensurável de
    # verdade (achado ao vivo, 2º piloto F1/C — ver LoadReportReader.cs).
    $injectionAtUtc = [DateTimeOffset]::UtcNow

    Step "Ativando caos: $Scenario (seed=$ChaosSeed) em $($target.Deployment)"
    $activateBody = @{ scenarioId = $Scenario; seed = $ChaosSeed } | ConvertTo-Json

    # Mesma robustez já aplicada à desativação (achado ao vivo, piloto F1/C) — 3 tentativas antes
    # de desistir, para qualquer falha transitória de rede/pod não abortar o script inteiro fora
    # do try/finally que protege a desativação.
    for ($attempt = 1; $attempt -le 3 -and -not $chaosActivated; $attempt++) {
        try {
            Invoke-RestMethod -Method Post -Uri "$($target.AdminBaseUrl)/admin/chaos/activate" -Body $activateBody -ContentType "application/json" -ErrorAction Stop | Out-Null
            $chaosActivated = $true
        } catch {
            Write-Warning "Tentativa $attempt de ativar o caos falhou: $_"
            if ($attempt -lt 3) { Start-Sleep -Seconds 5 }
        }
    }
    if (-not $chaosActivated) {
        throw "Ativação do caos ($Scenario) falhou 3x — abortando execução."
    }

    if ($Scenario -eq "F5") {
        # O F5 é kill abrupto e (quase) instantâneo — o próprio injetor grava o instante em Redis.
        Start-Sleep -Seconds 2
        $firedAtUtc = docker exec norn-redis redis-cli HGET norn:chaos:active firedAtUtc
        if ($firedAtUtc -and $firedAtUtc -ne "") { $f5KillAtUtc = $firedAtUtc }
    }

    $deadlineUtc = $startedAtUtc.AddMinutes($DurationMinutes)
    $pollIntervalSeconds = 5
    $cpuClockSamplesMhz = @()

    # Achado da revisão de código antes do commit: consultar `lastState.terminated` só uma vez no
    # teardown perde o evento sempre que o próprio Norn reage ao F1 com RestartPod (DeleteNamespacedPodAsync)
    # nos braços B/C — o Pod antigo, que sofreu o OOMKilled de verdade, some, e um Pod novo sem
    # histórico toma o lugar de `.items[0]`. Poll a cada 5s por todos os Pods do rótulo, guardando o
    # primeiro OOMKilled visto, reduz a janela de corrida de "toda a observação" para "um intervalo de
    # poll" — não elimina a corrida por completo (um restart mais rápido que 5s ainda pode escapar),
    # mas o caso comum (detecção + decisão do Norn levam dezenas de segundos, Fase 9) fica coberto. Uma
    # correção completa exigiria capturar o timestamp dentro do próprio Executor no instante da atuação
    # (fora do escopo desta sessão, só ferramental de campanha) — registrar como risco residual a
    # validar nos 3 pilotos do DoD da Fase 12.
    #
    # `-o json` + ConvertFrom-Json, não `-o jsonpath` — achado ao vivo (piloto F1/C): o PowerShell
    # (mesmo 7.x) reescreve os argumentos passados a um executável nativo, e as aspas literais que
    # o jsonpath exige em `{"|"}`/`{"\n"}` (separador de campo) chegam ao kubectl sem aspas —
    # "unrecognized character in action: U+007C '|'". JSON estruturado não depende de nenhuma
    # aspa sobrevivendo a essa reescrita.
    while ([DateTimeOffset]::UtcNow -lt $deadlineUtc) {
        if ($Scenario -eq "F1" -and $oomKilledAtUtc -eq "none") {
            $podsJson = kubectl get pods -l "app=$($target.Deployment)" -n norn-shop -o json 2>$null
            if ($podsJson) {
                try {
                    $pods = ($podsJson | ConvertFrom-Json).items
                    foreach ($pod in $pods) {
                        $terminated = $pod.status.containerStatuses[0].lastState.terminated
                        if ($terminated -and $terminated.reason -eq "OOMKilled") {
                            $oomKilledAtUtc = $terminated.finishedAt
                            break
                        }
                    }
                } catch { }
            }
        }

        # Clock médio de CPU (§3, DoD da Fase 12: "throttling térmico como covariável, não como
        # ruído") -- amostrado no mesmo poll, sem custo extra de espera. Temperatura fica sem
        # captura nesta sessão: o Windows não expõe isso sem WMI de terceiros (LibreHardwareMonitor)
        # ou admin.
        try {
            $clock = (Get-CimInstance -ClassName Win32_Processor -ErrorAction Stop | Select-Object -First 1).CurrentClockSpeed
            if ($clock) { $cpuClockSamplesMhz += $clock }
        } catch { }

        $sleepSeconds = [math]::Min($pollIntervalSeconds, ($deadlineUtc - [DateTimeOffset]::UtcNow).TotalSeconds)
        if ($sleepSeconds -gt 0) { Start-Sleep -Seconds $sleepSeconds }
    }

    $cpuClockAvgMhz = if ($cpuClockSamplesMhz.Count -gt 0) { ($cpuClockSamplesMhz | Measure-Object -Average).Average } else { $null }
    $observationEndUtc = [DateTimeOffset]::UtcNow
}
finally {
    # Roda sempre — sucesso ou exceção. Nunca deixa o caos ativo de verdade contra um pod real.
    if ($chaosActivated) {
        Step "Desativando caos (finally — garante que roda mesmo se algo acima falhar)"
        $deactivated = $false
        for ($attempt = 1; $attempt -le 3 -and -not $deactivated; $attempt++) {
            try {
                Invoke-RestMethod -Method Post -Uri "$($target.AdminBaseUrl)/admin/chaos/deactivate" -ErrorAction Stop -TimeoutSec 10 | Out-Null
                $deactivated = $true
            } catch {
                Write-Warning "Tentativa $attempt de desativar caos via HTTP falhou (pod pode estar em crash loop): $_"
                if ($attempt -lt 3) { Start-Sleep -Seconds 5 }
            }
        }

        if (-not $deactivated) {
            # Achado ao vivo (piloto F1/C, 18/09/2026): o pod pode estar inacessível bem na hora do
            # teardown (crash loop pelo próprio F1) — o deactivate via HTTP falha em silêncio e o
            # caos fica preso ativo no Redis indefinidamente, contaminando toda execução seguinte
            # de uma campanha desacompanhada. Fallback: limpar a chave direto, bypassando o HTTP.
            # `norn:chaos:active` é slot único e global (um cenário ativo por vez, confirmado pelo
            # schema do hash) — DEL é desativação completa, não específica de serviço.
            Write-Warning "Desativação via HTTP falhou 3x — limpando norn:chaos:active direto no Redis."
            try {
                docker exec norn-redis redis-cli DEL norn:chaos:active | Out-Null
            } catch {
                Write-Warning "Fallback via Redis também falhou — INTERVENÇÃO MANUAL NECESSÁRIA: docker exec norn-redis redis-cli DEL norn:chaos:active"
            }
        }
    }

    Step "Parando Norn.LoadGenerator"
    try {
        Wait-Job $loadGenJob -Timeout 120 | Out-Null
        Receive-Job $loadGenJob | Write-Host
        Remove-Job $loadGenJob -Force
    } catch { }

    if ($portForwardJob) {
        try {
            Stop-Job $portForwardJob | Out-Null
            Remove-Job $portForwardJob -Force
        } catch { }
    }
}

if (-not $observationEndUtc) {
    throw "Execução interrompida antes do fim da observação — caos e jobs já desligados (finally), mas sem dado para rotular. Descarte esta tentativa e refaça."
}

# --- Rotulagem (tarefa 1) ----------------------------------------------------------------
Step "Rotulando a execução (Norn.Labeler label)"
$labelArgs = @(
    "run", "--project", "tools/Norn.Labeler", "--",
    "label",
    "--run-id", $runId,
    "--scenario", $Scenario,
    "--arm", $Arm,
    "--repetition", $Repetition,
    "--run-order", $RunOrder,
    "--target-rps", $targetRps,
    "--target-service", $target.ExportedJob,
    "--window-start-utc", $startedAtUtc.ToString("o"),
    "--observation-end-utc", $observationEndUtc.ToString("o"),
    "--load-report", $loadReportPath,
    "--injection-at-utc", $injectionAtUtc.ToString("o"),
    "--oom-killed-at-utc", $oomKilledAtUtc,
    "--f5-kill-at-utc", $f5KillAtUtc
)
if ($null -ne $cpuClockAvgMhz) { $labelArgs += @("--cpu-clock-avg-mhz", $cpuClockAvgMhz) }
dotnet @labelArgs
if ($LASTEXITCODE -ne 0) { throw "Norn.Labeler label falhou" }

# --- Teardown final: ambiente limpo para a próxima execução -------------------------------
Step "Restaurando réplicas ao baseline"
# catalog-api fica fora deste loop genérico — o bloco abaixo escala ele a 0 e de volta a 1, então
# uma chamada de --replicas=1 aqui seria descartada 1 linha depois sem efeito nenhum (achado do
# code-reviewer antes do commit).
foreach ($deployment in @("order-api", "payment-api")) {
    kubectl scale "deployment/$deployment" -n norn-shop --replicas=1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "kubectl scale $deployment --replicas=1 falhou (teardown)" }
}

# Achado ao vivo (pilotos F1/C e F5/C, Fase 12): depois de um RestartPod/OOM real, o pod novo do
# Catalog nasce com memória residual alta (herdada do efeito de retenção do F1, que não libera
# memória só por desativar o caos) e entra em CrashLoopBackOff — só um ciclo completo de escala
# 0→1 devolve o baseline limpo (~70Mi); `kubectl delete pod` direto continua negado por permissão.
# Incondicional (não só depois de F1/F5): o custo (~10-20s) é desprezível contra os 25 min de uma
# execução e as ~20h da campanha, e evita qualquer caso futuro em que outro cenário acabe
# reiniciando o Catalog por um caminho diferente.
Step "Ciclo de escala 0→1 do Catalog — garante memória residual limpa para a próxima execução"
kubectl scale deployment/catalog-api -n norn-shop --replicas=0 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "kubectl scale catalog-api --replicas=0 falhou (teardown)" }

# `kubectl rollout status` foi desenhado pra rollout de template (ReplicaSet novo substituindo o
# antigo), não pra escala dentro do mesmo ReplicaSet — pode reportar sucesso antes do pod velho
# sair de verdade (achado do code-reviewer antes do commit). Poll direto do Pod, mesmo padrão de
# `-o json` já usado no poll de OOMKilled acima, confirma a remoção sem depender dessa semântica.
# 12 tentativas de 5s = 60s, mesmo orçamento que o rollout status teria usado.
$catalogGone = $false
for ($i = 0; $i -lt 12 -and -not $catalogGone; $i++) {
    $remaining = kubectl get pods -l app=catalog-api -n norn-shop -o json 2>$null | ConvertFrom-Json
    if (-not $remaining -or $remaining.items.Count -eq 0) { $catalogGone = $true } else { Start-Sleep -Seconds 5 }
}
if (-not $catalogGone) { throw "Pod do catalog-api não terminou em 60s depois de escalar a 0 (teardown)" }

kubectl scale deployment/catalog-api -n norn-shop --replicas=1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "kubectl scale catalog-api --replicas=1 falhou (teardown)" }
# 120s: mesmo timeout já usado pro rollout status do reset, no início do script.
kubectl rollout status deployment/catalog-api -n norn-shop --timeout=120s | Out-Null
if ($LASTEXITCODE -ne 0) { throw "catalog-api não voltou a Ready em 120s depois do ciclo de limpeza (teardown)" }

Step "Execução $runId concluída ($Scenario/$Arm, repetição $Repetition)"

} finally {
    Pop-Location
}
