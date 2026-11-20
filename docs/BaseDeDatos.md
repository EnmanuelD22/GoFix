# Base de datos de GoFix

GoFix usa SQL Server con un `GoFixDbContext` y una única secuencia de migraciones de EF Core. El modelo relacional sigue las relaciones descritas en [Arquitectura](Arquitectura.md#5-modelo-de-datos).

| Migración | Esquemas | Estructura |
|---|---|---|
| `InitialIdentity` | `identity`, `professionals` | Cuentas, roles, sesiones revocables y perfil técnico inicial con GUID independiente |
| `ProfessionalsCatalogs` | `professionals` | Especialidades, sectores y asociaciones de cobertura |
| `ServiceRequestAndReviews` | `services`, `reviews` | Solicitudes, eventos, claves de idempotencia y valoraciones |

Las claves foráneas entre módulos restringen el borrado físico. Las solicitudes apuntan al perfil técnico y a entradas de catálogo independientes. Así, un cambio en las asociaciones de cobertura permite conservar las solicitudes anteriores. Las citas usan `datetimeoffset` y conservan el desplazamiento recibido. Los instantes de auditoría se escriben en UTC en columnas `datetime2`.

SQL Server genera `rowversion` en perfiles y solicitudes. Al implementar la edición del perfil, el caso de uso deberá actualizar la fila del perfil en la misma transacción que sus asociaciones para detectar ediciones concurrentes de cobertura. La clave de idempotencia es única por cliente, almacena el hash SHA-256 en 32 bytes y referencia una sola solicitud. Cada solicitud admite una sola valoración y su puntuación debe estar entre 1 y 5. Autorización, cobertura activa, transiciones y elegibilidad corresponden a los futuros casos de uso. El borrado lógico conserva historial y claves y deberá filtrarse en las consultas operativas de solicitudes.

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
