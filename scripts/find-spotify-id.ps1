$ErrorActionPreference = 'Stop'
Write-Host 'Cuebox will search this PC for a Spotify Client ID.'
Write-Host 'It looks in app data, env vars, and local config files only.'
$ans = Read-Host 'Allow that and copy a match if found? [Y/N]'
if ($ans -notmatch '^[Yy]') {
    Write-Host 'Cancelled.'
    exit 1
}

$ids = New-Object System.Collections.Generic.List[string]
if ($env:SPOTIFY_CLIENT_ID) { [void]$ids.Add($env:SPOTIFY_CLIENT_ID.Trim()) }
if ($env:SPOTIFY_ID) { [void]$ids.Add($env:SPOTIFY_ID.Trim()) }

$rx = [regex]'(?i)client[_-]?id\s*[:=]\s*["'']([0-9a-f]{32})'
$roots = @(
    (Join-Path $env:APPDATA 'Cuebox'),
    (Join-Path $env:APPDATA 'Mixline'),
    (Join-Path $env:APPDATA 'spicetify'),
    $env:APPDATA,
    $env:LOCALAPPDATA
)
$skip = @('node_modules', '.git', 'Cache', 'Code Cache', 'GPUCache', 'Temp', 'Packages')

function Walk([string]$dir, [int]$depth, [int]$maxDepth) {
    if (-not (Test-Path -LiteralPath $dir)) { return }
    Get-ChildItem -LiteralPath $dir -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in '.json', '.env', '.config', '.ini', '.txt' -and $_.Length -lt 1500000 } |
        ForEach-Object {
            try {
                $t = Get-Content -LiteralPath $_.FullName -Raw -ErrorAction Stop
                foreach ($m in $rx.Matches($t)) { [void]$ids.Add($m.Groups[1].Value) }
            } catch {}
        }
    if ($depth -ge $maxDepth) { return }
    Get-ChildItem -LiteralPath $dir -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        if ($skip -contains $_.Name) { return }
        $next = if ($dir -eq $env:APPDATA -or $dir -eq $env:LOCALAPPDATA) { 1 } else { $maxDepth }
        Walk $_.FullName ($depth + 1) $next
    }
}

foreach ($r in $roots) { Walk $r 0 2 }

$hit = $ids | Where-Object { $_ -match '^[0-9a-fA-F]{32}$' } | Select-Object -First 1
$dir = Join-Path $env:APPDATA 'Cuebox'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
if ($hit) {
    Set-Clipboard -Value $hit
    Set-Content -Path (Join-Path $dir 'found-spotify-id.txt') -Value $hit
    Write-Host "Found Client ID. Copied to clipboard:"
    Write-Host $hit
    Write-Host 'Paste it into Cuebox Settings if it is not already filled.'
} else {
    Write-Host 'No Client ID on this PC. Opening the Spotify Developer Dashboard.'
    Write-Host 'Create an app, copy the Client ID, and set redirect to http://127.0.0.1:43821/callback'
    Start-Process 'https://developer.spotify.com/dashboard'
}
