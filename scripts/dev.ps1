param([switch]$NoPause)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$api = $null
$exitCode = 0

function Assert-LastCommand([string]$message) {
    if ($LASTEXITCODE -ne 0) { throw "$message (exit code $LASTEXITCODE)" }
}

try {
    Write-Host 'Starting Webhook Studio development environment...' -ForegroundColor Cyan
    if (-not (Test-Path "$root/src/WebhookStudio.Web/node_modules")) {
        throw 'Frontend dependencies are missing. Run npm install in src/WebhookStudio.Web first.'
    }

    dotnet build "$root/WebhookStudio.sln" --no-restore
    Assert-LastCommand 'The .NET build failed'

    $apiPath = "$root/src/WebhookStudio.Api/bin/Debug/net8.0/WebhookStudio.Api.exe"
    $api = Start-Process $apiPath -ArgumentList '--urls','http://localhost:5080' -WorkingDirectory "$root/src/WebhookStudio.Api" -WindowStyle Hidden -PassThru

    $healthy = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        if ($api.HasExited) { throw "The API stopped during startup (exit code $($api.ExitCode))." }
        try {
            Invoke-RestMethod 'http://localhost:5080/health' | Out-Null
            $healthy = $true
            break
        } catch { Start-Sleep -Milliseconds 250 }
    }
    if (-not $healthy) { throw 'The API did not become healthy at http://localhost:5080/health.' }

    Write-Host 'API:      http://localhost:5080' -ForegroundColor Green
    Write-Host 'Frontend: http://localhost:5173' -ForegroundColor Green
    Write-Host 'Press Ctrl+C to stop both services.' -ForegroundColor DarkGray
    Push-Location "$root/src/WebhookStudio.Web"
    try {
        npm run dev
        if ($LASTEXITCODE -notin 0, -1, 130) {
            throw "The Vite development server failed (exit code $LASTEXITCODE)"
        }
    } finally { Pop-Location }
} catch {
    $exitCode = 1
    Write-Host ''
    Write-Host 'Failed to start Webhook Studio:' -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
} finally {
    if ($api -and -not $api.HasExited) {
        Stop-Process -Id $api.Id -Force -ErrorAction SilentlyContinue
    }
    if (-not $NoPause) { Read-Host 'Press Enter to close this window' | Out-Null }
}

exit $exitCode
