[CmdletBinding()]
param(
    [string]$Python = 'python',
    [string]$ChromePath,
    [string]$AxePath,
    [string]$Artifacts
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$runId = [Guid]::NewGuid().ToString('N')
$database = 'GoFixWebTests_' + $runId
$logs = Join-Path $env:TEMP ('gofix-web-' + $runId)
New-Item -ItemType Directory -Path $logs | Out-Null
if (-not $Artifacts) { $Artifacts = $logs }
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnet = if ($dotnetCommand) { $dotnetCommand.Source } else {
    Join-Path $env:LOCALAPPDATA 'GoFix/dotnet/dotnet.exe'
}
$node = (Get-Command node -ErrorAction Stop).Source
$api = $null
$web = $null
$ownsDatabase = $false
$prior = @{}
$settings = @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    ASPNETCORE_URLS = 'https://localhost:7343'
    ConnectionStrings__GoFix = "Server=lpc:.;Database=$database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
    GOFIX_API_TARGET = 'https://localhost:7343'
    GOFIX_WEB_BASE_URL = 'https://localhost:3303'
}
if ($AxePath) { $settings.GOFIX_AXE_PATH = (Resolve-Path -LiteralPath $AxePath).Path }
$mkcert = Join-Path $env:LOCALAPPDATA 'mkcert/rootCA.pem'
if (Test-Path -LiteralPath $mkcert) { $settings.NODE_EXTRA_CA_CERTS = $mkcert }

Push-Location $repo
try {
    foreach ($name in $settings.Keys) {
        $prior[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $settings[$name], 'Process')
    }
    & $Python -c 'import playwright'
    if ($LASTEXITCODE) { throw 'Instala Playwright en el intérprete indicado con -Python.' }
    foreach ($port in 7343, 3303) {
        $listener = Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue
        if ($listener) { throw "El puerto $port ya está en uso." }
    }

    & $dotnet build GoFix.slnx --no-restore
    if ($LASTEXITCODE) { throw 'Falló la compilación .NET.' }
    & $dotnet tool restore
    if ($LASTEXITCODE) { throw 'No se pudo restaurar dotnet-ef.' }
    sqlcmd -S lpc:. -E -C -b -d master -Q "CREATE DATABASE [$database];"
    if ($LASTEXITCODE) { throw 'No se pudo crear la base aislada.' }
    $ownsDatabase = $true
    & $dotnet tool run dotnet-ef database update --no-build --project src/GoFix.Infrastructure --startup-project src/GoFix.Api *> (Join-Path $logs 'migrate.log')
    if ($LASTEXITCODE) { throw "Falló la migración. Consulta $logs." }
    sqlcmd -S lpc:. -E -C -b -d $database -f 65001 -i scripts/sql/LoadInitialCatalogs.sql
    if ($LASTEXITCODE) { throw 'Falló la carga de catálogos.' }

    $api = Start-Process -WindowStyle Hidden -PassThru -FilePath $dotnet -ArgumentList 'bin/Debug/net10.0/GoFix.Api.dll' -WorkingDirectory (Join-Path $repo 'src/GoFix.Api') -RedirectStandardOutput (Join-Path $logs 'api.log') -RedirectStandardError (Join-Path $logs 'api.err')
    $web = Start-Process -WindowStyle Hidden -PassThru -FilePath $node -ArgumentList '--use-system-ca ./node_modules/next/dist/bin/next dev --hostname localhost --port 3303 --experimental-https --experimental-https-cert certificates/localhost.pem --experimental-https-key certificates/localhost-key.pem' -WorkingDirectory (Join-Path $repo 'frontend') -RedirectStandardOutput (Join-Path $logs 'web.log') -RedirectStandardError (Join-Path $logs 'web.err')
    foreach ($url in 'https://localhost:7343/health/ready', 'https://localhost:3303/api/v1/especialidades') {
        $ready = $false
        for ($attempt = 0; $attempt -lt 60; $attempt++) {
            curl.exe --ssl-no-revoke --silent --fail --max-time 2 -o NUL $url
            if ($LASTEXITCODE -eq 0) { $ready = $true; break }
            if ($api.HasExited -or $web.HasExited) { throw "El servidor terminó. Consulta $logs." }
            Start-Sleep -Milliseconds 500
        }
        if (-not $ready) { throw "No respondió $url. Consulta $logs." }
    }
    & $Python (Join-Path $PSScriptRoot 'experience.py') --browser msedge --screenshots $Artifacts
    if ($LASTEXITCODE) { throw 'Fallaron las pruebas Edge.' }
    if ($ChromePath) {
        & $Python (Join-Path $PSScriptRoot 'experience.py') --browser chrome --executable $ChromePath --smoke --screenshots $Artifacts
        if ($LASTEXITCODE) { throw 'Fallaron las pruebas Chrome.' }
    }
    Write-Host "Pruebas web aprobadas. Diagnósticos en $logs."
}
finally {
    if ($web -and -not $web.HasExited) { taskkill.exe /PID $web.Id /T /F | Out-Null }
    if ($api -and -not $api.HasExited) { Stop-Process -Id $api.Id }
    if ($ownsDatabase) {
        if ($database -notmatch '\AGoFixWebTests_[a-f0-9]{32}\z') { throw 'Nombre de base de prueba inválido.' }
        sqlcmd -S lpc:. -E -C -b -d master -Q "ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database];"
        if ($LASTEXITCODE) { Write-Error "No se pudo eliminar la base temporal $database." }
    }
    foreach ($name in $prior.Keys) {
        [Environment]::SetEnvironmentVariable($name, $prior[$name], 'Process')
    }
    Pop-Location
}
