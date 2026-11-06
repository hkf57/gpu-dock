$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$arguments = '/nologo /target:winexe /platform:x64 /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /win32icon:"{0}\..\GpuDock.ico" /out:"{0}\..\GpuDock.exe" "{0}\GpuDock.cs"' -f $PSScriptRoot
$log = Join-Path $PSScriptRoot 'build.log'
$process = Start-Process -FilePath $compiler -ArgumentList $arguments -Wait -PassThru -NoNewWindow -RedirectStandardOutput $log
Get-Content $log
if ($process.ExitCode -ne 0) { throw "Build failed with exit code $($process.ExitCode)" }
Write-Output 'Built GpuDock.exe'
