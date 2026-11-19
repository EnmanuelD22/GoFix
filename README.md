# GoFix

Plataforma web para conectar clientes con técnicos de su zona. El producto contempla búsqueda por especialidad y sector, solicitudes de asistencia, seguimiento del servicio y valoraciones. La primera versión está orientada a un municipio piloto, con interfaz en español; pagos, chat y mapas quedan fuera de su alcance inicial.

## Estado actual

La base técnica está implementada: solución por capas, host ASP.NET Core, configuración de EF Core para SQL Server, diagnósticos de disponibilidad, errores HTTP con Problem Details, emisión de token antifalsificación, OpenAPI de desarrollo y una página inicial Next.js con React. Next.js se ejecuta en un proceso Node separado y reenvía `/api` al backend HTTPS.

Las capas contienen carpetas `Modules/Identity`, `Modules/Professionals`, `Modules/Services` y `Modules/Reviews`. Domain y Application contienen la estructura de proyectos, pero todavía no incluyen entidades ni casos de uso. No hay Identity configurado, migraciones, esquema funcional, registro, perfiles, búsqueda ni CRUD de servicios. La comprobación de conectividad SQL no acredita la integridad del futuro modelo de negocio.

## Stack tecnológico

| Área | Tecnología implementada |
|---|---|
| Backend | C# y ASP.NET Core sobre .NET 10; SDK definido en `global.json` |
| Persistencia | EF Core SQL Server 10.0.11; conexión externa a SQL Server |
| Frontend | Next.js 16.4.6, React 19.3 y TypeScript 6 |
| Contratos y diagnóstico | OpenAPI en desarrollo, Problem Details y Health Checks |
| Dependencias | Versiones y archivos de bloqueo de NuGet y npm versionados |
| Verificación actual | xUnit para HTTP y SQL Server; proyecto de pruebas de aplicación preparado; scripts PowerShell |

ASP.NET Core Identity forma parte del diseño pendiente de implementar. Las versiones exactas se consultan en los archivos de proyecto y `frontend/package.json`.

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

Domain permanece independiente de frameworks. Los futuros endpoints deben invocar casos de uso; los contratos entre módulos no deben exponer `DbSet` ni `IQueryable`.

## Plan de implementación por módulos

La tabla describe trabajo pendiente, no funcionalidades entregadas. El orden sigue las dependencias del producto y el plan de Especificaciones; los criterios detallados permanecen en el SRS.

| Orden / módulo | Backend y datos pendientes | Interfaz pendiente | Evidencia de terminación |
|---|---|---|---|
| 1. Identidad | Identity, roles cliente/técnico, registro atómico, sesión por cookie, cierre de sesión y edición de cuenta | Registro, acceso y edición de cuenta | RF-01 a RF-03: validación, unicidad de correo, roles, sesión y rollback ante registro incompleto |
| 1. Profesionales y catálogos | Perfil técnico, especialidades, sectores, asociaciones y completitud; carga inicial de catálogos y migraciones | Edición de descripción y cobertura | RF-04 y RF-05: publicación según completitud y persistencia consistente de perfil y cuenta |
| 2. Consulta de profesionales | Consultas filtradas y paginadas, ficha pública y contrato de reputación inicialmente sin opiniones | Buscador, filtros, resultados y perfil público | RF-06 y RF-07: filtros combinados, paginación, ausencia de contactos privados y perfiles no publicados |
| 3. Servicios: solicitudes | Agregado, repositorios, creación, consulta, edición y eliminación lógica de pendientes; historial inicial, idempotencia y rowversion | Solicitar asistencia, listado, detalle y edición | RF-08 a RF-11 y RF-24: CRUD propio, acceso cruzado rechazado y creación única ante reenvíos |
| 4. Servicios: seguimiento | Aceptar, rechazar, cancelar, iniciar y completar; transacciones de estado e historial, autorización y visibilidad de contacto | Paneles por rol, acciones permitidas e historial | RF-12 a RF-18, RF-21 y RF-23: matriz de transiciones y carreras concurrentes sobre SQL Server |
| 5. Valoraciones | Elegibilidad, puntuación, unicidad por servicio y consulta de reputación | Formulario de valoración y opiniones del técnico | RF-19 y RF-20: una opinión por servicio completado, permisos y promedio/cantidad correctos |
| Transversal: seguridad y contratos | Autorización por recurso, cookies de sesión, validación antifalsificación en mutaciones, límites de acceso y catálogo de errores | Cliente HTTP, manejo de sesión y permisos | RNF-01 a RNF-03 y RNF-10: respuestas seguras, correlación y ausencia de datos sensibles en logs |
| Transversal: experiencia web | Contratos de carga, validación y conflictos para cada operación | Estados vacíos, errores, accesibilidad, adaptación y prevención de doble envío | RF-22, RNF-06, RNF-07 y RNF-12: teclado, tamaños de pantalla y fallos recuperables |
| Transversal: entrega y calidad | Migraciones reproducibles, pruebas por módulo, límites entre módulos, publicación y procedimientos operativos | Recorrido completo cliente–técnico | RNF-04, RNF-05, RNF-08, RNF-09 y RNF-11: instalación limpia, SQL real, medición y restauración documentadas |

