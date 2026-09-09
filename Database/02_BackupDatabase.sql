/*
  SuvidhaPremium full backup.
  Change @BackupFile if required.
  SQL Server service account must have write permission to the target folder.
*/
USE master;
GO
DECLARE @BackupFile nvarchar(4000) = N'C:\SuvidhaPOSBackup\SuvidhaPremium.bak';
BACKUP DATABASE [SuvidhaPremium]
TO DISK = @BackupFile
WITH INIT, CHECKSUM, STATS = 10;
RESTORE VERIFYONLY FROM DISK = @BackupFile WITH CHECKSUM;
PRINT N'Backup created and verified: ' + @BackupFile;
GO
