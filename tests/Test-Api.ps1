[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [uri]$BaseUrl,

    [ValidateSet(200, 503)]
    [int]$EstadoReady = 503,

    [switch]$VerificarOpenApi
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

function Assert-EstadoHttp {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$Path,
        [int]$Status
    )

    $response = $Client.GetAsync($Path).GetAwaiter().GetResult()
    if ([int]$response.StatusCode -ne $Status) {
        $actualStatus = [int]$response.StatusCode
        $response.Dispose()
        throw "GET /$Path esperaba HTTP $Status y obtuvo HTTP $actualStatus."
    }

    return $response
}

if (-not $BaseUrl.IsAbsoluteUri -or $BaseUrl.Scheme -notin @('http', 'https') -or $BaseUrl.AbsolutePath -ne '/' -or $BaseUrl.Query -or $BaseUrl.Fragment) {
    throw 'BaseUrl debe ser el origen HTTP o HTTPS de GoFix, sin ruta, consulta ni fragmento.'
}

# Una redireccion ocultaria una respuesta incorrecta o el fallback a la SPA.
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$client = [System.Net.Http.HttpClient]::new($handler)
$client.BaseAddress = $BaseUrl
$client.Timeout = [TimeSpan]::FromSeconds(15)
$client.DefaultRequestHeaders.Accept.ParseAdd('application/problem+json, application/json')

try {
    (Assert-EstadoHttp $client 'health/live' 200).Dispose()
    (Assert-EstadoHttp $client 'health/ready' $EstadoReady).Dispose()

    $missing = Assert-EstadoHttp $client "api/v1/$([guid]::NewGuid())" 404
    try {
        if ($missing.Content.Headers.ContentType.MediaType -ne 'application/problem+json') {
            throw 'Una ruta API inexistente debe devolver Problem Details como JSON.'
        }
        $problem = $missing.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        if ($problem.status -ne 404 -or [string]::IsNullOrWhiteSpace($problem.code) -or [string]::IsNullOrWhiteSpace($problem.traceId)) {
            throw 'Problem Details debe incluir status 404, code y traceId.'
        }
    } finally {
        $missing.Dispose()
    }

    if ($BaseUrl.Scheme -eq 'https') {
        $csrf = Assert-EstadoHttp $client 'api/v1/auth/csrf' 200
        try {
            $payload = $csrf.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
            if ([string]::IsNullOrWhiteSpace($payload.token) -or -not $csrf.Headers.CacheControl.NoStore) {
                throw 'La respuesta antifalsificacion debe incluir un token y prohibir su almacenamiento en cache.'
            }
            if (-not $csrf.Headers.Contains('Set-Cookie')) {
                throw 'Falta la cookie antifalsificacion.'
            }
            $cookies = @($csrf.Headers.GetValues('Set-Cookie') | Where-Object {
                $_ -match '(?:^|;\s*)Secure(?:;|$)' -and $_ -match '(?:^|;\s*)HttpOnly(?:;|$)'
            })
            if ($cookies.Count -eq 0) {
                throw 'La cookie antifalsificacion debe ser Secure y HttpOnly.'
            }
        } finally {
            $csrf.Dispose()
        }
        Write-Host 'Emision del token antifalsificacion por HTTPS aprobada.'
    }

    if ($VerificarOpenApi) {
        $openApi = Assert-EstadoHttp $client 'openapi/v1.json' 200
        try {
            $document = $openApi.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
            if ([string]::IsNullOrWhiteSpace($document.openapi) -or -not $document.paths.'/api/v1/auth/csrf'.get) {
                throw 'OpenAPI debe documentar la operacion antifalsificacion.'
            }
        } finally {
            $openApi.Dispose()
        }
        Write-Host 'Contrato OpenAPI de desarrollo aprobado.'
    }

    Write-Host 'Verificacion HTTP aprobada: liveness, readiness y error de API.'
} finally {
    $client.Dispose()
}
