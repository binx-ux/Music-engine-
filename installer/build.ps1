param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "src\UI\Mixline.App.csproj"
$out = Join-Path $PSScriptRoot "output"
$pub = Join-Path $PSScriptRoot "publish"
$env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')

if (Test-Path $pub) { Remove-Item $pub -Recurse -Force }
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null

dotnet publish $project -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=false -o $pub
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

Copy-Item (Join-Path $root "LICENSE") (Join-Path $pub "LICENSE") -Force
Copy-Item (Join-Path $PSScriptRoot "info.txt") (Join-Path $pub "info.txt") -Force

$zip = Join-Path $out "Cuebox.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $pub '*') -DestinationPath $zip -CompressionLevel Optimal

$iscc = $null
$cmd = Get-Command iscc -ErrorAction SilentlyContinue
if ($cmd) { $iscc = $cmd.Source }
if (-not $iscc) {
    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if ($iscc) {
    & $iscc (Join-Path $PSScriptRoot "Cuebox.iss")
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed." }
} else {
    Write-Host "Inno Setup (iscc) is not installed. Zip is in $zip"
}

Write-Host "Built:"
Get-ChildItem $out | ForEach-Object { Write-Host $_.FullName }
