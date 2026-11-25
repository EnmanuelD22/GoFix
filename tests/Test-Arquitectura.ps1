[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$sourceRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'src'

# Evalua las referencias efectivas, incluidas las importadas desde archivos .props.
$allowedReferences = @{
    'GoFix.Domain'         = @()
    'GoFix.Application'    = @('GoFix.Domain')
    'GoFix.Infrastructure' = @('GoFix.Application', 'GoFix.Domain')
    'GoFix.Api'            = @('GoFix.Application', 'GoFix.Infrastructure')
}

foreach ($name in ($allowedReferences.Keys | Sort-Object)) {
    $project = Join-Path $sourceRoot "$name/$name.csproj"
    if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
        throw "No existe el proyecto esperado: $project."
    }

    $msbuildOutput = & dotnet msbuild $project -nologo '-getItem:ProjectReference,PackageReference,FrameworkReference,Reference'
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild no pudo evaluar las dependencias de $name."
    }

    $items = ($msbuildOutput -join "`n" | ConvertFrom-Json).Items
    $allowedPaths = @($allowedReferences[$name] | ForEach-Object {
        [IO.Path]::GetFullPath((Join-Path $sourceRoot "$_/$_.csproj"))
    })

    foreach ($reference in $items.ProjectReference) {
        if ($reference.FullPath -notin $allowedPaths) {
            throw "$name tiene una referencia de proyecto no permitida: $($reference.Identity)."
        }
    }

    if ($name -eq 'GoFix.Domain') {
        $externalFrameworks = @($items.FrameworkReference | Where-Object { $_.Identity -ne 'Microsoft.NETCore.App' })
        if ($items.PackageReference.Count -gt 0 -or $items.Reference.Count -gt 0 -or $externalFrameworks.Count -gt 0) {
            throw 'GoFix.Domain debe depender solo de la biblioteca base de .NET.'
        }
    }

    Write-Host "Dependencias aprobadas: $name"
}

# Los casos de uso y endpoints no deben acoplarse a la persistencia concreta.
$forbiddenImports = @{
    'GoFix.Application' = 'GoFix\.(Infrastructure|Api)(\.|;)'
    'GoFix.Domain' = 'GoFix\.(Application|Infrastructure|Api)(\.|;)'
}
foreach ($project in $forbiddenImports.Keys) {
    $moduleRoot = Join-Path $sourceRoot "$project/Modules"
    foreach ($file in (Get-ChildItem -LiteralPath $moduleRoot -Recurse -Filter '*.cs' -File)) {
        if (Select-String -LiteralPath $file.FullName -Pattern $forbiddenImports[$project] -Quiet) {
            throw "$project tiene una dependencia prohibida en $($file.FullName)."
        }
    }
}
foreach ($file in (Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'GoFix.Api/Modules') -Recurse -Filter '*.cs' -File)) {
    if (Select-String -LiteralPath $file.FullName -Pattern 'GoFix\.Infrastructure(\.|;)|\bGoFixDbContext\b|\bDbSet\s*<' -Quiet) {
        throw "Un endpoint usa persistencia concreta: $($file.FullName)."
    }
}
Write-Host 'Limites de modulos aprobados: casos de uso y endpoints sin persistencia concreta.'
