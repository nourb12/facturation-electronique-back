param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\ml_service_livraison_securisee.zip")
)

$source = (Resolve-Path (Join-Path $PSScriptRoot "..\ml_service")).Path
$output = [System.IO.Path]::GetFullPath($OutputPath)
$excluded = @(".env", ".venv", "__pycache__", ".ipynb_checkpoints")

$files = Get-ChildItem $source -Recurse -File | Where-Object {
    $relative = $_.FullName.Substring($source.Length).TrimStart('\', '/')
    -not ($excluded | Where-Object { $relative -split '[\\/]' -contains $_ })
}

if (Test-Path $output) { Remove-Item -LiteralPath $output -Force }
Compress-Archive -Path $files.FullName -DestinationPath $output -CompressionLevel Optimal
Write-Host "Archive sécurisée créée : $output"
