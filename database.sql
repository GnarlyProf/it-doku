-- SQL Server DDL for manual DB creation (alternative to EF migrations)
CREATE TABLE dbo.DokuObject (
    Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    ParentId UNIQUEIDENTIFIER NULL,
    Name NVARCHAR(200) NOT NULL,
    Slug NVARCHAR(200) NULL,
    NodeType TINYINT NOT NULL DEFAULT 0, -- 0=Folder,1=Category,2=Location,3=Device,4=Custom
    Description NVARCHAR(MAX) NULL,
    Level INT NOT NULL CONSTRAINT CK_DokuObject_Level CHECK (Level BETWEEN 0 AND 5) DEFAULT 0,
    SortOrder INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2(2) NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(128) NULL,
    UpdatedAt DATETIME2(2) NULL,
    UpdatedBy NVARCHAR(128) NULL,
    CONSTRAINT FK_DokuObject_Parent FOREIGN KEY (ParentId) REFERENCES dbo.DokuObject(Id)
);
GO

CREATE INDEX IX_DokuObject_Parent_Sort ON dbo.DokuObject (ParentId, SortOrder);
CREATE INDEX IX_DokuObject_Name ON dbo.DokuObject (Name);

CREATE TABLE dbo.DokuFile (
    Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    ObjectId UNIQUEIDENTIFIER NOT NULL,
    FileName NVARCHAR(260) NOT NULL,
    ContentType NVARCHAR(255) NULL,
    ByteSize BIGINT NOT NULL DEFAULT 0,
    Content VARBINARY(MAX) NULL, -- alternativ FILESTREAM oder externes Storage
    Version INT NOT NULL DEFAULT 1,
    Note NVARCHAR(1000) NULL,
    CreatedAt DATETIME2(2) NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(128) NULL,
    UpdatedAt DATETIME2(2) NULL,
    UpdatedBy NVARCHAR(128) NULL,
    CONSTRAINT FK_DokuFile_Object FOREIGN KEY (ObjectId) REFERENCES dbo.DokuObject(Id)
);
GO

CREATE INDEX IX_DokuFile_Object ON dbo.DokuFile (ObjectId);

CREATE TABLE dbo.DokuFileVersion (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    FileId UNIQUEIDENTIFIER NOT NULL,
    Version INT NOT NULL,
    FileName NVARCHAR(260) NOT NULL,
    ContentType NVARCHAR(255) NULL,
    ByteSize BIGINT NOT NULL,
    Content VARBINARY(MAX) NULL,
    CreatedAt DATETIME2(2) NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(128) NULL,
    CONSTRAINT FK_DokuFileVersion_File FOREIGN KEY (FileId) REFERENCES dbo.DokuFile(Id)
);
GO

CREATE UNIQUE INDEX UX_DokuFileVersion_File_Version ON dbo.DokuFileVersion (FileId, Version);
