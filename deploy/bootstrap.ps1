<#
.SYNOPSIS
    Leva o Norn de "nada rodando" a sistema funcional no k3d em um comando (tarefa 8, Fase 6).

.DESCRIPTION
    Ordem linear, cada passo depende do anterior:
      1. Cluster k3d com registry local, Traefik desabilitado (D9), portas do NodePort mapeadas.
      2. RBAC do Norn (ADR-03) e do scrape de cAdvisor (tarefa 5a) aplicados ao cluster; token
         extraído para o Compose — precisa existir como arquivo antes do passo 3, senão o
         bind mount do Prometheus cria um diretório vazio no lugar (Docker faz isso em silêncio).
      3. Infra (Postgres/RabbitMQ/Redis) e observabilidade (Collector/Prometheus/Tempo/Grafana)
         via Compose; o container do Prometheus é então conectado à rede Docker do cluster k3d
         — só depois disso o job kubelet-cadvisor resolve o nó por nome.
      4. Imagens do Shop construídas com tag = SHA do commit (7.4: nunca "latest"), publicadas
         no registry do cluster.
      5. Manifests aplicados via kustomize (overlays/local), rollout aguardado.

    A partir da Fase 11 este script também empacota o dashboard React antes do passo 5
    (build → generate:api → npm build → docker build copiando dist/ para wwwroot) — não
    implementado ainda, porque o front não existe até aquela fase.

.PARAMETER SkipBuild
    Pula a construção e publicação das imagens — útil para reaplicar manifests sem rebuild.
#>
param(
    [switch]$SkipBuild
)

# "Stop" quebraria em qualquer escrita de progresso em stderr de docker/k3d/kubectl
# (comportamento normal dessas ferramentas) — os $LASTEXITCODE explícitos abaixo
# é que decidem falha real.
$ErrorActionPreference = "Continue"

$repoRoot = Split-Path -Parent $PSScriptRoot
$composeDir = Join-Path $PSScriptRoot "compose"
$k8sDir = Join-Path $PSScriptRoot "k8s"
$clusterName = "norn"
$registryName = "k3d-norn-registry"
$registryPort = 5000

function Step($message) {
    Write-Host ""
    Write-Host "==> $message" -ForegroundColor Cyan
}

# 1. Cluster k3d --------------------------------------------------------------------------
Step "Verificando cluster k3d '$clusterName'"
$clusterExists = (k3d cluster list -o json | ConvertFrom-Json) | Where-Object { $_.name -eq $clusterName }

