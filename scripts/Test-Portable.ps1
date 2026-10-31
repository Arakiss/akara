[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable,[string]$SavePath,[string]$ExcelPath)
# Compatibility entry point for the native 0.3 distribution.
$arguments = @{Executable=$Executable}
if ($SavePath) { $arguments.SavePath=$SavePath }
if ($ExcelPath) { $arguments.ExcelPath=$ExcelPath }
& (Join-Path $PSScriptRoot 'Test-Native.ps1') @arguments
