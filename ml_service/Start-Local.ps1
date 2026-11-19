param([switch]$Reload)

$ErrorActionPreference = 'Stop'
$python = Join-Path $PSScriptRoot '.venv/Scripts/python.exe'
if (-not (Test-Path -LiteralPath $python)) {
    throw 'Create ml_service/.venv and install requirements.txt first.'
}

$key = $env:MachineLearning__ApiKey
if ([string]::IsNullOrWhiteSpace($key)) {
    $project = [xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot '../Einvoicing.Api/Einvoicing.Api.csproj') -Raw)
    $id = $project.SelectSingleNode('//UserSecretsId').InnerText
    $path = Join-Path $env:APPDATA "Microsoft/UserSecrets/$id/secrets.json"
    if (Test-Path -LiteralPath $path) {
        $settings = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        $key = $settings.'MachineLearning:ApiKey'
        if (-not $key) { $key = $settings.MachineLearning.ApiKey }
    }
}
if ([string]::IsNullOrWhiteSpace($key)) { $key = $env:TUNIFLOW_ML_API_KEY }
if ([string]::IsNullOrWhiteSpace($key) -or $key -match '^CHANGE_ME' -or $key -cne $key.Trim()) {
    throw 'Configure a valid MachineLearning:ApiKey in the .NET development user secrets.'
}

$previousKey = $env:TUNIFLOW_ML_API_KEY
$env:TUNIFLOW_ML_API_KEY = $key
$uvicornArgs = @('-m', 'uvicorn', 'app.main:app', '--host', '127.0.0.1', '--port', '8001')
if ($Reload) { $uvicornArgs += @('--reload', '--reload-dir', 'app') }
Push-Location -LiteralPath $PSScriptRoot
try {
    & $python @uvicornArgs
    if ($LASTEXITCODE -ne 0) { throw "Uvicorn exited with code $LASTEXITCODE." }
} finally {
    Pop-Location
    $env:TUNIFLOW_ML_API_KEY = $previousKey
}
