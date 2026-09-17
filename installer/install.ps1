$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

$release = 'https://github.com/binx-ux/Music-engine-/releases/latest/download/Cuebox.zip'
$zip = Join-Path $env:TEMP 'cuebox.zip'

function Pick-Folder([string]$title, [string]$start) {
    $d = New-Object System.Windows.Forms.FolderBrowserDialog
    $d.Description = $title
    $d.SelectedPath = $start
    $d.ShowNewFolderButton = $true
    if ($d.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
        throw 'Install cancelled.'
    }
    return $d.SelectedPath
}

$app = Pick-Folder 'App folder (Cuebox.exe)' (Join-Path $env:LOCALAPPDATA 'Cuebox')
$data = Pick-Folder 'Data folder (settings, music, logs, pads)' (Join-Path $env:APPDATA 'Cuebox')

Write-Host "Downloading Cuebox..."
$bytes = (Invoke-WebRequest -UseBasicParsing -Uri $release).Content
[IO.File]::WriteAllBytes($zip, $bytes)

Get-Process Cuebox -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

New-Item -ItemType Directory -Force -Path $app | Out-Null
New-Item -ItemType Directory -Force -Path $data | Out-Null
Expand-Archive -LiteralPath $zip -DestinationPath $app -Force
Remove-Item $zip -Force -ErrorAction SilentlyContinue

$exe = Join-Path $app 'Cuebox.exe'
if (-not (Test-Path $exe)) {
    $found = Get-ChildItem $app -Recurse -Filter Cuebox.exe | Select-Object -First 1
    if ($found) { $exe = $found.FullName; $app = $found.DirectoryName }
}
if (-not (Test-Path $exe)) { throw 'Cuebox.exe was not in the zip.' }

[IO.File]::WriteAllText((Join-Path $app 'data.path'), $data)

$desktop = [Environment]::GetFolderPath('Desktop')
$link = Join-Path $desktop 'Cuebox.lnk'
$w = New-Object -ComObject WScript.Shell
$s = $w.CreateShortcut($link)
$s.TargetPath = $exe
$s.WorkingDirectory = $app
$s.Save()

Start-Process $exe
Write-Host "Installed to $app"
Write-Host "Data in $data"
