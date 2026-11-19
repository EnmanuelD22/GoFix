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
