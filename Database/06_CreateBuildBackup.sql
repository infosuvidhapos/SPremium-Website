/*
  GitHub build backup creator.
  Runs inside the temporary SQL Server Linux container used by GitHub Actions.
  Output is bind-mounted to the runner and packaged as a real .bak file.
*/
USE master;
GO
IF DB_ID(N'SuvidhaPremium') IS NULL
    THROW 51000, 'SuvidhaPremium does not exist. Run 01_CreateDatabase.sql first.', 1;
GO

BACKUP DATABASE [SuvidhaPremium]
TO DISK = N'/var/opt/mssql/backup/SuvidhaPremium.bak'
WITH COPY_ONLY, INIT, FORMAT, CHECKSUM, COMPRESSION, STATS = 10;
GO

RESTORE VERIFYONLY
FROM DISK = N'/var/opt/mssql/backup/SuvidhaPremium.bak'
WITH CHECKSUM;
GO

PRINT N'Verified build backup created: /var/opt/mssql/backup/SuvidhaPremium.bak';
GO
