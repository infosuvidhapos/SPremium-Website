/* SuvidhaPOS Central - SQL Server 2017+ / SQL Express */
USE master;
GO
IF DB_ID(N'SuvidhaPremium') IS NULL
BEGIN
    CREATE DATABASE SuvidhaPremium;
END
GO
USE SuvidhaPremium;
GO

IF OBJECT_ID('dbo.AdminUsers','U') IS NULL
CREATE TABLE dbo.AdminUsers(
    AdminId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AdminUsers PRIMARY KEY,
    FullName NVARCHAR(120) NOT NULL,
    Email NVARCHAR(190) NOT NULL,
    PasswordHash NVARCHAR(512) NOT NULL,
    Role NVARCHAR(50) NOT NULL CONSTRAINT DF_AdminUsers_Role DEFAULT('Admin'),
    IsActive BIT NOT NULL CONSTRAINT DF_AdminUsers_Active DEFAULT(1),
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_AdminUsers_Created DEFAULT(SYSUTCDATETIME()),
    LastLoginAtUtc DATETIME2(0) NULL
);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_AdminUsers_Email' AND object_id=OBJECT_ID('dbo.AdminUsers'))
CREATE UNIQUE INDEX UX_AdminUsers_Email ON dbo.AdminUsers(Email);
GO

IF NOT EXISTS(SELECT 1 FROM sys.sequences WHERE name='OutletNumberSequence' AND schema_id=SCHEMA_ID('dbo'))
CREATE SEQUENCE dbo.OutletNumberSequence AS BIGINT START WITH 100001 INCREMENT BY 1;
GO

IF OBJECT_ID('dbo.Outlets','U') IS NULL
CREATE TABLE dbo.Outlets(
    OutletId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Outlets PRIMARY KEY,
    OutletCode NVARCHAR(30) NOT NULL,
    LicenseCode NVARCHAR(40) NOT NULL,
    OutletName NVARCHAR(180) NOT NULL,
    Address NVARCHAR(500) NULL,
    Mobile NVARCHAR(30) NULL,
    GstNo NVARCHAR(30) NULL,
    StoreType NVARCHAR(50) NOT NULL,
    ValidFromUtc DATETIME2(0) NOT NULL,
    ValidUntilUtc DATETIME2(0) NOT NULL,
    ActivationCodeHash CHAR(64) NOT NULL,
    LicenseVersion INT NOT NULL CONSTRAINT DF_Outlets_Version DEFAULT(1),
    IsBlocked BIT NOT NULL CONSTRAINT DF_Outlets_Blocked DEFAULT(0),
    CreatedByAdminId UNIQUEIDENTIFIER NULL,
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Outlets_Created DEFAULT(SYSUTCDATETIME()),
    UpdatedAtUtc DATETIME2(0) NULL,
    LastRenewedAtUtc DATETIME2(0) NULL,
    CONSTRAINT FK_Outlets_Admin FOREIGN KEY(CreatedByAdminId) REFERENCES dbo.AdminUsers(AdminId)
);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_Outlets_Code' AND object_id=OBJECT_ID('dbo.Outlets')) CREATE UNIQUE INDEX UX_Outlets_Code ON dbo.Outlets(OutletCode);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_Outlets_LicenseCode' AND object_id=OBJECT_ID('dbo.Outlets')) CREATE UNIQUE INDEX UX_Outlets_LicenseCode ON dbo.Outlets(LicenseCode);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Outlets_Validity' AND object_id=OBJECT_ID('dbo.Outlets')) CREATE INDEX IX_Outlets_Validity ON dbo.Outlets(IsBlocked,ValidUntilUtc);
GO

IF OBJECT_ID('dbo.Devices','U') IS NULL
CREATE TABLE dbo.Devices(
    DeviceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Devices PRIMARY KEY,
    OutletId UNIQUEIDENTIFIER NOT NULL,
    DeviceFingerprint NVARCHAR(256) NOT NULL,
    DeviceName NVARCHAR(120) NULL,
    IsBlocked BIT NOT NULL CONSTRAINT DF_Devices_Blocked DEFAULT(0),
    BoundAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Devices_Bound DEFAULT(SYSUTCDATETIME()),
    LastSeenAtUtc DATETIME2(0) NULL,
    CONSTRAINT FK_Devices_Outlet FOREIGN KEY(OutletId) REFERENCES dbo.Outlets(OutletId) ON DELETE CASCADE
);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_Devices_OutletFingerprint' AND object_id=OBJECT_ID('dbo.Devices')) CREATE UNIQUE INDEX UX_Devices_OutletFingerprint ON dbo.Devices(OutletId,DeviceFingerprint);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_Devices_OnePerOutlet' AND object_id=OBJECT_ID('dbo.Devices')) CREATE UNIQUE INDEX UX_Devices_OnePerOutlet ON dbo.Devices(OutletId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Devices_Outlet' AND object_id=OBJECT_ID('dbo.Devices')) CREATE INDEX IX_Devices_Outlet ON dbo.Devices(OutletId);
GO

IF OBJECT_ID('dbo.LicenseHistory','U') IS NULL
CREATE TABLE dbo.LicenseHistory(
    LicenseHistoryId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LicenseHistory PRIMARY KEY,
    OutletId UNIQUEIDENTIFIER NOT NULL,
    OldValidUntilUtc DATETIME2(0) NULL,
    NewValidUntilUtc DATETIME2(0) NULL,
    OldStoreType NVARCHAR(50) NULL,
    NewStoreType NVARCHAR(50) NULL,
    Action NVARCHAR(40) NOT NULL,
    AdminId UNIQUEIDENTIFIER NULL,
    TokenVersion INT NOT NULL,
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_LicenseHistory_Created DEFAULT(SYSUTCDATETIME()),
    CONSTRAINT FK_LicenseHistory_Outlet FOREIGN KEY(OutletId) REFERENCES dbo.Outlets(OutletId),
    CONSTRAINT FK_LicenseHistory_Admin FOREIGN KEY(AdminId) REFERENCES dbo.AdminUsers(AdminId)
);
GO

IF OBJECT_ID('dbo.AuditLogs','U') IS NULL
CREATE TABLE dbo.AuditLogs(
    AuditId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY,
    AdminId UNIQUEIDENTIFIER NULL,
    Action NVARCHAR(80) NOT NULL,
    EntityType NVARCHAR(50) NULL,
    EntityId NVARCHAR(100) NULL,
    Details NVARCHAR(1000) NULL,
    IpAddress NVARCHAR(80) NULL,
    UserAgent NVARCHAR(400) NULL,
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_AuditLogs_Created DEFAULT(SYSUTCDATETIME()),
    CONSTRAINT FK_AuditLogs_Admin FOREIGN KEY(AdminId) REFERENCES dbo.AdminUsers(AdminId)
);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_AuditLogs_Created' AND object_id=OBJECT_ID('dbo.AuditLogs')) CREATE INDEX IX_AuditLogs_Created ON dbo.AuditLogs(CreatedAtUtc DESC);
GO
PRINT 'SuvidhaPremium database schema is ready.';
