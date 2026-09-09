/*
  Restore the GitHub-build generated backup on Windows SQL Server / SQL Express 2019+.

  1) Copy DatabaseBackup\SuvidhaPremium.bak to:
       C:\SuvidhaPOSBackup\SuvidhaPremium.bak
  2) Run this script from SSMS while connected to the destination instance.

  WARNING: WITH REPLACE overwrites an existing SuvidhaPremium database.
  This script maps Linux backup file paths to the destination server's default Windows data/log folders.
*/
USE master;
GO
SET NOCOUNT ON;

DECLARE @BackupFile nvarchar(4000) = N'C:\SuvidhaPOSBackup\SuvidhaPremium.bak';
DECLARE @DataPath nvarchar(4000) = CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultDataPath'));
DECLARE @LogPath  nvarchar(4000) = CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultLogPath'));

IF @DataPath IS NULL OR @LogPath IS NULL
    THROW 51001, 'Could not determine SQL Server default data/log paths.', 1;

IF RIGHT(@DataPath, 1) NOT IN (N'\', N'/') SET @DataPath += N'\';
IF RIGHT(@LogPath, 1) NOT IN (N'\', N'/') SET @LogPath += N'\';

DECLARE @DataFile nvarchar(4000) = @DataPath + N'SuvidhaPremium.mdf';
DECLARE @LogFile  nvarchar(4000) = @LogPath  + N'SuvidhaPremium_log.ldf';
DECLARE @sql nvarchar(max);

BEGIN TRY
    IF DB_ID(N'SuvidhaPremium') IS NOT NULL
        ALTER DATABASE [SuvidhaPremium] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

    SET @sql =
        N'RESTORE DATABASE [SuvidhaPremium] FROM DISK = N''' + REPLACE(@BackupFile, '''', '''''') + N''' ' +
        N'WITH REPLACE, RECOVERY, CHECKSUM, ' +
        N'MOVE N''SuvidhaPremium'' TO N''' + REPLACE(@DataFile, '''', '''''') + N''', ' +
        N'MOVE N''SuvidhaPremium_log'' TO N''' + REPLACE(@LogFile, '''', '''''') + N''', STATS = 10;';

    EXEC sys.sp_executesql @sql;
    ALTER DATABASE [SuvidhaPremium] SET MULTI_USER;
    PRINT N'SuvidhaPremium restored successfully.';
END TRY
BEGIN CATCH
    IF DB_ID(N'SuvidhaPremium') IS NOT NULL
    BEGIN
        BEGIN TRY
            ALTER DATABASE [SuvidhaPremium] SET MULTI_USER;
        END TRY
        BEGIN CATCH
        END CATCH
    END
    THROW;
END CATCH;
GO
