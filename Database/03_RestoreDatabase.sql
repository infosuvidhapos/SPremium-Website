/*
  Restore the GitHub-build generated backup on Windows SQL Server / SQL Express 2022+.

  1) Copy DatabaseBackup\SuvidhaPOSCentral.bak to:
       C:\SuvidhaPOSBackup\SuvidhaPOSCentral.bak
  2) Run this script from SSMS while connected to the destination instance.

  WARNING: WITH REPLACE overwrites an existing SuvidhaPOSCentral database.
  This script maps Linux backup file paths to the destination server's default Windows data/log folders.
*/
USE master;
GO
SET NOCOUNT ON;

DECLARE @BackupFile nvarchar(4000) = N'C:\SuvidhaPOSBackup\SuvidhaPOSCentral.bak';
DECLARE @DataPath nvarchar(4000) = CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultDataPath'));
DECLARE @LogPath  nvarchar(4000) = CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultLogPath'));

IF @DataPath IS NULL OR @LogPath IS NULL
    THROW 51001, 'Could not determine SQL Server default data/log paths.', 1;

IF RIGHT(@DataPath, 1) NOT IN (N'\', N'/') SET @DataPath += N'\';
IF RIGHT(@LogPath, 1) NOT IN (N'\', N'/') SET @LogPath += N'\';

DECLARE @DataFile nvarchar(4000) = @DataPath + N'SuvidhaPOSCentral.mdf';
DECLARE @LogFile  nvarchar(4000) = @LogPath  + N'SuvidhaPOSCentral_log.ldf';
DECLARE @sql nvarchar(max);

BEGIN TRY
    IF DB_ID(N'SuvidhaPOSCentral') IS NOT NULL
        ALTER DATABASE [SuvidhaPOSCentral] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

    SET @sql =
        N'RESTORE DATABASE [SuvidhaPOSCentral] FROM DISK = N''' + REPLACE(@BackupFile, '''', '''''') + N''' ' +
        N'WITH REPLACE, RECOVERY, CHECKSUM, ' +
        N'MOVE N''SuvidhaPOSCentral'' TO N''' + REPLACE(@DataFile, '''', '''''') + N''', ' +
        N'MOVE N''SuvidhaPOSCentral_log'' TO N''' + REPLACE(@LogFile, '''', '''''') + N''', STATS = 10;';

    EXEC sys.sp_executesql @sql;
    ALTER DATABASE [SuvidhaPOSCentral] SET MULTI_USER;
    PRINT N'SuvidhaPOSCentral restored successfully.';
END TRY
BEGIN CATCH
    IF DB_ID(N'SuvidhaPOSCentral') IS NOT NULL
    BEGIN
        BEGIN TRY
            ALTER DATABASE [SuvidhaPOSCentral] SET MULTI_USER;
        END TRY
        BEGIN CATCH
        END CATCH
    END
    THROW;
END CATCH;
GO
