[CmdletBinding()]
param(
    [string]$BackendRoot = "C:\backendpfe\einvoicing",
    [string]$FrontendUrl = "http://localhost",
    [string]$FrontendLoginUrl = "http://localhost/login",
    [string]$BackendUrl = "http://localhost:5051/",
    [string]$BackendLoginUrl = "http://localhost/api/auth/login",
    [string]$OcrHealthUrl = "http://localhost:8000/health",
    [string]$JenkinsUrl = "http://localhost:8081/login",
    [string]$SonarUrl = "http://localhost:9000/api/system/status",
    [string]$NexusUrl = "http://localhost:8083/",
    [string]$PrometheusTargetsUrl = "http://localhost:9090/api/v1/targets",
    [string]$GrafanaHealthUrl = "http://localhost:3000/api/health"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Section([string]$Title) {
    Write-Host ""
    Write-Host "=== $Title ===" -ForegroundColor Cyan
}

function Assert-UrlStatus([string]$Name, [string]$Url, [int[]]$AllowedStatusCodes) {
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -Method Get -TimeoutSec 20
        if ($AllowedStatusCodes -contains [int]$response.StatusCode) {
            Write-Host "[OK] $Name -> $($response.StatusCode)" -ForegroundColor Green
            return
        }

        throw "Unexpected status code $($response.StatusCode)"
    }
    catch {
        $statusCode = $null
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }

        if ($null -ne $statusCode -and $AllowedStatusCodes -contains $statusCode) {
            Write-Host "[OK] $Name -> $statusCode" -ForegroundColor Green
            return
        }

        Write-Host "[FAIL] $Name -> $($_.Exception.Message)" -ForegroundColor Red
        throw
    }
}

function Assert-ContainerRunning([string]$ContainerName) {
    $running = docker ps --format "{{.Names}}" | Where-Object { $_ -eq $ContainerName }
    if (-not $running) {
        throw "Container missing or stopped: $ContainerName"
    }

    Write-Host "[OK] Container $ContainerName running" -ForegroundColor Green
}

Write-Section "Containers"
$containers = @(
    "tuniflow-postgres",
    "tuniflow-ocr-api",
    "tuniflow-backend-api",
    "tuniflow-frontend",
    "tuniflow-jenkins",
    "tuniflow-sonarqube",
    "tuniflow-nexus",
    "tuniflow-prometheus",
    "tuniflow-grafana"
)
$containers | ForEach-Object { Assert-ContainerRunning $_ }

Write-Section "Application URLs"
Assert-UrlStatus "Frontend" $FrontendUrl @(200)
Assert-UrlStatus "Frontend login" $FrontendLoginUrl @(200)
Assert-UrlStatus "Backend direct" $BackendUrl @(401)
Assert-UrlStatus "Backend proxy auth/login" $BackendLoginUrl @(401)
Assert-UrlStatus "OCR health" $OcrHealthUrl @(200)

Write-Section "DevOps URLs"
Assert-UrlStatus "Jenkins login" $JenkinsUrl @(200, 403)
Assert-UrlStatus "SonarQube status" $SonarUrl @(200)
Assert-UrlStatus "Nexus UI" $NexusUrl @(200)
Assert-UrlStatus "Grafana health" $GrafanaHealthUrl @(200)

Write-Section "Prometheus targets"
$targetsResponse = Invoke-WebRequest -UseBasicParsing -Uri $PrometheusTargetsUrl -TimeoutSec 20
$targetsJson = $targetsResponse.Content | ConvertFrom-Json
$activeTargets = @($targetsJson.data.activeTargets)
$downTargets = @($activeTargets | Where-Object { $_.health -ne "up" })

if ($downTargets.Count -gt 0) {
    $downTargets | ForEach-Object { Write-Host "[FAIL] Target down: $($_.labels.job) / $($_.labels.instance)" -ForegroundColor Red }
    throw "Some Prometheus targets are down."
}

Write-Host "[OK] Prometheus targets up: $($activeTargets.Count)" -ForegroundColor Green

Write-Section "Compose files"
docker compose -f (Join-Path $BackendRoot "docker-compose.local.yml") config | Out-Null
Write-Host "[OK] docker-compose.local.yml valid" -ForegroundColor Green
docker compose -f (Join-Path $BackendRoot "docker-compose.devops.yml") config | Out-Null
Write-Host "[OK] docker-compose.devops.yml valid" -ForegroundColor Green

Write-Section "Result"
Write-Host "Stack verification completed successfully." -ForegroundColor Green
