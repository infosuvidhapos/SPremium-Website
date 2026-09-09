@echo off
setlocal
where dotnet >nul 2>&1 || (
  echo ERROR: .NET 10 SDK not found.
  echo Install .NET 10 SDK or use GitHub Actions Build IIS Package workflow.
  pause
  exit /b 1
)
if exist iis-publish rmdir /s /q iis-publish
dotnet restore .\src\SuvidhaPremium\SuvidhaPremium.csproj || exit /b 1
dotnet publish .\src\SuvidhaPremium\SuvidhaPremium.csproj -c Release -o .\iis-publish || exit /b 1
echo.
echo IIS publish folder ready: %CD%\iis-publish
pause
