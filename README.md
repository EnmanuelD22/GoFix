# GoFix

Plataforma web para conectar clientes con técnicos de su zona. El producto contempla búsqueda por especialidad y sector, solicitudes de asistencia, seguimiento del servicio y valoraciones. La primera versión está orientada a un municipio piloto, con interfaz en español; pagos, chat y mapas quedan fuera de su alcance inicial.

## Estado actual

La base técnica está implementada: solución por capas, host ASP.NET Core, configuración de EF Core para SQL Server, diagnósticos de disponibilidad, errores HTTP con Problem Details, emisión de token antifalsificación, OpenAPI de desarrollo y una página inicial Next.js con React. Next.js se ejecuta en un proceso Node separado y reenvía `/api` al backend HTTPS.

Identidad permite registrar cuentas de cliente y técnico, iniciar y cerrar sesión, consultar la sesión y editar el nombre y el teléfono propios. El registro técnico crea un perfil inicial sin publicar. La edición profesional, los catálogos, la búsqueda y el CRUD de servicios siguen en la lista de implementación.

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
- [ ] **1. Profesionales y catálogos (RF-04 y RF-05).** Edición y publicación del perfil profesional, selección de especialidades y sectores activos, y carga inicial de los catálogos.
- [ ] **2. Consulta de profesionales (RF-06 y RF-07).** Búsqueda paginada por especialidad y sector, perfil público y reputación sin exponer contactos privados.
- [ ] **3. Servicios, solicitudes (RF-08 a RF-11 y RF-24).** Alta idempotente, consulta, edición y eliminación lógica de solicitudes propias pendientes.
- [ ] **4. Servicios, seguimiento (RF-12 a RF-18, RF-21 y RF-23).** Transiciones por rol, historial, visibilidad de contacto y control de concurrencia sobre SQL Server.
- [ ] **5. Valoraciones (RF-19 y RF-20).** Una opinión por servicio completado, con permisos, promedio y cantidad de opiniones.
- [ ] **Seguridad y contratos (RNF-01 a RNF-03 y RNF-10).** Sesión, autorización por recurso, antifalsificación, protección de acceso y errores correlacionados.
- [ ] **Experiencia web (RF-22, RNF-06, RNF-07 y RNF-12).** Estados de carga, errores, accesibilidad, adaptación y prevención del doble envío.
- [ ] **Entrega y calidad (RNF-04, RNF-05, RNF-08, RNF-09 y RNF-11).** Migraciones reproducibles, límites entre módulos, recorrido completo, pruebas sobre SQL Server y procedimientos de operación.

Cada incremento debe incluir sus migraciones, contratos HTTP, pantallas y pruebas aplicables. Las pruebas con SQL Server real verifican restricciones, transacciones y concurrencia.

### Decisiones previas a la implementación

El responsable aprobó usar `PerfilTecnicoId` como identificador público y referencia del técnico. Las migraciones crearán las tablas de catálogos y un script SQL versionado e idempotente cargará los valores iniciales aprobados. Quedan por decidir el municipio, las especialidades y los sectores iniciales. Siguen abiertas las observaciones sobre tipos temporales, expiración de sesión, ubicación de valoraciones y catálogo de errores (ARQ-H2, H4, H5 y H6), además de las políticas y metas propuestas en Especificaciones y SRS.

La rama histórica `feature/usuarios-perfiles` entregó la base técnica y ya completó sus tres PR. El trabajo funcional pendiente requiere ramas nuevas desde `dev` actualizado, con nombres que describan su contenido.

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

Abre `https://localhost:3000`. Usa `/registro`, `/acceso` y `/cuenta` para gestionar la cuenta. Next.js envía `/api` a `https://localhost:7043`. La configuración alternativa está en [frontend/.env.example](frontend/.env.example).

| Ruta | Comportamiento actual |
|---|---|
| `/health/live` | Comprueba que el proceso responde |
| `/health/ready` | Comprueba conexión a SQL; devuelve 503 sin conexión disponible |
| `GET /api/v1/auth/csrf` | Emite un token para el encabezado `X-CSRF-TOKEN` |
| `POST /api/v1/auth/register` | Crea la cuenta e indica éxito sin abrir sesión |
| `POST /api/v1/auth/login`, `POST /api/v1/auth/logout` | Abren y cierran la sesión |
| `GET /api/v1/auth/me`, `PUT /api/v1/cuenta` | Consultan la cuenta propia y permiten editar nombre y teléfono |
| `/openapi/v1.json` | Contrato disponible únicamente en desarrollo |

Las mutaciones de Identidad requieren el encabezado `X-CSRF-TOKEN` de la sesión actual. La cookie de sesión usa HTTPS y el servidor revoca la sesión al salir. Los valores iniciales del SRS se aplican a esta implementación. La sesión vence a los 60 minutos, la cuenta se bloquea durante 15 minutos tras cinco fallos y registro e inicio de sesión comparten el límite de diez peticiones por minuto e IP.

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

## Contribución e integración

Cada cambio parte de `dev` actualizado en una rama `feature/` o `hotfix/`. La misma rama se integra mediante PR primero a `dev`, después a `qa` y finalmente a `main`, fusionando cada PR con merge commit antes de abrir el siguiente. Se conservan las ramas como evidencia de la práctica. No se trabaja directamente en las ramas permanentes ni se utilizan commits vacíos o force push.

## Documentación

- [Especificaciones](docs/Especificaciones.md): alcance, incrementos y decisiones de producto.
- [Arquitectura](docs/Arquitectura.md): límites, persistencia, seguridad y observaciones técnicas.
- [SRS](docs/SRS.md): requisitos, reglas y criterios de aceptación.

Estos documentos describen el diseño esperado; su contenido propuesto no acredita funcionalidad implementada ni aprobación de políticas pendientes.
