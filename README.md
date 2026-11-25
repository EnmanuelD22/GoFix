# GoFix

Plataforma web para conectar clientes con técnicos de su zona. El producto contempla búsqueda por especialidad y sector, solicitudes de asistencia, seguimiento del servicio y valoraciones. La primera versión está orientada a un municipio piloto, con interfaz en español; pagos, chat y mapas quedan fuera de su alcance inicial.

## Estado actual

La base técnica está implementada: solución por capas, host ASP.NET Core, configuración de EF Core para SQL Server, diagnósticos de disponibilidad, errores HTTP con Problem Details, emisión de token antifalsificación, OpenAPI de desarrollo y una página inicial Next.js con React. Next.js se ejecuta en un proceso Node separado y reenvía `/api` al backend HTTPS.

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

Monolito modular con Clean Architecture pragmática. Los módulos previstos son Identidad, Profesionales, Servicios y Valoraciones. Las dependencias apuntan hacia el núcleo: Application depende de Domain; Infrastructure implementa los contratos de Application; Api compone los adaptadores y expone HTTP.

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

## Plan de implementación por módulos

Marca cada casilla cuando el alcance indicado esté implementado y verificado. Los criterios detallados están en el SRS.

- [x] **1. Identidad (RF-01 a RF-03).** Registro de cliente o técnico, alta atómica del perfil técnico inicial, acceso y salida con cookie, consulta de sesión y edición de la cuenta propia.
- [x] **1. Profesionales y catálogos (RF-04 y RF-05).** Edición y publicación del perfil profesional, selección de especialidades y sectores activos, y carga inicial de los catálogos.
- [x] **2. Consulta de profesionales (RF-06 y RF-07).** Búsqueda paginada por especialidad y sector, perfil público y reputación sin exponer contactos privados.
- [x] **3. Servicios, solicitudes (RF-08 a RF-11 y RF-24).** Alta idempotente, consulta, edición y eliminación lógica de solicitudes propias pendientes.
- [x] **4. Servicios, seguimiento (RF-12 a RF-18, RF-21 y RF-23).** Transiciones por rol, historial, visibilidad de contacto y control de concurrencia sobre SQL Server.
- [x] **5. Valoraciones (RF-19 y RF-20).** Una opinión por servicio completado, con permisos, promedio y cantidad de opiniones.
- [x] **Seguridad y contratos (RNF-01 a RNF-03 y RNF-10).** Sesión, autorización por recurso, antifalsificación, protección de acceso y errores correlacionados.
- [x] **Experiencia web (RF-22, RNF-06, RNF-07 y RNF-12).** Estados de carga, errores, accesibilidad, adaptación y prevención del doble envío.
- [x] **Entrega y calidad (RNF-04, RNF-05, RNF-08, RNF-09 y RNF-11).** Migraciones reproducibles, límites entre módulos, recorrido completo, pruebas sobre SQL Server y procedimientos de operación.

Cada incremento debe incluir sus migraciones, contratos HTTP, pantallas y pruebas aplicables. Las pruebas con SQL Server real verifican restricciones, transacciones y concurrencia.

### Decisiones previas a la implementación

El responsable aprobó usar `PerfilTecnicoId` como identificador público y referencia del técnico. Las migraciones crearán las tablas de catálogos y un script SQL versionado e idempotente cargará los valores iniciales aprobados. El piloto seleccionado por delegación del responsable usa Santo Domingo de Guzmán, seis especialidades y cuatro sectores. La carga está en `scripts/sql/LoadInitialCatalogs.sql`. Las observaciones ARQ-H2, H4 y H5 tienen soluciones implementadas; ARQ-H6 cuenta con códigos de error, pero su catálogo estable requiere revisión. Las políticas y metas propuestas en Especificaciones y SRS siguen sujetas a validación de producto.

La rama histórica `feature/usuarios-perfiles` entregó la base técnica y ya completó sus tres PR. Los incrementos posteriores se desarrollaron en ramas nuevas desde `dev`, con nombres que describen su contenido.

## Desarrollo local

Requisitos: SDK indicado en `global.json`, Node.js 24 o superior compatible con el frontend, npm y PowerShell para los scripts. SQL Server es necesario para obtener readiness saludable y ejecutar los flujos de Identidad.

