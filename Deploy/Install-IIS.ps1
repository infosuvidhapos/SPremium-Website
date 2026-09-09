param(
    [Parameter(Mandatory=$true)] [string]$PhysicalPath,
    [string]$SiteName = "SuvidhaPremium",
    [string]$AppPoolName = "SuvidhaPremium",
    [int]$Port = 8080
)
$ErrorActionPreference = "Stop"
Import-Module WebAdministration
$PhysicalPath = (Resolve-Path $PhysicalPath).Path

if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    New-WebAppPool -Name $AppPoolName | Out-Null
}
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ""
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name processModel.identityType -Value ApplicationPoolIdentity

if (Test-Path "IIS:\Sites\$SiteName") {
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $PhysicalPath
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName
} else {
    New-Website -Name $SiteName -PhysicalPath $PhysicalPath -ApplicationPool $AppPoolName -Port $Port | Out-Null
}

$identity = "IIS AppPool\$AppPoolName"
foreach ($dir in @("App_Data","logs")) {
    $path = Join-Path $PhysicalPath $dir
    New-Item -ItemType Directory -Force -Path $path | Out-Null
    & icacls $path /grant "${identity}:(OI)(CI)M" /T | Out-Null
}

Write-Host "IIS site ready: $SiteName" -ForegroundColor Green
Write-Host "Physical path: $PhysicalPath"
Write-Host "App pool: $AppPoolName (No Managed Code)"
Write-Host "HTTP test binding: http://localhost:$Port"
Write-Host "For production, add an HTTPS certificate binding and enable Security:RequireHttps."
