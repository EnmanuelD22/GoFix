-- Ejecutar con la conexión a la base de GoFix después de ProfessionalsCatalogs.
-- Carga del piloto. Revisar conexión de destino antes de ejecutarlo.
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

-- Seleccion inicial del piloto, delegada por el responsable del producto.
INSERT INTO @Especialidades (Codigo, Nombre) VALUES
('electricidad', N'Electricidad'),
('plomeria', N'Plomería'),
('refrigeracion', N'Refrigeración y aire acondicionado'),
('electrodomesticos', N'Reparación de electrodomésticos'),
('cerrajeria', N'Cerrajería'),
('pintura', N'Pintura');

INSERT INTO @Sectores (Codigo, Nombre, Municipio) VALUES
('sdg-gazcue', N'Gazcue', N'Santo Domingo de Guzmán'),
('sdg-ensanche-naco', N'Ensanche Naco', N'Santo Domingo de Guzmán'),
('sdg-piantini', N'Piantini', N'Santo Domingo de Guzmán'),
('sdg-bella-vista', N'Bella Vista', N'Santo Domingo de Guzmán');

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
