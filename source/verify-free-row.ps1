$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[void][Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot '..\GpuDock.exe'))
$config=Join-Path $env:LOCALAPPDATA 'GpuDock\settings.xml'
$exists=Test-Path $config
$original=if($exists){[IO.File]::ReadAllBytes($config)}else{$null}
$dock=$null
try {
 $settings=[GpuDock.Settings]::Load()
 $settings.ShowFree=$true
 $dock=New-Object GpuDock.Dock -ArgumentList $settings
 $dock.Show()
 [Windows.Forms.Application]::DoEvents()
 $height=$dock.Height
 $field=$dock.GetType().GetField('tray',[Reflection.BindingFlags]'Instance,NonPublic')
 $tray=$field.GetValue($dock)
 $item=$tray.ContextMenuStrip.Items | Where-Object Text -eq 'Show free % row'
 if(!$item.Checked){throw 'Toggle should start checked'}
 $item.PerformClick()
 if($settings.ShowFree -or [GpuDock.Settings]::Load().ShowFree){throw 'Off state did not persist'}
 if($dock.Height -ne ($height-[int][Math]::Floor($settings.FontSize*2.2))){throw 'Panel did not shrink by one row'}
 $item=$tray.ContextMenuStrip.Items | Where-Object Text -eq 'Show free % row'
 if($item.Checked){throw 'Unchecked menu state missing'}
 $item.PerformClick()
 if(!$settings.ShowFree -or ![GpuDock.Settings]::Load().ShowFree -or $dock.Height -ne $height){throw 'On state did not restore'}
 'PASS: tray toggle, checked state, saved off/on settings, and panel resizing'
} finally {
 if($dock){$dock.Close();$dock.Dispose()}
 if($exists){[IO.File]::WriteAllBytes($config,$original)}elseif(Test-Path $config){Remove-Item $config}
}
