-- Ejecutar exclusivamente en una base desechable, ya migrada.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
IF DB_NAME() NOT LIKE 'GoFixPerfTests[_]%'
    THROW 51000, 'La base debe llamarse GoFixPerfTests_<id>.', 1;
IF EXISTS (SELECT 1 FROM [identity].[AspNetUsers] WHERE Email LIKE '%@perf.gofix.test')
    THROW 51001, 'La carga ya existe; use otra base desechable.', 1;

BEGIN TRANSACTION;
DECLARE @especialidad int, @sector int;
INSERT INTO professionals.Especialidad (Codigo, Nombre, Activa)
VALUES ('perf-electricidad', N'Electricidad de prueba', 1);
SET @especialidad = SCOPE_IDENTITY();
INSERT INTO professionals.Sector (Codigo, Nombre, Municipio, Activo)
VALUES ('perf-centro', N'Centro de prueba', N'Municipio de prueba', 1);
SET @sector = SCOPE_IDENTITY();

;WITH numbers AS (
    SELECT TOP (1000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects AS a CROSS JOIN sys.all_objects AS b
)
INSERT INTO [identity].[AspNetUsers]
    (Id, Nombre, UserName, NormalizedUserName, Email, NormalizedEmail,
     EmailConfirmed, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled,
     LockoutEnabled, AccessFailedCount)
SELECT NEWID(), CONCAT(N'Técnico de carga ', FORMAT(n, '0000')),
       CONCAT('perf-', FORMAT(n, '0000'), '@perf.gofix.test'),
       UPPER(CONCAT('perf-', FORMAT(n, '0000'), '@perf.gofix.test')),
       CONCAT('perf-', FORMAT(n, '0000'), '@perf.gofix.test'),
       UPPER(CONCAT('perf-', FORMAT(n, '0000'), '@perf.gofix.test')),
       1, '+59176543210', 1, 0, 0, 0
FROM numbers;

INSERT INTO professionals.PerfilTecnico (Id, UsuarioId, Descripcion)
SELECT NEWID(), Id, N'Técnico de prueba con experiencia en instalaciones eléctricas residenciales.'
FROM [identity].[AspNetUsers] WHERE Email LIKE '%@perf.gofix.test';

INSERT INTO professionals.TecnicoEspecialidad (PerfilTecnicoId, EspecialidadId)
SELECT Id, @especialidad FROM professionals.PerfilTecnico
WHERE UsuarioId IN (SELECT Id FROM [identity].[AspNetUsers] WHERE Email LIKE '%@perf.gofix.test');
INSERT INTO professionals.TecnicoSector (PerfilTecnicoId, SectorId)
SELECT Id, @sector FROM professionals.PerfilTecnico
WHERE UsuarioId IN (SELECT Id FROM [identity].[AspNetUsers] WHERE Email LIKE '%@perf.gofix.test');

IF (SELECT COUNT(*) FROM professionals.PerfilTecnico WHERE UsuarioId IN
    (SELECT Id FROM [identity].[AspNetUsers] WHERE Email LIKE '%@perf.gofix.test')) <> 1000
    THROW 51002, 'No se insertaron exactamente 1000 técnicos.', 1;
COMMIT TRANSACTION;
