/*
  For SQL Server/SQL Express on the SAME Windows server as IIS.
  Grants the default IIS App Pool identity only the application permissions it needs.
  If SQL Server is remote, use SQL Authentication or a domain service account instead.
*/
USE master;
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'IIS APPPOOL\SuvidhaPremium')
    CREATE LOGIN [IIS APPPOOL\SuvidhaPremium] FROM WINDOWS;
GO
USE [SuvidhaPremium];
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'IIS APPPOOL\SuvidhaPremium')
    CREATE USER [IIS APPPOOL\SuvidhaPremium] FOR LOGIN [IIS APPPOOL\SuvidhaPremium];
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [IIS APPPOOL\SuvidhaPremium];
GRANT UPDATE ON OBJECT::dbo.OutletNumberSequence TO [IIS APPPOOL\SuvidhaPremium];
GO
PRINT N'IIS App Pool SQL permissions granted.';
