[CmdletBinding()]
param(
    [ValidateSet('Backup', 'Drill')]
    [string]$Mode = 'Drill',
    [string]$Server = 'lpc:.',
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,127}$')]
    [string]$Database = 'GoFixDB',
    [string]$BackupFile,
    [switch]$TrustServerCertificate
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data

function Open-SqlConnection {
    param([string]$Catalog = 'master')
    $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
    $builder['Data Source'] = $Server
    $builder['Initial Catalog'] = $Catalog
    $builder['Integrated Security'] = $true
    $builder['Encrypt'] = $true
    $builder['TrustServerCertificate'] = [bool]$TrustServerCertificate
    $builder['Connect Timeout'] = 15
    $connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
    $connection.Open()
    return $connection
}

function Invoke-Sql {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$CommandText,
        [hashtable]$Parameters = @{},
        [switch]$Table
    )
    $command = $Connection.CreateCommand()
    $command.CommandText = $CommandText
    $command.CommandTimeout = 600
    foreach ($key in $Parameters.Keys) {
        [void]$command.Parameters.AddWithValue("@$key", $Parameters[$key])
    }
    try {
        if ($Table) {
            $result = [System.Data.DataTable]::new()
            $adapter = [System.Data.SqlClient.SqlDataAdapter]::new($command)
            try { [void]$adapter.Fill($result) } finally { $adapter.Dispose() }
            return ,$result
        }
        return $command.ExecuteScalar()
    } finally {
        $command.Dispose()
    }
}

function Quote-SqlIdentifier([string]$Value) { return '[' + $Value.Replace(']', ']]') + ']' }

