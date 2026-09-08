/*
  For SQL Server/SQL Express on the SAME Windows server as IIS.
  Grants the default IIS App Pool identity only the application permissions it needs.
  If SQL Server is remote, use SQL Authentication or a domain service account instead.
*/
USE master;
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'IIS APPPOOL\SuvidhaPOSCentral')
    CREATE LOGIN [IIS APPPOOL\SuvidhaPOSCentral] FROM WINDOWS;
GO
USE [SuvidhaPOSCentral];
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'IIS APPPOOL\SuvidhaPOSCentral')
    CREATE USER [IIS APPPOOL\SuvidhaPOSCentral] FOR LOGIN [IIS APPPOOL\SuvidhaPOSCentral];
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [IIS APPPOOL\SuvidhaPOSCentral];
GRANT UPDATE ON OBJECT::dbo.OutletNumberSequence TO [IIS APPPOOL\SuvidhaPOSCentral];
GO
PRINT N'IIS App Pool SQL permissions granted.';
