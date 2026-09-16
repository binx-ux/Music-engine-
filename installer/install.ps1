$ErrorActionPreference = 'Stop'

$url = 'https://github.com/binx-ux/Music-engine-/releases/latest/download/Cuebox.zip'
$zip = Join-Path $env:TEMP 'cuebox.zip'
$dest = Join-Path $env:LOCALAPPDATA 'Cuebox'
$exe = Join-Path $dest 'Cuebox.exe'

$bytes = (Invoke-WebRequest -UseBasicParsing -Uri $url).Content
[IO.File]::WriteAllBytes($zip, $bytes)

if (Test-Path $dest) {
    Get-Process Cuebox -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 400
    Remove-Item $dest -Recurse -Force
}

New-Item -ItemType Directory -Path $dest | Out-Null
Expand-Archive -LiteralPath $zip -DestinationPath $dest -Force
Remove-Item $zip -Force

$desktop = [Environment]::GetFolderPath('Desktop')
$link = Join-Path $desktop 'Cuebox.lnk'
$w = New-Object -ComObject WScript.Shell
$s = $w.CreateShortcut($link)
$s.TargetPath = $exe
$s.WorkingDirectory = $dest
$s.Save()

Start-Process $exe