$connection = Open-SqlConnection
$drillDatabase = $null
$started = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $sourceExists = Invoke-Sql $connection 'SELECT COUNT(*) FROM sys.databases WHERE name = @name AND state_desc = ''ONLINE''' @{ name = $Database }
    if ($sourceExists -ne 1) { throw "La base de origen $Database no existe o no está ONLINE." }

    $createBackup = $Mode -eq 'Backup' -or -not $BackupFile
    if (-not $BackupFile) {
        $backupDirectory = [string](Invoke-Sql $connection "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultBackupPath'))")
        if (-not $backupDirectory) { throw 'SQL Server no informa InstanceDefaultBackupPath; especifica -BackupFile.' }
        $BackupFile = Join-Path $backupDirectory ("{0}_{1:yyyyMMdd_HHmmss}_{2}.bak" -f $Database, [datetime]::UtcNow, [guid]::NewGuid().ToString('N'))
    }
    if (-not [IO.Path]::IsPathFullyQualified($BackupFile) -or [IO.Path]::GetExtension($BackupFile) -ne '.bak') {
        throw 'BackupFile debe ser una ruta absoluta con extensión .bak accesible por SQL Server.'
    }

    if ($createBackup) {
        $fileExists = Invoke-Sql $connection 'DECLARE @exists int; EXEC master.dbo.xp_fileexist @file, @exists OUTPUT; SELECT @exists' @{ file = $BackupFile }
        if ($fileExists -eq 1) { throw "El respaldo ya existe: $BackupFile" }
        # COPY_ONLY evita alterar la cadena de diferenciales del operador.
        [void](Invoke-Sql $connection "BACKUP DATABASE $(Quote-SqlIdentifier $Database) TO DISK = @file WITH COPY_ONLY, CHECKSUM, COMPRESSION, INIT" @{ file = $BackupFile })
        [void](Invoke-Sql $connection 'RESTORE VERIFYONLY FROM DISK = @file WITH CHECKSUM' @{ file = $BackupFile })
        if ($Mode -eq 'Backup') {
            [pscustomobject]@{ Modo = 'Backup'; Origen = $Database; Archivo = $BackupFile; Segundos = [math]::Round($started.Elapsed.TotalSeconds, 2); Verificado = $true }
            return
        }
    }

    $fileExists = Invoke-Sql $connection 'DECLARE @exists int; EXEC master.dbo.xp_fileexist @file, @exists OUTPUT; SELECT @exists' @{ file = $BackupFile }
    if ($fileExists -ne 1) { throw "SQL Server no puede acceder al respaldo: $BackupFile" }
    [void](Invoke-Sql $connection 'RESTORE VERIFYONLY FROM DISK = @file WITH CHECKSUM' @{ file = $BackupFile })
    $files = Invoke-Sql $connection 'RESTORE FILELISTONLY FROM DISK = @file' @{ file = $BackupFile } -Table
    if ($files.Rows.Count -lt 2) { throw 'El respaldo no contiene archivos de datos y log esperados.' }

    $drillDatabase = 'GoFixRecovery_' + [guid]::NewGuid().ToString('N')
    $dataDirectory = [string](Invoke-Sql $connection "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultDataPath'))")
    $logDirectory = [string](Invoke-Sql $connection "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultLogPath'))")
    if (-not $dataDirectory -or -not $logDirectory) { throw 'SQL Server no informa sus directorios de datos y log.' }
    $moves = @()
    $index = 0
    foreach ($file in $files.Rows) {
        $index++
        $logical = [string]$file.LogicalName
        $isLog = [string]$file.Type -eq 'L'
        $directory = if ($isLog) { $logDirectory } else { $dataDirectory }
        $extension = if ($isLog) { '.ldf' } elseif ($index -eq 1) { '.mdf' } else { '.ndf' }
        $physical = Join-Path $directory ("{0}_{1}{2}" -f $drillDatabase, $index, $extension)
        $moves += "MOVE N'$($logical.Replace("'", "''"))' TO N'$($physical.Replace("'", "''"))'"
    }
    $fileLiteral = $BackupFile.Replace("'", "''")
    $restore = "RESTORE DATABASE $(Quote-SqlIdentifier $drillDatabase) FROM DISK = N'$fileLiteral' WITH RECOVERY, CHECKSUM, " + ($moves -join ', ')
    [void](Invoke-Sql $connection $restore)

    $validation = Invoke-Sql $connection "SELECT CASE WHEN OBJECT_ID(N'$(Quote-SqlIdentifier $drillDatabase).[identity].AspNetUsers', 'U') IS NOT NULL AND OBJECT_ID(N'$(Quote-SqlIdentifier $drillDatabase).[professionals].PerfilTecnico', 'U') IS NOT NULL AND OBJECT_ID(N'$(Quote-SqlIdentifier $drillDatabase).dbo.__EFMigrationsHistory', 'U') IS NOT NULL THEN 1 ELSE 0 END"
    if ($validation -ne 1) { throw 'La copia restaurada no contiene las tablas esenciales de GoFix.' }
    $migrations = Invoke-Sql $connection "SELECT COUNT(*) FROM $(Quote-SqlIdentifier $drillDatabase).dbo.__EFMigrationsHistory"
    if ($migrations -lt 3) { throw "La copia restaurada contiene solo $migrations migraciones." }
    [void](Invoke-Sql $connection "DBCC CHECKDB($(Quote-SqlIdentifier $drillDatabase)) WITH NO_INFOMSGS")
    [pscustomobject]@{ Modo = 'Drill'; Origen = $Database; Restaurada = $drillDatabase; Archivo = $BackupFile; Segundos = [math]::Round($started.Elapsed.TotalSeconds, 2); Migraciones = $migrations; Validacion = 'Tablas y migraciones verificadas' }
} finally {
    if ($drillDatabase -and $connection.State -eq 'Open') {
        $existing = Invoke-Sql $connection 'SELECT COUNT(*) FROM sys.databases WHERE name = @name' @{ name = $drillDatabase }
        if ($existing -eq 1) {
            [void](Invoke-Sql $connection "ALTER DATABASE $(Quote-SqlIdentifier $drillDatabase) SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE $(Quote-SqlIdentifier $drillDatabase)")
        }
    }
    $connection.Dispose()
}
