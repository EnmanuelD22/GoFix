# GoFix

Plataforma web para conectar clientes con técnicos de su zona. El producto contempla búsqueda por especialidad y sector, solicitudes de asistencia, seguimiento del servicio y valoraciones. La primera versión está orientada a un municipio piloto, con interfaz en español; pagos, chat y mapas quedan fuera de su alcance inicial.

## Instalación y ejecución en Windows

Esta guía crea una base nueva de GoFix en SQL Server y carga los catálogos del piloto. No hace falta copiar archivos `.mdf` ni usar credenciales del autor.

### Requisitos

- Git y PowerShell.
- SDK .NET **10.0.400** o una revisión compatible de la misma banda `10.0.4xx` (véase [global.json](global.json)).
- Node.js 24 o superior y npm.
- SQL Server o SQL Server Express instalado y en ejecución, con autenticación de Windows y una cuenta con permisos para crear bases y aplicar migraciones.
- `sqlcmd` instalado y disponible en el PATH para cargar los catálogos.

### 1. Obtener el proyecto y sus dependencias

```powershell
git clone https://github.com/EnmanuelD22/GoFix.git
cd GoFix
dotnet restore GoFix.slnx --locked-mode
dotnet build GoFix.slnx --no-restore
dotnet tool restore
npm.cmd --prefix frontend ci
dotnet dev-certs https --trust
```

Acepta la instalación del certificado de desarrollo cuando Windows lo solicite. Comprueba que los puertos `7043` y `3000` estén libres.

### 2. Configurar SQL Server y aplicar migraciones

El siguiente ejemplo usa una instancia local `localhost` y una base nueva `GoFixDB`. Cambia `localhost` por el nombre de tu instancia, por ejemplo `.\SQLEXPRESS`, en **ambos comandos**. `TrustServerCertificate=True` es apropiado para esta prueba local con certificado de SQL Server no confiable.

```powershell
dotnet user-secrets set "ConnectionStrings:GoFix" "Server=localhost;Database=GoFixDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" --project src/GoFix.Api
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet tool run dotnet-ef database update --project src/GoFix.Infrastructure --startup-project src/GoFix.Api
sqlcmd -S localhost -d GoFixDB -E -C -b -I -f 65001 -i scripts/sql/LoadInitialCatalogs.sql
```

EF Core crea la base si la cuenta tiene permiso, aplica las tres migraciones y registra su historial. El último comando carga especialidades y sectores: es necesario para completar y publicar un perfil técnico. Para usar una base existente, comprueba el destino y respáldala antes de aplicar migraciones. Consulta la [guía de base de datos](docs/BaseDeDatos.md) para otras opciones y solución de problemas.

### 3. Iniciar la API y la web

Abre dos terminales PowerShell en la raíz del proyecto. En la primera:

```powershell
dotnet run --project src/GoFix.Api --launch-profile GoFix.Api
```

En la segunda:

```powershell
npm.cmd --prefix frontend run dev
```

Abre **https://localhost:3000**. Next.js usa HTTPS de desarrollo y puede solicitar la instalación de un certificado local mediante `mkcert`; acepta la solicitud. El certificado de la API debe ser confiable para Windows y Node. Si aparece un error TLS, revisa los mensajes de ambas terminales y el certificado antes de continuar.

