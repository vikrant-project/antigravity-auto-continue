$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources=@(Get-ChildItem (Join-Path $PSScriptRoot 'src/*.cs') | ForEach-Object FullName)
$distPath=Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $distPath -Force | Out-Null
$references=@('/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Management.dll','/r:System.Net.Http.dll','/r:System.Web.Extensions.dll','/r:System.Security.dll')
& $compiler /nologo /target:winexe /main:AntiGravityRepeater.Program /platform:anycpu /optimize+ "/out:$distPath/AntiGravityRepeater.exe" @references @sources
if($LASTEXITCODE -ne 0){throw 'Build failed'}
& $compiler /nologo /target:exe /platform:anycpu /optimize+ /main:AntiGravityRepeater.Diagnostics "/out:$distPath/RepeaterDiagnostics.exe" @references @sources
if($LASTEXITCODE -ne 0){throw 'Diagnostics build failed'}
Write-Output 'Build succeeded.'
