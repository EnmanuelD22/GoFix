[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$SqlServer,
    [string]$Dotnet = 'dotnet',
    [string]$Npm = 'npm.cmd'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$scratch = Join-Path ([IO.Path]::GetTempPath()) "GoFixEntrega_$([guid]::NewGuid().ToString('N'))"
$database = "GoFixEntrega_$([guid]::NewGuid().ToString('N'))"
$source = Join-Path $scratch 'source'
$archive = Join-Path $scratch 'source.zip'
$oldConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__GoFix', 'Process')
$oldEnvironment = [Environment]::GetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Process')
$databaseCreated = $false

function Invoke-Checked {
    param([string]$File, [string[]]$Arguments)
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$File fallo con codigo $LASTEXITCODE."
    }
}

try {
    New-Item -ItemType Directory -Path $scratch, $source -Force | Out-Null
    # HEAD representa exactamente lo que recibiria una copia nueva del repositorio.
    Invoke-Checked 'git' @('-C', $repoRoot, 'archive', '--format=zip', '-o', $archive, 'HEAD')
    Expand-Archive -LiteralPath $archive -DestinationPath $source
    Push-Location $source
    try {
        Invoke-Checked $Dotnet @('restore', 'GoFix.slnx', '--locked-mode')
        Invoke-Checked $Dotnet @('build', 'GoFix.slnx', '--no-restore', '-c', 'Release')
        Invoke-Checked $Npm @('--prefix', 'frontend', 'ci')
        Invoke-Checked $Npm @('--prefix', 'frontend', 'run', 'typecheck')
        Invoke-Checked $Npm @('--prefix', 'frontend', 'run', 'build')
        Invoke-Checked $Dotnet @('tool', 'restore')

        $env:ConnectionStrings__GoFix = "Server=$SqlServer;Database=$database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        # EF crea exclusivamente esta base con nombre aleatorio; el bloque finally la elimina.
        $databaseCreated = $true
        Invoke-Checked $Dotnet @('tool', 'run', 'dotnet-ef', 'database', 'update', '--project', 'src/GoFix.Infrastructure', '--startup-project', 'src/GoFix.Api')
        $migrationCount = & sqlcmd -S $SqlServer -d $database -E -C -b -h -1 -W -Q 'SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.__EFMigrationsHistory;'
        if ($LASTEXITCODE -ne 0 -or ($migrationCount -join ' ').Trim() -notmatch '^3$') {
            throw "La copia limpia no contiene las tres migraciones esperadas: $($migrationCount -join ' ')."
        }
        Write-Host "Entrega limpia aprobada: restore bloqueado, build, frontend y tres migraciones en $database."
    } finally {
        Pop-Location
    }
} finally {
    [Environment]::SetEnvironmentVariable('ConnectionStrings__GoFix', $oldConnection, 'Process')
    [Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', $oldEnvironment, 'Process')
    if ($databaseCreated) {
        & sqlcmd -S $SqlServer -d master -E -C -b -Q "IF DB_ID(N'$database') IS NOT NULL BEGIN ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]; END"
        if ($LASTEXITCODE -ne 0) { Write-Warning "No se pudo retirar la base temporal $database." }
    }
    if (Test-Path -LiteralPath $scratch) { Remove-Item -LiteralPath $scratch -Recurse -Force }
}
