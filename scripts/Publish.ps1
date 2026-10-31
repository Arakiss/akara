[CmdletBinding()]
param(
    [string]$DotnetPath,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$sourceRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if (-not $DotnetPath) {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnetCommand) { $DotnetPath = $dotnetCommand.Source }
}
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $sourceRoot 'artifacts\AKARA' }
$webRoot = Join-Path $sourceRoot 'src\ForjaHoradrica.Web'
$serverRoot = Join-Path $sourceRoot 'src\ForjaHoradrica.Server'
$serverProject = Join-Path $serverRoot 'ForjaHoradrica.Server.csproj'
$cliProject = Join-Path $sourceRoot 'src\Akara.Cli\Akara.Cli.csproj'
if (-not $DotnetPath -or -not (Test-Path -LiteralPath $DotnetPath)) { throw 'Instala el SDK de .NET 10 o indica su ruta con -DotnetPath.' }
Push-Location -LiteralPath $webRoot
try {
    & npm.cmd ci --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron preparar las dependencias de la interfaz.' }
    & npm.cmd run build
    if ($LASTEXITCODE -ne 0) { throw 'La interfaz no compila.' }
} finally { Pop-Location }
$distRoot = Join-Path $webRoot 'dist'
if (-not (Test-Path -LiteralPath (Join-Path $distRoot 'index.html'))) { throw 'Falta la interfaz compilada.' }
$wwwRoot = [IO.Path]::GetFullPath((Join-Path $serverRoot 'wwwroot'))
if (-not $wwwRoot.StartsWith($sourceRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Destino de recursos fuera del proyecto.' }
if (Test-Path -LiteralPath $wwwRoot) {
    Get-ChildItem -LiteralPath $wwwRoot -Force | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Recurse -Force }
} else { New-Item -ItemType Directory -Path $wwwRoot | Out-Null }
Get-ChildItem -LiteralPath $distRoot -Force | Copy-Item -Destination $wwwRoot -Recurse -Force
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
& $DotnetPath publish $serverProject -c Release -r win-x64 --self-contained true --artifacts-path (Join-Path $sourceRoot 'artifacts\build') -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'No se pudo publicar AKARA.' }
& $DotnetPath publish $cliProject -c Release -r win-x64 --self-contained true --artifacts-path (Join-Path $sourceRoot 'artifacts\build') -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'No se pudo publicar AKARA CLI.' }
Copy-Item -LiteralPath (Join-Path $sourceRoot 'LEEME.txt') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $sourceRoot 'LICENSE') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $sourceRoot 'licenses') -Destination $OutputDirectory -Recurse -Force
Copy-Item -LiteralPath (Join-Path $sourceRoot 'docs\cli.md') -Destination (Join-Path $OutputDirectory 'CLI.md') -Force
Copy-Item -LiteralPath (Join-Path $sourceRoot 'examples') -Destination $OutputDirectory -Recurse -Force
$application = Join-Path $OutputDirectory 'Akara.exe'
if (-not (Test-Path -LiteralPath $application)) { throw 'La publicación no produjo Akara.exe.' }
$cliApplication = Join-Path $OutputDirectory 'akara-cli.exe'
if (-not (Test-Path -LiteralPath $cliApplication)) { throw 'La publicación no produjo akara-cli.exe.' }
& (Join-Path $PSScriptRoot 'Test-Portable.ps1') -Executable $application
if ($LASTEXITCODE -ne 0) { throw 'No se superó la prueba del ejecutable empaquetado.' }
& $cliApplication version
if ($LASTEXITCODE -ne 0) { throw 'No se superó la prueba de AKARA CLI.' }
Get-FileHash -LiteralPath $application,$cliApplication -Algorithm SHA256 | Select-Object Path, Hash | ConvertTo-Json
