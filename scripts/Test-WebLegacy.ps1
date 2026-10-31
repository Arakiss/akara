[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$application = [IO.Path]::GetFullPath($Executable)
if (-not (Test-Path -LiteralPath $application)) { throw 'No existe el ejecutable que se quiere probar.' }
$qaRoot = Join-Path ([IO.Path]::GetTempPath()) ('akara-portable-qa-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $qaRoot | Out-Null
$stdout = Join-Path $qaRoot 'stdout.log'
$stderr = Join-Path $qaRoot 'stderr.log'
$process = Start-Process -FilePath $application -ArgumentList '--no-browser','--port','0' -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
try {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $match = $null
    while ($timer.Elapsed.TotalSeconds -lt 30) {
        $log = if (Test-Path -LiteralPath $stdout) { Get-Content -LiteralPath $stdout -Raw } else { '' }
        if ($log) { $match = [regex]::Match($log, 'AKARA_READY (http://[^\s]+)'); if ($match.Success) { break } }
        if ($process.HasExited) { throw ('El ejecutable terminó antes de iniciar: ' + (Get-Content -LiteralPath $stderr -Raw)) }
        Start-Sleep -Milliseconds 100
    }
    if (-not $match -or -not $match.Success) { throw 'El ejecutable no publicó su estado preparado en30segundos.' }
    $ready = [uri]$match.Groups[1].Value
    $base = $ready.GetLeftPart([UriPartial]::Authority)
    $token = [uri]::UnescapeDataString($ready.Query.Substring(7))
    $headers = @{'X-Forja-Token'=$token}
    $boot = Invoke-RestMethod -Uri "$base/api/bootstrap" -Headers $headers
    if ($boot.appName -ne 'AKARA') { throw 'El endpoint no corresponde a AKARA.' }
    $page = Invoke-WebRequest -Uri "$base/"
    $assets = [regex]::Matches($page.Content, '(?:src|href)="([^"?#]+\.(?:js|css|svg))"')
    if ($assets.Count -lt 3) { throw 'Faltan la interfaz y sus recursos embebidos.' }
    foreach ($asset in $assets) {
        $response = Invoke-WebRequest -Uri ($base + $asset.Groups[1].Value)
        if ($response.StatusCode -ne 200 -or $response.Headers['Content-Type'] -match 'text/html') { throw ('Recurso no servido: ' + $asset.Groups[1].Value) }
    }
    $unauthorized = Invoke-WebRequest -Uri "$base/api/bootstrap" -SkipHttpErrorCheck
    if ($unauthorized.StatusCode -ne 401) { throw 'La API no rechaza peticiones sin token.' }
    $foreign = Invoke-WebRequest -Uri "$base/api/bootstrap" -Headers @{'X-Forja-Token'=$token;Origin='https://example.invalid'} -SkipHttpErrorCheck
    if ($foreign.StatusCode -ne 403) { throw 'La API no rechaza un origen externo.' }
    $missing = Invoke-WebRequest -Uri "$base/assets/not-a-real-file.js" -SkipHttpErrorCheck
    if ($missing.StatusCode -ne 404) { throw 'Un recurso inexistente debe devolver404.' }
    $closed = Invoke-RestMethod -Uri "$base/api/shutdown" -Method Post -Headers $headers -ContentType 'application/json' -Body '{}'
    if (-not $closed.closed -or -not $process.WaitForExit(5000)) { throw 'El cierre no terminó el host local.' }
    [pscustomobject]@{Executable=$application;Version=$boot.version;EmbeddedAssets=$assets.Count;Unauthorized=401;ForeignOrigin=403;MissingAsset=404;Shutdown=$true;Sha256=(Get-FileHash -LiteralPath $application -Algorithm SHA256).Hash}
} finally {
    if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
}