Comprueba [https://localhost:7043/health/ready](https://localhost:7043/health/ready): debe mostrar `Healthy`. Registra un cliente y un técnico desde la aplicación; el técnico debe completar su descripción, especialidad y sector para aparecer en el directorio. Detén cada servidor con `Ctrl+C` en su terminal.

## Estado actual

La base técnica está implementada: solución por capas, host ASP.NET Core, configuración de EF Core para SQL Server, diagnósticos de disponibilidad, errores HTTP con Problem Details, emisión de token antifalsificación, OpenAPI de desarrollo y una interfaz Next.js con React. Next.js se ejecuta en un proceso Node separado y reenvía `/api` al backend HTTPS.

Identidad permite registrar cuentas de cliente y técnico, iniciar y cerrar sesión, consultar la sesión y editar el nombre y el teléfono propios. El registro técnico crea un perfil inicial sin publicar. Los técnicos pueden completar su perfil con descripción, especialidades y sectores activos. El directorio público permite buscar técnicos publicados y consultar su ficha y reputación agregada. Los clientes pueden crear solicitudes desde el perfil de un técnico, consultar sus solicitudes y editar o eliminar las pendientes. Los técnicos pueden consultar las solicitudes que tienen asignadas.

## Stack tecnológico

| Área | Tecnología implementada |
|---|---|
| Backend | C# y ASP.NET Core sobre .NET 10; SDK definido en `global.json` |
| Persistencia | EF Core SQL Server 10.0.11; conexión externa a SQL Server |
| Frontend | Next.js 16.4.6, React 19.3 y TypeScript 6 |
| Contratos y diagnóstico | OpenAPI en desarrollo, Problem Details y Health Checks |
| Dependencias | Versiones y archivos de bloqueo de NuGet y npm versionados |
| Verificación actual | xUnit para aplicación, HTTP y SQL Server; scripts PowerShell |

ASP.NET Core Identity gestiona cuentas, contraseñas y roles. Las versiones exactas se consultan en los archivos de proyecto y `frontend/package.json`.

## Arquitectura y estructura

Monolito modular con Clean Architecture pragmática. Los módulos son Identidad, Profesionales, Servicios y Valoraciones. Las dependencias apuntan hacia el núcleo: Application depende de Domain; Infrastructure implementa los contratos de Application; Api compone los adaptadores y expone HTTP.

| Ruta | Responsabilidad |
|---|---|
| `src/GoFix.Domain` | Entidades, estados e invariantes de negocio |
| `src/GoFix.Application` | Casos de uso, DTO y contratos entre módulos |
| `src/GoFix.Infrastructure` | Persistencia, contexto EF Core y adaptadores |
| `src/GoFix.Api` | Host, endpoints, errores y composición de dependencias |
| `frontend` | Next.js App Router con React y proxy hacia la API |
| `tests` | Comprobaciones ejecutables de arquitectura y HTTP |
| `docs` | Especificaciones, arquitectura y requisitos de referencia |

Domain permanece independiente de frameworks. Los endpoints de negocio invocan casos de uso; los contratos entre módulos no deben exponer `DbSet` ni `IQueryable`.

## API y configuración local

Las instrucciones de instalación y migración están al inicio de este documento. [appsettings.Example.json](src/GoFix.Api/appsettings.Example.json) y [frontend/.env.example](frontend/.env.example) describen configuraciones adicionales.

| Ruta | Comportamiento actual |
|---|---|
| `/health/live` | Comprueba que el proceso responde |
| `/health/ready` | Comprueba conexión a SQL; devuelve 503 sin conexión disponible |
| `GET /api/v1/auth/csrf` | Emite un token para el encabezado `X-CSRF-TOKEN` |
| `POST /api/v1/auth/register` | Crea la cuenta e indica éxito sin abrir sesión |
| `POST /api/v1/auth/login`, `POST /api/v1/auth/logout` | Abren y cierran la sesión |
| `GET /api/v1/auth/me`, `PUT /api/v1/cuenta` | Consultan la cuenta propia y permiten editar nombre y teléfono |
| `GET /api/v1/especialidades`, `GET /api/v1/sectores` | Entregan las opciones activas del catálogo piloto |
| `GET /api/v1/tecnicos/me/perfil`, `PUT /api/v1/tecnicos/me/perfil` | Consultan y editan el perfil del técnico autenticado con control de versión |
| `GET /api/v1/tecnicos`, `GET /api/v1/tecnicos/{id}` | Consultan técnicos publicados y su ficha pública con reputación agregada |
| `/openapi/v1.json` | Contrato disponible únicamente en desarrollo |

Las mutaciones de Identidad requieren el encabezado `X-CSRF-TOKEN` de la sesión actual. La cookie de sesión usa HTTPS y el servidor revoca la sesión al salir. Los valores iniciales del SRS se aplican a esta implementación. La sesión vence a los 60 minutos, la cuenta se bloquea durante 15 minutos tras cinco fallos y registro e inicio de sesión comparten el límite de diez peticiones por minuto e IP.

## Búsqueda pública de técnicos

Accede a `/tecnicos` para filtrar por especialidad y sector. La página conserva los filtros en la URL y permite avanzar por resultados ordenados por nombre e identificador. La ficha `/tecnicos/{id}` muestra descripción, cobertura activa y reputación agregada. Cuando no existen opiniones se presenta «Sin valoraciones».

La API admite `especialidadId`, `sectorId`, `page` (inicial 1) y `pageSize` (inicial 20, máximo 50). Devuelve `items`, `page`, `pageSize` y `total`. Los perfiles incompletos o sin cobertura activa quedan fuera del directorio y su detalle devuelve 404. Las respuestas públicas conservan la privacidad de los datos de cuenta.

El promedio y la cantidad se calculan desde las valoraciones persistidas mediante un contrato del módulo Valoraciones. Los clientes pueden valorar sus servicios completados y consultar las opiniones paginadas en el perfil público del técnico.

## Solicitudes de servicio

Desde la ficha pública de un técnico, selecciona «Solicitar servicio»; sin sesión, el acceso devuelve después al formulario. El cliente puede filtrar sus solicitudes por estado en `/cliente/solicitudes` y abrir el detalle para editar o eliminar una solicitud pendiente. La eliminación pide confirmación y retira la solicitud de ambas listas. El técnico consulta las solicitudes asignadas en `/tecnico/solicitudes`.

La API expone `POST /api/v1/solicitudes` con el encabezado `Idempotency-Key`, y `GET /api/v1/solicitudes` con los filtros `estado`, `page` y `pageSize`. Para el detalle se usa `GET /api/v1/solicitudes/{id}`. La edición usa `PUT` con `version` en el cuerpo y la eliminación usa `DELETE` con `X-Resource-Version`. Si la versión cambió, vuelve a consultar el detalle antes de reintentar.

## Seguimiento de servicios

En el detalle de una solicitud, el técnico puede aceptar con fecha confirmada futura, rechazar con motivo, iniciar y completar el trabajo. El cliente puede cancelar una pendiente y ambos participantes pueden cancelar una aceptada con motivo. Cada acción exige la versión vigente y registra su actor, fecha y cambio de estado en el historial.

El contacto de la contraparte aparece después de la aceptación y se conserva al cancelar un servicio que había sido aceptado. El panel de `/tecnico/panel` muestra pendientes, aceptadas, servicios en proceso y las próximas diez visitas futuras.

La API añade `POST /api/v1/solicitudes/{id}/{aceptar|rechazar|cancelar|iniciar|completar}`, `GET /api/v1/solicitudes/{id}/historial` y `GET /api/v1/tecnicos/me/resumen`. Las transiciones envían `version` y los datos de la acción. Un conflicto HTTP 409 requiere consultar de nuevo el detalle antes de confirmar otra acción.

## Valoraciones

El cliente puede publicar una valoración desde el detalle de su servicio completado. La puntuación es un entero de 1 a 5 y el comentario opcional admite hasta 1.000 caracteres. La confirmación publica una opinión inmutable por solicitud. Un reenvío devuelve conflicto y permite consultar la opinión ya guardada.

El perfil público del técnico muestra opiniones paginadas con nombre de pila del cliente, puntuación, comentario y fecha. El promedio se calcula desde las valoraciones persistidas y se presenta con una cifra decimal.

La API expone `POST /api/v1/solicitudes/{id}/valoracion`, `GET` sobre esa misma ruta para consultar la opinión propia y `GET /api/v1/tecnicos/{id}/valoraciones?page=1&pageSize=20` para las opiniones públicas. La consulta privada devuelve 204 cuando aún no existe una valoración.

## Seguridad y contratos

Las sesiones usan HTTPS, cookies HttpOnly y Secure y una duración absoluta de 60 minutos. Cerrar sesión revoca la fila persistida y rechaza cualquier copia de esa cookie. Las mutaciones requieren el token antifalsificación de la sesión actual.

La API verifica rol y pertenencia de los recursos. Registro e inicio de sesión comparten diez intentos por minuto e IP. Las respuestas 429 incluyen Retry-After. Cinco accesos fallidos consecutivos bloquean la cuenta durante 15 minutos. Detrás de un proxy, configura únicamente las direcciones de confianza descritas en Verificación y publicación.

Los errores de API devuelven Problem Details con code y traceId. El registro estructurado GoFix.Api.Errors conserva estado, código, tipo de excepción y correlación. Se filtran las categorías del framework que pueden registrar URL, valores de entrada o mensajes SQL. El registro sensible de EF permanece desactivado.

Las pruebas verifican expiración, revocación, bloqueo y recuperación, permisos con dos clientes, dos técnicos y visitante, antifalsificación y correlación de errores sin los datos sensibles utilizados en las pruebas.

## Experiencia web

Los formularios conservan los datos ante fallos recuperables y bloquean envíos duplicados mientras se guarda. Cada petición tiene un límite de 30 segundos. Si la sesión vence, la interfaz lleva al acceso y muestra un aviso. Las credenciales no se guardan en el almacenamiento del navegador.

La creación de solicitudes conserva su clave de idempotencia para recuperar una respuesta perdida. Las transiciones con resultado incierto requieren consultar de nuevo el detalle antes de confirmar otra acción. Los conflictos de versión ofrecen recargar los datos actuales.

Los errores reciben foco y se muestran junto a cada campo con su ayuda y contador. Si la sesión vence, el acceso devuelve a la página anterior (solo rutas internas). Existen páginas propias para 404 y errores inesperados, un enlace para saltar al contenido y un menú móvil accesible. Los diálogos permiten navegación por teclado y la interfaz respeta la preferencia de movimiento reducido. Los flujos principales se comprueban a 360, 768 y 1440 px.

Las pruebas de navegador están en `tests/web`. Requieren Python con Playwright, Edge, SQL Server local, el SDK .NET y los certificados HTTPS de desarrollo ya configurados. Instala Playwright en un entorno Python y ejecuta `tests/web/Test-Web.ps1 -Python <ruta-al-python>`. El script crea y elimina una base temporal, inicia los servidores y termina sus procesos al finalizar. Puedes añadir `-ChromePath <chrome.exe>` y `-AxePath <axe.min.js>` para comprobar Chrome y ejecutar la revisión automática de accesibilidad.

## Verificación y publicación

Con la API en ejecución, desde la raíz:

```powershell
powershell -NoProfile -ExecutionPolicy RemoteSigned -File tests/Test-Arquitectura.ps1
powershell -NoProfile -ExecutionPolicy RemoteSigned -File tests/Test-Api.ps1 -BaseUrl https://localhost:7043/ -VerificarOpenApi
npm.cmd --prefix frontend run build
dotnet publish src/GoFix.Api -c Release -o artifacts/publish
```

El smoke espera readiness 503 por defecto; añade `-EstadoReady 200` cuando SQL esté configurado y accesible. La API y Next.js se publican por separado. Para Next.js ejecuta `npm.cmd --prefix frontend run build` y después `npm.cmd --prefix frontend start`; requiere Node.js en ejecución.

Ejecuta la API publicada desde `artifacts/publish`. En despliegue configura TLS en el origen web y `GOFIX_API_TARGET` antes de compilar e iniciar Next.js. El certificado del backend debe ser confiable para Node; no desactives la validación TLS. En desarrollo, Next genera el certificado HTTPS local con `--experimental-https`.

Para limitar intentos por IP detrás de Next en producción, el proxy público debe sobrescribir `X-Forwarded-For` con la IP del cliente y ser el único punto de entrada. Restringe el acceso directo a Next y a la API, y configura `ReverseProxy__KnownAddresses__0` con la IP del proceso Next vista por la API. Con la configuración predeterminada, la API usa la IP de la conexión inmediata.

```powershell
dotnet test GoFix.slnx
```

`GoFix.Api.Tests` comprueba el contrato HTTP, la antifalsificación y la autorización. `GoFix.Integration.Tests` verifica migraciones, registro, edición, sesión e integridad con SQL Server. Configura `GOFIX_TEST_SQLSERVER` con una conexión exclusiva de pruebas que permita crear bases temporales. Las pruebas de Identidad crean y eliminan bases propias con nombre `GoFixIdentityTests_<guid>` y se omiten si falta esa configuración.

El frontend integra el diseño Índigo Medianoche del modelo local: portada con catálogos reales, directorio con filtros, perfiles, paneles de cliente y técnico con un marco común por rol, progreso visual de cada solicitud y formularios adaptables. Inter y Space Grotesk se distribuyen localmente con sus licencias. La navegación usa la sesión y el rol; los datos de demostración del modelo no sustituyen las respuestas de la API. La carpeta `gofix-frontend/` sigue siendo una referencia local ignorada por Git. Las pruebas web incluyen portada, búsqueda y capturas a 360 y 1440 px.

## Entrega y calidad

La copia limpia se comprueba con `tests/Test-EntregaLimpia.ps1 -SqlServer 'lpc:.'`: restaura dependencias bloqueadas, compila .NET y Next.js y aplica las tres migraciones en una base SQL desechable. `tests/Test-Arquitectura.ps1` comprueba dependencias entre capas y módulos; `tests/Test-Api.ps1 -VerificarOpenApi` verifica los contratos HTTP principales. Las pruebas de integración SQL y el recorrido de navegador usan bases temporales independientes.

El ensayo de rendimiento reproducible está en [tests/performance](tests/performance/README.md). El [resultado local](tests/performance/result-2026-11-25.json) del 25 de noviembre de 2026 registró 600,11 s, 20 usuarios, 1.000 técnicos, 143.904 peticiones y cero errores: p95 de 253,8 ms en lecturas y 104,96 ms en escrituras. Estos valores corresponden al equipo y la base aislada descritos en el informe.

La [guía de base de datos](docs/BaseDeDatos.md#respaldo-y-ensayo-de-recuperación-rnf-09) explica el respaldo previo a migraciones, el job diario de SQL Server Agent y el ensayo de restauración aislada. La instancia local ejecutó el job correctamente. El objetivo propuesto de RPO de 24 h y RTO de 4 h en otro entorno requiere vigilar fallos, copiar respaldos fuera del servidor y repetir el ensayo con sus datos y hardware.
## Contribución e integración

Cada cambio parte de `dev` actualizado en una rama `feature/` o `hotfix/`. La misma rama se integra mediante PR primero a `dev`, después a `qa` y finalmente a `main`, fusionando cada PR con merge commit antes de abrir el siguiente. Se conservan las ramas como evidencia de la práctica. No se trabaja directamente en las ramas permanentes ni se utilizan commits vacíos o force push.

## Documentación

- [Especificaciones](docs/Especificaciones.md): alcance, incrementos y decisiones de producto.
- [Arquitectura](docs/Arquitectura.md): límites, persistencia, seguridad y observaciones técnicas.
- [SRS](docs/SRS.md): requisitos, reglas y criterios de aceptación.

Estos documentos describen el diseño esperado; su contenido propuesto no acredita funcionalidad implementada ni aprobación de políticas pendientes.