Cada incremento debe incluir sus migraciones, contratos HTTP, pantallas y pruebas aplicables. Las pruebas con SQL Server real son necesarias para demostrar restricciones, transacciones y concurrencia; los scripts actuales no cubren esos comportamientos.

### Decisiones previas a la implementación

Los documentos distinguen acuerdos, propuestas y observaciones sin aprobar. Antes del primer incremento funcional deben resolverse ARQ-H1 (identificador público del técnico) y ARQ-H3 (procedimiento y contenido inicial de catálogos). También siguen abiertas las observaciones sobre tipos temporales, expiración de sesión, ubicación de valoraciones y catálogo de errores (ARQ-H2, H4, H5 y H6), además de las políticas y metas propuestas en Especificaciones y SRS.

La rama histórica `feature/usuarios-perfiles` entregó la base técnica y ya completó sus tres PR. El trabajo funcional pendiente requiere ramas nuevas desde `dev` actualizado, con nombres que describan su contenido.

## Desarrollo local

Requisitos: SDK indicado en `global.json`, Node.js 24 o superior compatible con el frontend, npm y PowerShell para los scripts. SQL Server es necesario para obtener readiness saludable y, cuando existan migraciones, ejecutar los flujos persistidos.

Desde la raíz:

```powershell
dotnet restore GoFix.slnx --locked-mode
dotnet build GoFix.slnx --no-restore
npm.cmd --prefix frontend ci
dotnet dev-certs https --trust
```

Inicia la API y el frontend en terminales separadas:

```powershell
dotnet run --project src/GoFix.Api --launch-profile GoFix.Api
```

```powershell
npm.cmd --prefix frontend run dev
```

Abre `https://localhost:3000`. Next.js envía `/api` a `https://localhost:7043`; la configuración alternativa se documenta en [frontend/.env.example](frontend/.env.example).

Configura `ConnectionStrings:GoFix` mediante User Secrets en desarrollo, o `ConnectionStrings__GoFix` en el entorno de despliegue. [appsettings.Example.json](src/GoFix.Api/appsettings.Example.json) muestra las claves admitidas sin credenciales reales. Los secretos y las claves persistidas de Data Protection deben almacenarse fuera del repositorio y de la publicación.

| Ruta | Comportamiento actual |
|---|---|
| `/health/live` | Comprueba que el proceso responde |
| `/health/ready` | Comprueba conexión a SQL; devuelve 503 sin conexión disponible |
| `/api/v1/auth/csrf` | Emite token antifalsificación por HTTPS |
| `/openapi/v1.json` | Contrato disponible únicamente en desarrollo |

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

```powershell
dotnet test GoFix.slnx
```

`GoFix.Api.Tests` comprueba errores y antifalsificación mediante el host HTTP de prueba. `GoFix.Application.Tests` recibirá pruebas al incorporar casos de uso. `GoFix.Integration.Tests` comprueba conectividad de solo lectura con una base exclusiva indicada por `GOFIX_TEST_SQLSERVER`; sin ella la prueba se omite. No crea ni elimina bases y no verifica todavía transacciones o migraciones. Las migraciones se incorporarán en un incremento posterior.

## Contribución e integración

Cada cambio parte de `dev` actualizado en una rama `feature/` o `hotfix/`. La misma rama se integra mediante PR primero a `dev`, después a `qa` y finalmente a `main`, fusionando cada PR con merge commit antes de abrir el siguiente. Se conservan las ramas como evidencia de la práctica. No se trabaja directamente en las ramas permanentes ni se utilizan commits vacíos o force push.

## Documentación

- [Especificaciones](docs/Especificaciones.md): alcance, incrementos y decisiones de producto.
- [Arquitectura](docs/Arquitectura.md): límites, persistencia, seguridad y observaciones técnicas.
- [SRS](docs/SRS.md): requisitos, reglas y criterios de aceptación.

Estos documentos describen el diseño esperado; su contenido propuesto no acredita funcionalidad implementada ni aprobación de políticas pendientes.
