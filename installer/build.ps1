param(
    [string]$Configuration = "Release"
)

$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "src\UI\Mixline.App.csproj"
dotnet publish $project -c $Configuration -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

$iss = Get-Command iscc -ErrorAction SilentlyContinue
if ($iss) {
    & iscc (Join-Path $PSScriptRoot "Cuebox.iss")
} else {
    Write-Host "Inno Setup (iscc) is not on PATH. App is published; compile Cuebox.iss to build Setup.exe."
}
