# Base de datos de GoFix

GoFix usa SQL Server con un `GoFixDbContext` y una única secuencia de migraciones de EF Core. El modelo relacional sigue las relaciones descritas en [Arquitectura](Arquitectura.md#5-modelo-de-datos).

| Migración | Esquemas | Estructura |
|---|---|---|
| `InitialIdentity` | `identity`, `professionals` | Cuentas, roles, sesiones revocables y perfil técnico inicial con GUID independiente |
| `ProfessionalsCatalogs` | `professionals` | Especialidades, sectores y asociaciones de cobertura |
| `ServiceRequestAndReviews` | `services`, `reviews` | Solicitudes, eventos, claves de idempotencia y valoraciones |

Las claves foráneas entre módulos restringen el borrado físico. Las solicitudes apuntan al perfil técnico y a entradas de catálogo independientes. Así, un cambio en las asociaciones de cobertura permite conservar las solicitudes anteriores. Las citas usan `datetimeoffset` y conservan el desplazamiento recibido. Los instantes de auditoría se escriben en UTC en columnas `datetime2`.

SQL Server genera `rowversion` en perfiles y solicitudes. La edición del perfil actualiza su fila y sus asociaciones en una transacción para detectar ediciones concurrentes de cobertura. La clave de idempotencia es única por cliente, almacena el hash SHA-256 en 32 bytes y referencia una sola solicitud. Cada solicitud admite una sola valoración y su puntuación debe estar entre 1 y 5. Los casos de uso aplican autorización, cobertura activa, transiciones y elegibilidad. El borrado lógico conserva historial y claves, y las consultas operativas excluyen las solicitudes eliminadas.

## Aplicar y revisar migraciones

Desde la raíz, restaura la herramienta fijada en el repositorio y configura `ConnectionStrings:GoFix` mediante User Secrets o `ConnectionStrings__GoFix` en el entorno de ejecución.

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/GoFix.Infrastructure --startup-project src/GoFix.Api
```

Para un entorno compartido, genera el SQL idempotente, revisa las operaciones y verifica la base de destino antes de ejecutarlo con credenciales de migración. El historial de EF registra las migraciones aplicadas. La API espera que la migración ya esté aplicada cuando atiende las operaciones que necesitan sus tablas.

```powershell
New-Item -ItemType Directory -Path artifacts -Force
dotnet tool run dotnet-ef migrations script --idempotent --project src/GoFix.Infrastructure --startup-project src/GoFix.Api --output artifacts/GoFix.migrations.sql
```

El script puede revisarse y aplicarse a una base vacía o a una que ya tenga `InitialIdentity`. Verifica un respaldo antes de actualizar una base existente. Revertir las migraciones que crean tablas elimina los datos almacenados en ellas.

Si lo ejecutas con `sqlcmd` clásico (ODBC), activa `QUOTED_IDENTIFIER` con `-I` para los índices filtrados de Identity. `-b` devuelve un código de error si el script falla.

```powershell
sqlcmd -S '<servidor>' -d '<base-de-GoFix>' -E -b -I -i artifacts/GoFix.migrations.sql
```

## Cargar catálogos

El piloto seleccionado por delegación del responsable del producto usa Santo Domingo de Guzmán, Distrito Nacional. El script carga seis especialidades (electricidad, plomería, refrigeración y aire acondicionado, reparación de electrodomésticos, cerrajería y pintura) y cuatro sectores (Gazcue, Ensanche Naco, Piantini y Bella Vista). Ejecuta la carga después de aplicar las migraciones y verifica la conexión de destino. [Script de carga SQL](../scripts/sql/LoadInitialCatalogs.sql)

Los nombres territoriales se contrastaron con el plan municipal de la Alcaldía del Distrito Nacional. La selección es acotada para el piloto y se puede ampliar por operación. [Plan municipal](https://www.sismap.gob.do/Municipal/uploads/evidencias/637462984116335145-5%29-2020-2024-PLAN-MUNICIPAL-DE-DESARROLLO-20.11.2020.pdf)

```powershell
sqlcmd -S localhost -d GoFixDB -E -b -I -f 65001 -i scripts/sql/LoadInitialCatalogs.sql
```

- Asigna a cada fila un `Codigo` estable de hasta 64 caracteres. Empieza con letra minúscula ASCII o dígito y usa solo esas letras, dígitos y guiones. Conserva el código si el nombre visible cambia.
- Utiliza nombres de hasta 100 caracteres. La especialidad tiene nombre único y el sector tiene nombre único dentro de su municipio.
- Al repetir la carga, se insertan los códigos faltantes y se preservan los registros existentes, incluidos los desactivados. Los cambios posteriores de estado se administran mediante el procedimiento de operación.

La carga se ejecuta dentro de una transacción. Un error de integridad revierte todas las inserciones de esa ejecución.

## Respaldo y ensayo de recuperación (RNF-09)

El objetivo **propuesto** del piloto es RPO ≤ 24 h y RTO ≤ 4 h. El operador debe programar un respaldo completo diario y ejecutar otro inmediatamente antes de cada migración. La programación, retención y copia fuera del servidor pertenecen al entorno de despliegue; una ejecución manual no demuestra por sí sola un RPO de 24 h. El archivo `.bak` contiene datos personales: mantenlo fuera de Git, limita su acceso y conserva una copia en almacenamiento independiente del servidor SQL. El repositorio ignora los archivos de base de datos y `artifacts/`.

El script [Invoke-GoFixRecovery.ps1](../scripts/Invoke-GoFixRecovery.ps1) usa autenticación integrada. La cuenta operadora necesita permisos para respaldar la base y para restaurar y crear una base aislada en el ensayo. SQL Server debe poder escribir o leer la ruta `-BackupFile`; si se omite, usa su directorio de respaldos predeterminado. La opción `-TrustServerCertificate` es únicamente para desarrollo local con certificado no confiable.

```powershell
# Respaldo diario o previo a migración; anotar el archivo devuelto y verificar su retención.
./scripts/Invoke-GoFixRecovery.ps1 -Mode Backup -Server '<servidor>' -Database 'GoFixDB'

# Ensayo periódico: crea respaldo y restaura una copia temporal.
./scripts/Invoke-GoFixRecovery.ps1 -Mode Drill -Server '<servidor>' -Database 'GoFixDB'

# O bien, ensaya un respaldo existente sin crear uno nuevo.
./scripts/Invoke-GoFixRecovery.ps1 -Mode Drill -Server '<servidor>' -Database 'GoFixDB' -BackupFile '<ruta-absoluta-al-respaldo.bak>'
```

`Backup` crea un respaldo `COPY_ONLY` con `CHECKSUM` y compresión y ejecuta `RESTORE VERIFYONLY`. Rechaza sobrescribir un archivo ya existente. `Drill` crea un respaldo si no se indica `-BackupFile`; después verifica el respaldo, restaura todos sus archivos en una base `GoFixRecovery_<guid>`, comprueba tablas esenciales, las tres migraciones y `DBCC CHECKDB`, registra la duración y elimina **solo** esa base temporal incluso si falla la validación. Conserva el `.bak` para la política de retención del operador. No usar el ensayo como restauración de producción: ante una incidencia real, aislar el servicio, seleccionar el último respaldo válido, restaurar según el procedimiento del entorno y ejecutar las pruebas funcionales y de integridad antes de reabrir el tráfico.

Evidencia local del 25 de noviembre de 2026: un respaldo `COPY_ONLY` de `GoFixDB` se creó y verificó en 44,09 s; su restauración aislada, comprobación de 3 migraciones y `DBCC CHECKDB` terminaron en 20,1 s. Se confirmó que no quedó ninguna base `GoFixRecovery_*`. Esto demuestra el procedimiento en la máquina local para ese tamaño de datos; el RTO del entorno final requiere ensayo con su volumen, hardware y operación completa. Para comprobar el RPO, registrar las ejecuciones diarias y alertar cualquier respaldo omitido. Los modos `Backup` y `Drill` también se probaron sobre bases desechables. `Backup` creó y verificó un respaldo en 3,51 s; `Drill` migró una base, cargó catálogos, creó y restauró su respaldo en 9,89 s, con 3 migraciones y `DBCC CHECKDB` correctos. Las bases desechables se eliminaron. Los respaldos de ensayo permanecen en el directorio de respaldos de SQL Server y requieren aplicar la política local de retención.


### Respaldo diario con SQL Server Agent

La instancia local dispone de SQL Server Agent. El instalador [InstallDailyBackupJob.sql](../scripts/sql/InstallDailyBackupJob.sql) crea el job `GoFix - respaldo diario` solo si no existe, sin editar jobs ajenos. Corre cada día a las **02:00 según el reloj del servidor**, crea el `.bak` en la carpeta exclusiva `GoFixDaily` bajo el directorio predeterminado de respaldos SQL, lo verifica y retiene allí 7 días. La limpieza solo afecta los `.bak` de esa carpeta. Ejecuta con una cuenta autorizada a administrar jobs; revisa el horario, los permisos y el destino antes de aplicarlo en otro entorno.

```powershell
sqlcmd -S '<servidor>' -E -b -i scripts/sql/InstallDailyBackupJob.sql
sqlcmd -S '<servidor>' -E -b -Q "EXEC msdb.dbo.sp_help_job @job_name=N'GoFix - respaldo diario'"
# Ver el resultado del último paso: run_status = 1 indica éxito.
sqlcmd -S '<servidor>' -E -b -Q "SELECT TOP 1 h.run_date,h.run_time,h.run_status,h.message FROM msdb.dbo.sysjobhistory h JOIN msdb.dbo.sysjobs j ON j.job_id=h.job_id WHERE j.name=N'GoFix - respaldo diario' AND h.step_id=0 ORDER BY h.instance_id DESC"
```

En desarrollo local, si SQL Server presenta un certificado no confiable, agrega `-C` a `sqlcmd`; en despliegue usa un certificado confiable.

Estado local al 25 de noviembre de 2026: el job quedó habilitado y se ejecutó manualmente una vez con `run_status = 1`; produjo un respaldo verificado en `GoFixDaily`. `sp_help_job` indicó próxima ejecución el **26 de noviembre de 2026 a las 02:00, hora del servidor**. Antes de instalarlo, el ensayo con job y base desechables también terminó con `run_status = 1`, y ambos se eliminaron. Se debe configurar una alerta operativa ante fallos y copiar los respaldos a almacenamiento externo protegido: el job local por sí solo no cubre pérdida del equipo ni prueba un RPO real. Verifica diariamente el historial y la edad del último `.bak`. El respaldo previo a una migración sigue siendo una acción explícita mediante `-Mode Backup`, seguida de un ensayo de restauración si el cambio es de alto riesgo.
