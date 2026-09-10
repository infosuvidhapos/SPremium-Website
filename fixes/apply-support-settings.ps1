$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$program = Join-Path $root 'src\SuvidhaPremium\Program.cs'
$index = Join-Path $root 'src\SuvidhaPremium\wwwroot\index.html'

$p = [IO.File]::ReadAllText($program, [Text.Encoding]::UTF8)
$marker = 'SupportSettingsModule.Map(app);'
if (-not $p.Contains($marker)) {
    $needle = 'app.UseAuthorization();'
    if (-not $p.Contains($needle)) { throw 'Program.cs mapping insertion point not found' }
    $p = $p.Replace($needle, $needle + "`r`n" + $marker)
    [IO.File]::WriteAllText($program, $p, (New-Object Text.UTF8Encoding($false)))
}

$h = [IO.File]::ReadAllText($index, [Text.Encoding]::UTF8)
$script = '<script src="assets/js/support-settings.js?v=34"></script>'
if (-not $h.Contains($script)) {
    if (-not $h.Contains('</body>')) { throw 'index.html body close not found' }
    $h = $h.Replace('</body>', $script + '</body>')
    [IO.File]::WriteAllText($index, $h, (New-Object Text.UTF8Encoding($false)))
}

Write-Host 'Support settings build wiring applied.'
