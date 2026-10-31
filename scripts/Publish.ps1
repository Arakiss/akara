[CmdletBinding()]
param([string]$DotnetPath,[string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$sourceRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if (-not $DotnetPath) {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnetCommand) { $DotnetPath = $dotnetCommand.Source }
}
if (-not $DotnetPath -or -not (Test-Path -LiteralPath $DotnetPath)) { throw 'Instala el SDK de .NET 10 o indica su ruta con -DotnetPath.' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $sourceRoot 'artifacts\AKARA-Desktop' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$desktopProject = Join-Path $sourceRoot 'src\Akara.Desktop\Akara.Desktop.csproj'
$cliProject = Join-Path $sourceRoot 'src\Akara.Cli\Akara.Cli.csproj'
if (-not (Test-Path -LiteralPath $desktopProject)) { throw 'Falta el proyecto nativo de escritorio.' }
if (Test-Path -LiteralPath (Join-Path $sourceRoot 'src\Akara.Desktop\Assets\class-portraits.png')) { throw 'Un candidato visual rechazado sigue dentro de los recursos de producción.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$buildRoot = Join-Path $sourceRoot 'artifacts\native-build'
# The shipping UI is WPF. No web build, localhost host, browser or WebView is used.
foreach ($project in @($desktopProject,$cliProject)) {
    & $DotnetPath publish $project -c Release -r win-x64 --self-contained true --artifacts-path $buildRoot -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw ('No se pudo publicar ' + [IO.Path]::GetFileNameWithoutExtension($project) + '.') }
}
Copy-Item -LiteralPath (Join-Path $sourceRoot 'LEEME.txt') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $sourceRoot 'LICENSE') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $sourceRoot 'licenses') -Destination $OutputDirectory -Recurse -Force
Copy-Item -LiteralPath (Join-Path $sourceRoot 'docs\cli.md') -Destination (Join-Path $OutputDirectory 'CLI.md') -Force
Copy-Item -LiteralPath (Join-Path $sourceRoot 'examples') -Destination $OutputDirectory -Recurse -Force
$application = Join-Path $OutputDirectory 'Akara.exe'
$cliApplication = Join-Path $OutputDirectory 'akara-cli.exe'
if (-not (Test-Path -LiteralPath $application) -or -not (Test-Path -LiteralPath $cliApplication)) { throw 'La publicación no produjo ambos ejecutables.' }
& (Join-Path $PSScriptRoot 'Test-Native.ps1') -Executable $application
& $cliApplication version
if ($LASTEXITCODE -ne 0) { throw 'No se superó la prueba de AKARA CLI.' }
Get-FileHash -LiteralPath $application,$cliApplication -Algorithm SHA256 | Select-Object Path,Hash | ConvertTo-Json
