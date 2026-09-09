param(
    [Parameter(Mandatory=$true)] [string]$PublishPath,
    [string]$SqlServer = ".\SQLEXPRESS",
    [string]$Database = "SuvidhaPremium",
    [string]$SqlUser = "",
    [string]$SqlPassword = "",
    [switch]$RequireHttps
)
$ErrorActionPreference = "Stop"
$PublishPath = (Resolve-Path $PublishPath).Path

$bytes = New-Object byte[] 24
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
$hex = -join ($bytes | ForEach-Object { $_.ToString("X2") })
$setupKey = "SUV-SETUP-" + $hex

$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder["Data Source"] = $SqlServer
$builder["Initial Catalog"] = $Database
$builder["Encrypt"] = $true
$builder["TrustServerCertificate"] = $true
$builder["MultipleActiveResultSets"] = $false
if ([string]::IsNullOrWhiteSpace($SqlUser)) {
    $builder["Integrated Security"] = $true
} else {
    $builder["User ID"] = $SqlUser
    $builder["Password"] = $SqlPassword
}
$cs = $builder.ConnectionString

$config = [ordered]@{
    ConnectionStrings = [ordered]@{ CentralDb = $cs }
    Security = [ordered]@{
        SetupKey = $setupKey
        RequireHttps = [bool]$RequireHttps
    }
}
$configPath = Join-Path $PublishPath "appsettings.Production.json"
$config | ConvertTo-Json -Depth 5 | Set-Content -Path $configPath -Encoding UTF8

$appData = Join-Path $PublishPath "App_Data"
$logs = Join-Path $PublishPath "logs"
New-Item -ItemType Directory -Force -Path $appData,$logs | Out-Null

Write-Host ""
Write-Host "Production configuration created:" -ForegroundColor Green
Write-Host "  $configPath"
Write-Host ""
Write-Host "FIRST ADMIN SETUP KEY (save it securely):" -ForegroundColor Yellow
Write-Host "  $setupKey" -ForegroundColor Cyan
Write-Host ""
Write-Host "After the first administrator is created, the setup endpoint closes automatically."
