-- Ejecutar con la conexión a la base de GoFix después de ProfessionalsCatalogs.
-- Agregar las filas aprobadas en los dos bloques INSERT y revisar el archivo antes de ejecutarlo.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Especialidades TABLE (
    Codigo varchar(64) NOT NULL PRIMARY KEY,
    Nombre nvarchar(100) NOT NULL
);
DECLARE @Sectores TABLE (
    Codigo varchar(64) NOT NULL PRIMARY KEY,
    Nombre nvarchar(100) NOT NULL,
    Municipio nvarchar(100) NOT NULL
);

-- Insertar aquí las especialidades y los sectores aprobados.
-- INSERT INTO @Especialidades (Codigo, Nombre) VALUES (...);
-- INSERT INTO @Sectores (Codigo, Nombre, Municipio) VALUES (...);

IF NOT EXISTS (SELECT 1 FROM @Especialidades) OR NOT EXISTS (SELECT 1 FROM @Sectores)
    THROW 51000, N'Completa los catálogos iniciales con los datos aprobados antes de ejecutar la carga.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    -- Dos ejecuciones de este procedimiento en la misma base se aplican en serie.
    DECLARE @ResultadoBloqueo int;
    EXEC @ResultadoBloqueo = sys.sp_getapplock
        @Resource = 'GoFix:InitialCatalogs',
        @LockMode = 'Exclusive',
        @LockOwner = 'Transaction',
        @LockTimeout = 10000;

    IF @ResultadoBloqueo < 0
        THROW 51001, N'No se pudo obtener el bloqueo para cargar los catálogos.', 1;

    -- Repetir la carga conserva nombres actualizados y entradas desactivadas por operación.
    INSERT INTO [professionals].[Especialidad] (Codigo, Nombre, Activa)
    SELECT entrada.Codigo, entrada.Nombre, 1
    FROM @Especialidades AS entrada
    WHERE NOT EXISTS (
        SELECT 1 FROM [professionals].[Especialidad] AS actual WITH (UPDLOCK, HOLDLOCK)
        WHERE actual.Codigo = entrada.Codigo
    );

    INSERT INTO [professionals].[Sector] (Codigo, Nombre, Municipio, Activo)
    SELECT entrada.Codigo, entrada.Nombre, entrada.Municipio, 1
    FROM @Sectores AS entrada
    WHERE NOT EXISTS (
        SELECT 1 FROM [professionals].[Sector] AS actual WITH (UPDLOCK, HOLDLOCK)
        WHERE actual.Codigo = entrada.Codigo
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