Desde la raíz:

```powershell
dotnet restore GoFix.slnx --locked-mode
dotnet build GoFix.slnx --no-restore
npm.cmd --prefix frontend ci
dotnet dev-certs https --trust
```

Configura `ConnectionStrings:GoFix` mediante User Secrets en desarrollo, o `ConnectionStrings__GoFix` en el entorno de despliegue. [appsettings.Example.json](src/GoFix.Api/appsettings.Example.json) muestra las claves admitidas sin credenciales reales. Los secretos y las claves persistidas de Data Protection deben almacenarse fuera del repositorio y de la publicación.

Con la conexión configurada, aplica las migraciones a la base seleccionada. Crean el esquema de Identidad, los catálogos, la cobertura técnica, las solicitudes, el historial, la idempotencia y las valoraciones. Usa credenciales de migración en el paso de despliegue correspondiente.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/GoFix.Infrastructure --startup-project src/GoFix.Api
```

Consulta la [guía de base de datos](docs/BaseDeDatos.md) para revisar el esquema, generar el SQL de despliegue y cargar los catálogos cuando se aprueben sus valores.

Inicia la API y el frontend en terminales separadas.

```powershell
dotnet run --project src/GoFix.Api --launch-profile GoFix.Api
```

```powershell
npm.cmd --prefix frontend run dev
```

Abre `https://localhost:3000`. Usa `/registro`, `/acceso` y `/cuenta` para gestionar la cuenta. Los técnicos pueden editar su descripción y cobertura en `/tecnico/perfil`. Next.js envía `/api` a `https://localhost:7043`. La configuración alternativa está en [frontend/.env.example](frontend/.env.example).

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

Desde la ficha pública de un técnico, selecciona «Solicitar servicio». El cliente puede filtrar sus solicitudes por estado en `/cliente/solicitudes` y abrir el detalle para editar o eliminar una solicitud pendiente. La eliminación pide confirmación y retira la solicitud de ambas listas. El técnico consulta las solicitudes asignadas en `/tecnico/solicitudes`.

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

La API verifica rol y pertenencia de los recursos. Registro e inicio de sesión comparten diez intentos por minuto e IP. Las respuestas 429 incluyen Retry-After. Cinco accesos fallidos consecutivos bloquean la cuenta durante 15 minutos. Detrás de un proxy, configura únicamente las direcciones de confianza descritas en Desarrollo local.

Los errores de API devuelven Problem Details con code y traceId. El registro estructurado GoFix.Api.Errors conserva estado, código, tipo de excepción y correlación. Se filtran las categorías del framework que pueden registrar URL, valores de entrada o mensajes SQL. El registro sensible de EF permanece desactivado.

Las pruebas verifican expiración, revocación, bloqueo y recuperación, permisos con dos clientes, dos técnicos y visitante, antifalsificación y correlación de errores sin los datos sensibles utilizados en las pruebas.

## Experiencia web

Los formularios conservan los datos ante fallos recuperables y bloquean envíos duplicados mientras se guarda. Cada petición tiene un límite de 30 segundos. Si la sesión vence, la interfaz lleva al acceso y muestra un aviso. Las credenciales no se guardan en el almacenamiento del navegador.

La creación de solicitudes conserva su clave de idempotencia para recuperar una respuesta perdida. Las transiciones con resultado incierto requieren consultar de nuevo el detalle antes de confirmar otra acción. Los conflictos de versión ofrecen recargar los datos actuales.

Los errores reciben foco y se asocian a los campos correspondientes. Los diálogos permiten navegación por teclado y la interfaz respeta la preferencia de movimiento reducido. Los flujos principales se comprueban a 360, 768 y 1440 px.

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

El frontend integra el diseño Índigo Medianoche del modelo local: portada con catálogos reales, directorio con filtros, perfiles, panel técnico y formularios adaptables. Inter y Space Grotesk se distribuyen localmente con sus licencias. La navegación usa la sesión y el rol; los datos de demostración del modelo no sustituyen las respuestas de la API. La carpeta `gofix-frontend/` sigue siendo una referencia local ignorada por Git. Las pruebas web incluyen portada, búsqueda y capturas a 360 y 1440 px.

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
