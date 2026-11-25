-- Revisar y ejecutar con sqlcmd -S <servidor> -E -b -i scripts/sql/InstallDailyBackupJob.sql.
-- Requiere SQL Server Agent en ejecución y privilegios para administrar jobs,
-- respaldar GoFixDB y limpiar únicamente el directorio exclusivo de este job.
USE msdb;
GO
IF DB_ID(N'GoFixDB') IS NULL
    THROW 51000, 'GoFixDB no existe en esta instancia.', 1;

IF EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = N'GoFix - respaldo diario')
BEGIN
    PRINT N'El job GoFix - respaldo diario ya existe. Revisar su configuración; no se modifica.';
    RETURN;
END;

DECLARE @jobId uniqueidentifier;
DECLARE @command nvarchar(max) = N'
DECLARE @root nvarchar(4000) = CONVERT(nvarchar(4000), SERVERPROPERTY(''InstanceDefaultBackupPath''));
IF @root IS NULL THROW 51001, ''SQL Server no informa InstanceDefaultBackupPath.'', 1;
DECLARE @directory nvarchar(4000) = @root + CASE WHEN RIGHT(@root, 1) = N''\'' THEN N'''' ELSE N''\'' END + N''GoFixDaily'';
EXEC master.dbo.xp_create_subdir @directory;
DECLARE @file nvarchar(4000) = @directory + N''\GoFixDB_'' + CONVERT(char(8), GETDATE(), 112) + N''_'' + REPLACE(CONVERT(char(8), GETDATE(), 108), '':'', '''') + N''_'' + CONVERT(nvarchar(36), NEWID()) + N''.bak'';
BACKUP DATABASE [GoFixDB] TO DISK = @file WITH COPY_ONLY, CHECKSUM, COMPRESSION, INIT;
RESTORE VERIFYONLY FROM DISK = @file WITH CHECKSUM;
DECLARE @cutoff datetime = DATEADD(day, -7, GETDATE());
EXEC master.dbo.xp_delete_file 0, @directory, N''bak'', @cutoff, 0;
';

EXEC msdb.dbo.sp_add_job
    @job_name = N'GoFix - respaldo diario',
    @enabled = 1,
    @description = N'Respaldo completo diario COPY_ONLY de GoFixDB; verificación y retención local de 7 días en directorio exclusivo.',
    @job_id = @jobId OUTPUT;

EXEC msdb.dbo.sp_add_jobstep
    @job_id = @jobId,
    @step_name = N'Respaldar y verificar GoFixDB',
    @subsystem = N'TSQL',
    @database_name = N'master',
    @command = @command,
    @on_success_action = 1,
    @on_fail_action = 2;

EXEC msdb.dbo.sp_add_jobschedule
    @job_id = @jobId,
    @name = N'GoFix - diario 02:00',
    @freq_type = 4,
    @freq_interval = 1,
    @active_start_time = 020000;

EXEC msdb.dbo.sp_add_jobserver @job_id = @jobId;
PRINT N'Job GoFix - respaldo diario creado. Configurar alerta por fallo y copia externa antes de considerar cumplido el RPO.';
GO
