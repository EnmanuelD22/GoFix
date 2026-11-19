# GoFix

GoFix conecta a clientes con técnicos para solicitar y seguir servicios. La solución usa React y TypeScript, una API ASP.NET Core y SQL Server. Esta rama incorpora la base ejecutable del monolito modular. El registro y los perfiles son el primer incremento funcional previsto.

La [arquitectura](docs/Arquitectura.md) documenta los límites del sistema. [Especificaciones](docs/Especificaciones.md) define el alcance y los incrementos, y el [SRS](docs/SRS.md) relaciona cada requisito con su evidencia de aceptación.

## Estructura

| Proyecto | Responsabilidad |
|---|---|
| `src/GoFix.Domain` | Entidades e invariantes de negocio, independiente de frameworks. |
| `src/GoFix.Application` | Casos de uso y puertos, con dependencia de Domain. |
| `src/GoFix.Infrastructure` | Adaptadores de persistencia e identidad, con dependencias de Application y Domain. |
| `src/GoFix.Api` | Contratos HTTP y composición de dependencias. |
| `frontend` | SPA React y cliente HTTP. |
| `tests` | Comprobaciones de arquitectura y del host HTTP. |

Los módulos de Identidad, Profesionales, Servicios y Valoraciones se organizan dentro de las capas a medida que se implementan. Los casos de uso controlan el acceso a sus datos y exponen contratos acotados a otros módulos.

## Desarrollo local

Instala el SDK indicado en [global.json](global.json) y una versión de Node compatible con [frontend/package.json](frontend/package.json). Los ejemplos usan PowerShell.

```powershell
dotnet restore GoFix.slnx --locked-mode
dotnet build GoFix.slnx --no-restore
dotnet dev-certs https --trust
```

Inicia el backend en un terminal y la SPA en otro. Abre `http://localhost:5173`. El proxy de Vite mantiene las llamadas del navegador bajo el mismo origen y se conecta a la API en `https://localhost:7043`. Si cambias la URL del backend, consulta [el ejemplo de configuración de Vite](frontend/.env.example).

```powershell
dotnet run --project src/GoFix.Api
```

```powershell
Set-Location frontend
npm.cmd ci
npm.cmd run dev
```

La configuración de SQL Server es externa. Usa User Secrets en desarrollo y la variable `ConnectionStrings__GoFix` o un proveedor de secretos en despliegue. Consulta [la configuración de ejemplo](src/GoFix.Api/appsettings.Example.json) y guarda las llaves de Data Protection en un directorio protegido fuera de la publicación. `/health/live` comprueba el proceso y `/health/ready` indica si la API puede conectar a la base configurada. Sin una conexión válida, la segunda ruta devuelve 503. El contrato OpenAPI se expone en desarrollo.

## Verificación y publicación

Ejecuta las comprobaciones desde la raíz. Para el smoke HTTP, ajusta la URL al host levantado y usa `-EstadoReady 200` si hay una base configurada y accesible.

```powershell
powershell -NoProfile -ExecutionPolicy RemoteSigned -File tests/Test-Arquitectura.ps1
powershell -NoProfile -ExecutionPolicy RemoteSigned -File tests/Test-Api.ps1 -BaseUrl https://localhost:7043/ -VerificarOpenApi
npm.cmd --prefix frontend run build
dotnet publish src/GoFix.Api -c Release -o artifacts/publish
```

Compila la SPA antes de publicar el backend. La publicación incluye `frontend/dist` en `artifacts/publish/wwwroot` para servir la SPA y la API bajo el mismo origen. Configura TLS y ejecuta el smoke sobre la publicación con `-VerificarProduccion` en lugar de `-VerificarOpenApi`. Cuando se incorporen migraciones, aplícalas con credenciales de operación mediante un paso separado y con respaldo previo. La base arquitectónica comprueba conectividad y aún carece de un esquema funcional.

## Integración

Cada incremento parte de `dev` en una rama de trabajo. Se valida y se integra con *merge commit* mediante PR desde esa misma rama a `dev`, después a `qa` y por último a `main`. Las ramas se conservan hasta completar la evidencia de la práctica.