if (-not $clusterExists) {
    Step "Criando registry local '$registryName'"
    k3d registry create norn-registry --port $registryPort
    if ($LASTEXITCODE -ne 0) { throw "k3d registry create falhou" }

    Step "Criando cluster k3d '$clusterName' (Traefik desabilitado — D9)"
    k3d cluster create $clusterName `
        --servers 1 --agents 0 `
        --registry-use "$($registryName):$registryPort" `
        --k3s-arg "--disable=traefik@server:0" `
        -p "8080:30080@server:0" `
        -p "8081:30081@server:0"
    if ($LASTEXITCODE -ne 0) { throw "k3d cluster create falhou" }
} else {
    Write-Host "Cluster '$clusterName' já existe — reaproveitando."
    k3d kubeconfig merge $clusterName --kubeconfig-merge-default --kubeconfig-switch-context | Out-Null

    # k3d só injeta host.k3d.internal no CoreDNS em create/start — um cluster que
    # sobrevive a um restart do Docker Desktop (containers preservados, mas o daemon
    # reinicia do zero) fica sem essa entrada até um ciclo explícito de stop/start,
    # e todo pod do Shop cai em CrashLoopBackOff por não resolver Postgres/RabbitMQ/Redis.
    Step "Reciclando o cluster para garantir host.k3d.internal no CoreDNS"
    k3d cluster stop $clusterName
    k3d cluster start $clusterName
    if ($LASTEXITCODE -ne 0) { throw "k3d cluster start falhou" }
}

kubectl config use-context "k3d-$clusterName" | Out-Null

# Verificação D9: k3d não deve ter instalado o Traefik.
$traefikPods = kubectl get pods -A --no-headers 2>$null | Select-String -Pattern "traefik"
if ($traefikPods) {
    throw "Traefik encontrado no cluster — a desabilitação via --k3s-arg falhou (D9)."
}
Write-Host "Traefik ausente — confirmado (D9)."

# 2. RBAC (Norn + cAdvisor) e token para o Prometheus (fora do cluster) --------------------
Step "Aplicando RBAC (ADR-03 e tarefa 5a) e namespaces"
kubectl apply -f (Join-Path $k8sDir "base/namespaces.yaml")
kubectl apply -f (Join-Path $k8sDir "base/rbac-norn.yaml")
kubectl apply -f (Join-Path $k8sDir "base/cadvisor-scrape-rbac.yaml")

Step "Extraindo token do ServiceAccount de cAdvisor para o Compose"
for ($i = 0; $i -lt 20; $i++) {
    $tokenB64 = kubectl get secret prometheus-cadvisor-reader-token -n norn-platform -o jsonpath="{.data.token}" 2>$null
    if ($tokenB64) { break }
    Start-Sleep -Seconds 1
}
if (-not $tokenB64) { throw "Token do ServiceAccount de cAdvisor ainda não populado pelo controller." }
$tokenPath = Join-Path $composeDir "cadvisor-token"
if (Test-Path $tokenPath -PathType Container) { Remove-Item -Recurse -Force $tokenPath }
# Set-Content -Encoding utf8 no Windows PowerShell 5.1 grava BOM — três bytes que o
# kubelet aceita como parte do bearer token e derrubam a autenticação com 401.
# WriteAllText com UTF8Encoding($false) é o único caminho sem BOM nesta versão.
$tokenText = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($tokenB64))
[System.IO.File]::WriteAllText($tokenPath, $tokenText, (New-Object System.Text.UTF8Encoding $false))

# 3. Compose: infra + observabilidade -------------------------------------------------------
Step "Subindo infra e observabilidade (Compose)"
docker compose -f (Join-Path $composeDir "compose.infra.yaml") -f (Join-Path $composeDir "compose.otel.yaml") up -d
if ($LASTEXITCODE -ne 0) { throw "docker compose up falhou" }

Step "Conectando o Prometheus à rede Docker do cluster k3d (alcançar :10250 do nó)"
# `network connect` num container já rodando não precisa de restart — o Prometheus
# resolve os alvos de static_configs por DNS a cada scrape, não uma vez no start.
docker network connect "k3d-$clusterName" norn-prometheus 2>$null

# 4. Build e publish das imagens do Shop ---------------------------------------------------
$imageTag = (git -C $repoRoot rev-parse --short=12 HEAD).Trim()
if (-not $SkipBuild) {
    Step "Construindo e publicando imagens do Shop (tag $imageTag)"
    $services = @(
        @{ Name = "catalog"; Dockerfile = "src/Shop/Norn.Shop.Catalog.API/Dockerfile" },
        @{ Name = "order";   Dockerfile = "src/Shop/Norn.Shop.Order.API/Dockerfile" },
        @{ Name = "payment"; Dockerfile = "src/Shop/Norn.Shop.Payment.API/Dockerfile" }
    )
    foreach ($svc in $services) {
        $image = "localhost:${registryPort}/norn-shop-$($svc.Name)-api:$imageTag"
        docker build -f (Join-Path $repoRoot $svc.Dockerfile) -t $image $repoRoot
        if ($LASTEXITCODE -ne 0) { throw "docker build falhou para $($svc.Name)" }
        docker push $image
        if ($LASTEXITCODE -ne 0) { throw "docker push falhou para $($svc.Name)" }
    }
} else {
    Write-Host "SkipBuild: reaproveitando imagens já publicadas com tag $imageTag."
}

# 5. Aplica os manifests --------------------------------------------------------------------
Step "Aplicando manifests (overlays/local, tag $imageTag)"
$tempOverlay = Join-Path ([System.IO.Path]::GetTempPath()) "norn-k8s-$imageTag"
if (Test-Path $tempOverlay) { Remove-Item -Recurse -Force $tempOverlay }
Copy-Item -Recurse $k8sDir $tempOverlay

$localKustomization = Join-Path $tempOverlay "overlays/local/kustomization.yaml"
(Get-Content $localKustomization) -replace "placeholder", $imageTag | Set-Content $localKustomization

kubectl apply -k (Join-Path $tempOverlay "overlays/local")
if ($LASTEXITCODE -ne 0) { throw "kubectl apply -k falhou" }
Remove-Item -Recurse -Force $tempOverlay

Step "Aguardando rollout"
foreach ($deployment in @("catalog-api", "order-api", "payment-api")) {
    kubectl rollout status "deployment/$deployment" -n norn-shop --timeout=180s
    if ($LASTEXITCODE -ne 0) { throw "Rollout de $deployment não completou" }
}

Step "Bootstrap concluído"
Write-Host "Catalog: http://localhost:8080/api/v1/products"
Write-Host "Order:   http://localhost:8081/api/v1/orders"
Write-Host "Grafana: http://localhost:3000"

