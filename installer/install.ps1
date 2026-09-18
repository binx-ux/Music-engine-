$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

$api = 'https://api.github.com/repos/binx-ux/Music-engine-/releases/latest'
$setupUrl = 'https://github.com/binx-ux/Music-engine-/releases/latest/download/CueboxSetup.exe'
$zipUrl = 'https://github.com/binx-ux/Music-engine-/releases/latest/download/Cuebox.zip'

Write-Host 'Cuebox installer'
Write-Host ''

$tag = $null
try {
    $rel = Invoke-RestMethod -Uri $api -Headers @{ 'User-Agent' = 'Cuebox-Install' }
    $tag = $rel.tag_name
    Write-Host ("Latest release: " + $tag)
} catch {
    Write-Host 'Could not read GitHub API. Using /latest/download links.'
}

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

$setup = Join-Path $env:TEMP 'CueboxSetup.exe'
Write-Host 'Downloading CueboxSetup.exe...'
try {
    Invoke-WebRequest -UseBasicParsing -Uri $setupUrl -OutFile $setup
    if ((Test-Path $setup) -and ((Get-Item $setup).Length -gt 1_000_000)) {
        Write-Host 'Starting Cuebox Setup...'
        Start-Process -FilePath $setup -Wait
        Write-Host 'Done.'
        return
    }
} catch {
    Write-Host 'Setup download failed. Falling back to zip.'
}

$app = Pick-Folder 'App folder (Cuebox.exe)' (Join-Path $env:LOCALAPPDATA 'Cuebox')
$data = Pick-Folder 'Data folder (settings, music, logs, pads)' (Join-Path $env:APPDATA 'Cuebox')

$zip = Join-Path $env:TEMP 'cuebox.zip'
Write-Host 'Downloading Cuebox.zip...'
$bytes = (Invoke-WebRequest -UseBasicParsing -Uri $zipUrl).Content
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
