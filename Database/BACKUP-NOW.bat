@echo off
setlocal
set SERVER=.\SQLEXPRESS
if not exist C:\SuvidhaPOSBackup mkdir C:\SuvidhaPOSBackup
where sqlcmd >nul 2>&1 || (
  echo ERROR: sqlcmd not found. Install SQL Server command-line utilities or run 02_BackupDatabase.sql from SSMS.
  pause
  exit /b 1
)
sqlcmd -S "%SERVER%" -E -b -i "%~dp002_BackupDatabase.sql"
if errorlevel 1 (
  echo Backup failed. Check SQL Server service permissions for C:\SuvidhaPOSBackup.
  pause
  exit /b 1
)
echo Backup ready: C:\SuvidhaPOSBackup\SuvidhaPOSCentral.bak
pause
