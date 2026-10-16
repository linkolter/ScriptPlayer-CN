param([string]$PublishName='playback-baseline/round1-20261010/final-build')
$ErrorActionPreference='Stop'
$workspace=& (Join-Path $PSScriptRoot 'Find-LocalWorkspace.ps1')
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class MaintenanceCursor {
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point p);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
}
'@
$saved=New-Object MaintenanceCursor+Point
if(![MaintenanceCursor]::GetCursorPos([ref]$saved)){throw 'Cannot preserve cursor before desktop regression'}
try {
    if(![MaintenanceCursor]::SetCursorPos(0,0)){throw 'Cannot establish neutral physical cursor baseline'}
    & (Join-Path $workspace 'run-playback-baseline.ps1') -PublishName $PublishName -Stage4
} finally {
    if(![MaintenanceCursor]::SetCursorPos($saved.X,$saved.Y)){throw 'Cannot restore original cursor after desktop regression'}
}
