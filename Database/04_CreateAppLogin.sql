/*
  OPTIONAL: use only if you prefer SQL Authentication.
  Replace the password before running. Do not commit the real password to GitHub.
*/
USE master;
GO
IF NOT EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = N'suvidhapos_app')
BEGIN
    CREATE LOGIN [suvidhapos_app]
    WITH PASSWORD = N'CHANGE_SQL_PASSWORD_HERE', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
END
GO
USE [SuvidhaPremium];
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'suvidhapos_app')
    CREATE USER [suvidhapos_app] FOR LOGIN [suvidhapos_app];
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [suvidhapos_app];
GRANT UPDATE ON OBJECT::dbo.OutletNumberSequence TO [suvidhapos_app];
GO
