[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable,[string]$SavePath,[string]$ExcelPath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$application = [IO.Path]::GetFullPath($Executable)
if (-not (Test-Path -LiteralPath $application)) { throw 'No existe el ejecutable nativo que se quiere probar.' }
if ([bool]$SavePath -ne [bool]$ExcelPath) { throw 'Indica SavePath y ExcelPath juntos, o ninguno para la prueba sin datos privados.' }
$qaRoot = Join-Path ([IO.Path]::GetTempPath()) ('akara-native-qa-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $qaRoot | Out-Null
$storage = Join-Path $qaRoot 'data'
$checks = [Collections.Generic.List[string]]::new()
function Run-Native([string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($application)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    $outTask = $process.StandardOutput.ReadToEndAsync()
    $errTask = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw ('La prueba nativa superó 60 segundos. Evidencia: ' + $qaRoot) }
    $stdout = $outTask.GetAwaiter().GetResult()
    $stderr = $errTask.GetAwaiter().GetResult()
    if ($process.ExitCode -ne 0) { throw ('El proceso nativo devolvió ' + $process.ExitCode + '. Evidencia: ' + $qaRoot + '. ' + $stderr + $stdout) }
    if ($stdout -match 'AKARA_READY http|localhost|127\.0\.0\.1') { throw 'La aplicación de escritorio está arrancando un host web.' }
    return @{Stdout=$stdout;Stderr=$stderr}
}
function Assert-Png([string]$Path,[int]$Width,[int]$Height) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 10000 -or $bytes[0] -ne 137 -or $bytes[1] -ne 80 -or $bytes[2] -ne 78 -or $bytes[3] -ne 71) { throw 'La captura no es un PNG nativo válido.' }
    $actualWidth = ([int]$bytes[16] -shl 24) -bor ([int]$bytes[17] -shl 16) -bor ([int]$bytes[18] -shl 8) -bor [int]$bytes[19]
    $actualHeight = ([int]$bytes[20] -shl 24) -bor ([int]$bytes[21] -shl 16) -bor ([int]$bytes[22] -shl 8) -bor [int]$bytes[23]
    if ($actualWidth -ne $Width -or $actualHeight -ne $Height) { throw 'Las dimensiones de la captura no coinciden.' }
}
foreach ($size in @(@{Width=1600;Height=1000},@{Width=1280;Height=900})) {
    $capture = Join-Path $qaRoot ('native-' + $size.Width + '.png')
    $null = Run-Native @('--capture',$capture,'--scenario','demo','--width',$size.Width.ToString(),'--height',$size.Height.ToString(),'--data-dir',$storage)
    Assert-Png $capture $size.Width $size.Height
    $metadata = Get-Content -LiteralPath ($capture+'.json') -Raw | ConvertFrom-Json
    if ($metadata.framework -ne 'PresentationFramework' -or -not $metadata.native -or $metadata.browser -or $metadata.visibleWindowCreated) { throw 'La captura no acredita un árbol WPF fuera de pantalla.' }
    if ($metadata.actualWidth -ne $size.Width -or $metadata.actualHeight -ne $size.Height) { throw 'El control WPF no se dispuso con el tamaño requerido.' }
    $checks.Add('wpf-offscreen-' + $size.Width + 'x' + $size.Height)
}
$reportPath = Join-Path $qaRoot 'self-test.json'
$arguments = @('--self-test',$reportPath,'--data-dir',$storage)
if ($SavePath) { $arguments += @('--open',[IO.Path]::GetFullPath($SavePath),'--excel',[IO.Path]::GetFullPath($ExcelPath)) }
$null = Run-Native $arguments
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($report.framework -ne 'PresentationFramework' -or $report.failed -ne 0 -or $report.passed -lt 1) { throw 'La prueba de comportamiento nativo no pasó.' }
$checks.Add('native-viewmodel-self-test')
[pscustomobject]@{Executable=$application;Framework='WPF';StructuralChecks=$checks.Count;Checks=$checks;BehaviorPassed=$report.passed;BehaviorSkipped=$report.skipped;EvidenceDirectory=$qaRoot;Sha256=(Get-FileHash -LiteralPath $application -Algorithm SHA256).Hash} | ConvertTo-Json -Depth 8
