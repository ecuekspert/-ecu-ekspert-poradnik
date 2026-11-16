param([string]$Exe)
$sh = New-Object -ComObject WScript.Shell
$desk = [Environment]::GetFolderPath('Desktop')
$start = Join-Path ([Environment]::GetFolderPath('Programs')) 'ECU EKSPERT'
New-Item -ItemType Directory -Force -Path $start | Out-Null
$name = 'ECU EKSPERT - Poradnik elektryka samochodowego.lnk'

# usun stare skroty
foreach ($old in @('Poradnik czujnikow.lnk', 'Poradnik czujnikow (offline).lnk')) {
  Remove-Item -ErrorAction SilentlyContinue (Join-Path $desk $old)
}
Remove-Item -ErrorAction SilentlyContinue -Recurse (Join-Path ([Environment]::GetFolderPath('Programs')) 'Ecu Ekspert\Poradnik czujnikow.lnk')

foreach ($dir in @($desk, $start)) {
  $l = $sh.CreateShortcut((Join-Path $dir $name))
  $l.TargetPath = $Exe
  $l.WorkingDirectory = Split-Path $Exe
  $l.IconLocation = "$Exe,0"
  $l.Description = 'ECU EKSPERT - Poradnik elektryka samochodowego'
  $l.Save()
}
