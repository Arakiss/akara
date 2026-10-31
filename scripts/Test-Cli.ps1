[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][string]$SavePath,
    [Parameter(Mandatory)][string]$ExcelPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$cli = [IO.Path]::GetFullPath($Executable)
$fixture = [IO.Path]::GetFullPath($SavePath)
$excel = [IO.Path]::GetFullPath($ExcelPath)
$qaRoot = Join-Path ([IO.Path]::GetTempPath()) ('akara-cli-qa-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $qaRoot | Out-Null
$storage = Join-Path $qaRoot 'storage'
$fixtureHash = (Get-FileHash -LiteralPath $fixture -Algorithm SHA256).Hash
$checks = [Collections.Generic.List[string]]::new()
function Run-Cli([string[]]$Arguments, [int]$Expected = 0) {
    $start = [Diagnostics.ProcessStartInfo]::new($cli)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments + @('--excel',$excel,'--data-dir',$storage)) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    $outTask = $process.StandardOutput.ReadToEndAsync()
    $errTask = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'CLI timeout' }
    $stdout = $outTask.GetAwaiter().GetResult()
    $stderr = $errTask.GetAwaiter().GetResult()
    if ($process.ExitCode -ne $Expected) { throw ('Expected exit ' + $Expected + ', got ' + $process.ExitCode + ': ' + $stderr + $stdout) }
    if ($Expected -eq 0) { if ($stderr) { throw ('Unexpected stderr: ' + $stderr) }; return ($stdout | ConvertFrom-Json) }
    if ($stdout) { throw ('Error emitted stdout instead of stderr: ' + $stdout) }
    $errorObject = $stderr | ConvertFrom-Json
    if (-not $errorObject.error.code) { throw 'Missing machine error code' }
    return $errorObject
}
$version = Run-Cli @('version'); if ($version.version -ne '0.3.0') { throw 'Wrong CLI version' }; $checks.Add('standalone-version')
$schema = Run-Cli @('schema','preview'); if (-not $schema.'$defs'.PreviewRequest.properties.itemOperations) { throw 'Missing inventory schema' }; $checks.Add('machine-schema')
$inspection = Run-Cli @('inspect',$fixture)
if (-not $inspection.validation.roundtrip) { throw 'Fixture must roundtrip' }
$name = $inspection.character.name
$source = Join-Path $qaRoot ($name + '.d2s')
Copy-Item -LiteralPath $fixture -Destination $source
$changes = Join-Path $qaRoot 'changes.json'
'{"fillTomes":true}' | Set-Content -LiteralPath $changes -Encoding utf8
$plan = Join-Path $qaRoot 'plan.json'
$null = Run-Cli @('plan',$source,'--changes',('@' + $changes),'--out',$plan)
if ((Get-FileHash -LiteralPath $source).Hash -ne $fixtureHash) { throw 'Planning wrote the source' }; $checks.Add('plan-read-only')
$null = Run-Cli @('validate',$plan); $checks.Add('cross-process-plan-validation')
$badOutput = Join-Path $qaRoot 'must-not-exist.json'
$null = Run-Cli @('plan',$source,'--changes',('@' + $changes),'--out',$badOutput,'--typo','value') 2
if (Test-Path -LiteralPath $badOutput) { throw 'Unknown option mutated files' }; $checks.Add('unknown-options-before-write')
$tampered = Get-Content -LiteralPath $plan -Raw | ConvertFrom-Json
$tampered.candidateHash = '0' * 64
$tamperedPath = Join-Path $qaRoot 'tampered.json'
$tampered | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath $tamperedPath -Encoding utf8
$null = Run-Cli @('validate',$tamperedPath) 3; $checks.Add('tamper-detection')
$copyDir = Join-Path $qaRoot 'copy'; New-Item -ItemType Directory -Path $copyDir | Out-Null
$export = Join-Path $copyDir ($name + '.d2s')
$null = Run-Cli @('export',$plan,'--out',$export)
$null = Run-Cli @('export',$plan,'--out',$export) 4; $checks.Add('export-no-overwrite')
$before = Run-Cli @('inspect',$source)
$applied = Run-Cli @('apply',$plan)
if (-not (Test-Path -LiteralPath $applied.backupPath)) { throw 'Apply did not create a backup' }
if ((Get-FileHash -LiteralPath $applied.backupPath).Hash -ne $fixtureHash) { throw 'Backup did not preserve original' }
$after = Run-Cli @('inspect',$source)
if ($after.character.experience -ne $before.character.experience) { throw 'XP was reset by an unrelated edit' }
$checks.Add('verified-backup-atomic-apply-xp-preserved')
$null = Run-Cli @('apply',$plan) 4; $checks.Add('stale-source-conflict')
$design = '{"baseCode":"amu","quality":"crafted","level":85,"rarePrefixId":1,"rareSuffixId":1,"stats":[{"id":127,"value":2},{"id":105,"value":20}]}'
$craftPlan = Join-Path $qaRoot 'craft.akara-plan.json'
$null = Run-Cli @('items','create','--save',$source,'--changes',$design,'--out',$craftPlan)
$craftDir = Join-Path $qaRoot 'crafted'; New-Item -ItemType Directory -Path $craftDir | Out-Null
$craftedSave = Join-Path $craftDir ($name + '.d2s')
$null = Run-Cli @('export',$craftPlan,'--out',$craftedSave)
$crafted = Run-Cli @('inspect',$craftedSave)
$amulet = $crafted.items | Where-Object code -eq 'amu' | Select-Object -First 1
if (-not $amulet -or $amulet.stats.Count -ne 2) { throw 'Custom item lost native properties' }; $checks.Add('crafted-native-save')
$blueprint = Join-Path $qaRoot 'item.akara-item.json'
$null = Run-Cli @('items','export','--save',$craftedSave,'--id',$amulet.id,'--out',$blueprint)
$importPlan = Join-Path $qaRoot 'import.akara-plan.json'
$null = Run-Cli @('items','import','--save',$source,'--blueprint',('@'+$blueprint),'--out',$importPlan)
$null = Run-Cli @('validate',$importPlan); $checks.Add('cross-process-blueprint-import-export')
$editDesign = $design | ConvertFrom-Json
$editDesign | Add-Member -NotePropertyName personalizedName -NotePropertyValue 'AKARA'
$editPlan = Join-Path $qaRoot 'edit.akara-plan.json'
$edited = Run-Cli @('items','edit','--save',$craftedSave,'--id',$amulet.id,'--changes',($editDesign|ConvertTo-Json -Compress -Depth 10),'--out',$editPlan)
$editedItem = $edited.plan.inspection.items | Where-Object id -eq $amulet.id | Select-Object -First 1
if (-not $editedItem -or $editedItem.personalizedName -ne 'AKARA' -or $editedItem.page -ne $amulet.page -or $editedItem.x -ne $amulet.x -or $editedItem.y -ne $amulet.y) { throw 'Editing lost identity, signature or position' }
$null = Run-Cli @('validate',$editPlan); $checks.Add('item-edit-signature-identity-position')
$worldPlan = Join-Path $qaRoot 'world.akara-plan.json'
$null = Run-Cli @('plan',$source,'--changes','{"world":{"waypoints":[{"difficulty":"normal","id":1,"active":true}]}}','--out',$worldPlan)
$null = Run-Cli @('validate',$worldPlan); $checks.Add('world-waypoint-plan')
if ((Get-FileHash -LiteralPath $fixture).Hash -ne $fixtureHash) { throw 'Private fixture was modified' }; $checks.Add('fixture-unchanged')
[pscustomobject]@{Passed=$checks.Count;Checks=$checks;EvidenceDirectory=$qaRoot} | ConvertTo-Json -Depth 6
