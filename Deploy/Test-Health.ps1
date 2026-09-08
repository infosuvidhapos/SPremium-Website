param([string]$BaseUrl = "http://localhost:8080")
$u = $BaseUrl.TrimEnd('/') + '/health'
try {
    $r = Invoke-RestMethod -Uri $u -Method Get
    $r | ConvertTo-Json -Depth 5
} catch {
    Write-Error $_
    exit 1
}
