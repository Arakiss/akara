[CmdletBinding()]
param([string]$SourceDirectory,[string]$MSBuildPath)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$commit='6b889ec8f421cbc9baccec296c582362ab917887'
if(-not $SourceDirectory){$SourceDirectory=Join-Path $repository 'artifacts/casclib-source'}
$SourceDirectory=[IO.Path]::GetFullPath($SourceDirectory)
if(-not(Test-Path -LiteralPath (Join-Path $SourceDirectory '.git'))){
    New-Item -ItemType Directory -Path $SourceDirectory -Force|Out-Null
    & git -C $SourceDirectory init --quiet
    if($LASTEXITCODE -ne 0){throw 'No se pudo crear la copia de fuentes de CascLib.'}
    & git -C $SourceDirectory fetch --quiet --depth 1 https://github.com/ladislav-zezula/CascLib.git $commit
    if($LASTEXITCODE -ne 0){throw 'No se pudo obtener el commit de CascLib.'}
    & git -C $SourceDirectory checkout --quiet --detach FETCH_HEAD
    if($LASTEXITCODE -ne 0){throw 'No se pudo seleccionar el commit de CascLib.'}
}
$actual=(& git -C $SourceDirectory rev-parse HEAD).Trim()
if($actual -ne $commit){throw 'Las fuentes de CascLib no coinciden con el commit fijado.'}
if(-not $MSBuildPath){
    $locator=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if(Test-Path -LiteralPath $locator){$MSBuildPath=(& $locator -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\Current\Bin\MSBuild.exe'|Select-Object -First 1)}
}
if(-not $MSBuildPath -or -not(Test-Path -LiteralPath $MSBuildPath)){throw 'Instala Visual Studio Build Tools con C++ y Windows SDK o indica -MSBuildPath.'}
# This version-resource include is the only local patch. CascLib itself does not
# require MFC: winres.h provides the resource constants from the Windows SDK.
$resource=Join-Path $SourceDirectory 'src/DllMain.rc'
$text=[IO.File]::ReadAllText($resource).Replace('#include "afxres.h"','#include <winres.h>')
[IO.File]::WriteAllText($resource,$text)
Push-Location -LiteralPath $SourceDirectory
try{
    & $MSBuildPath CascLib_dll.vcxproj /p:Configuration=Release /p:Platform=x64 /m /v:minimal /nologo
    if($LASTEXITCODE -ne 0){throw 'CascLib no compila.'}
}finally{Pop-Location}
$destination=Join-Path $repository 'src/Akara.Desktop/Native'
New-Item -ItemType Directory -Path $destination -Force|Out-Null
Copy-Item -LiteralPath (Join-Path $SourceDirectory 'bin/CascLib_dll/x64/Release/CascLib.dll') -Destination (Join-Path $destination 'CascLib.dll') -Force
Copy-Item -LiteralPath (Join-Path $SourceDirectory 'LICENSE') -Destination (Join-Path $repository 'licenses/CascLib-MIT.txt') -Force
Get-FileHash -LiteralPath (Join-Path $destination 'CascLib.dll') -Algorithm SHA256|Select-Object Hash
