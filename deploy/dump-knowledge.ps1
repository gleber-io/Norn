<#
.SYNOPSIS
    Backup do Knowledge (Fase 12, tarefa 3b) -- para fora do VHDX do WSL2, nunca versionado no git.

.DESCRIPTION
    "A pergunta que decide o formato do backup e 'quanto retrabalho custa perder isto?'" (Master
    Plan Sec3): perder o Postgres custa refazer a campanha inteira -- vinte horas e uma janela de
    calendario que um TCC nao tem duas vezes. `pg_dump` de dentro do container `norn-postgres` para
    uma pasta fora do WSL2 (default C:\norn-backups\), nomeado com data e hora.

.PARAMETER OutputDirectory
    Pasta fora do VHDX do WSL2. Default C:\git\norn-results\postgres-backups -- mesma pasta
    consolidada dos demais artefatos da campanha (logs, screenshots, CSVs), pra tudo que o TCC
    precisa depois ficar num lugar só, fora do repositório git (nunca versionado).
#>
param(
    [string]$OutputDirectory = "C:\git\norn-results\postgres-backups"
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$dumpPath = Join-Path $OutputDirectory "norn-knowledge-$timestamp.sql"

Write-Host "Gerando dump em $dumpPath ..."
docker exec norn-postgres pg_dump -U norn -d norn --format=plain | Out-File -Encoding utf8 $dumpPath
if ($LASTEXITCODE -ne 0) { throw "pg_dump falhou" }

$sizeKb = [math]::Round((Get-Item $dumpPath).Length / 1KB, 1)
Write-Host "Dump concluido: $dumpPath ($sizeKb KB)"
Write-Host "Lembrete (Sec3): um dos dumps do lote precisa ser restaurado e reprocessado ate o grafico final -- backup nao verificado nao e backup."
