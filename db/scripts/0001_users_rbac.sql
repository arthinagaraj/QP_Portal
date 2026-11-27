CREATE TABLE Roles (
  Id INT IDENTITY PRIMARY KEY,
  Name VARCHAR(30) NOT NULL UNIQUE
);

CREATE TABLE Permissions (
  Id INT IDENTITY PRIMARY KEY,
  [Key] VARCHAR(50) NOT NULL UNIQUE,
  Description NVARCHAR(200) NOT NULL
);

CREATE TABLE RolePermissions (
  RoleId INT NOT NULL REFERENCES Roles(Id),
  PermissionId INT NOT NULL REFERENCES Permissions(Id),
  PRIMARY KEY (RoleId, PermissionId)
);

CREATE TABLE Users (
  Id INT IDENTITY PRIMARY KEY,
  Name NVARCHAR(120) NOT NULL,
  Email NVARCHAR(200) NOT NULL UNIQUE,
  Mobile VARCHAR(15) NULL,
  PasswordHash NVARCHAR(200) NULL,
  DateOfBirth DATE NULL,
  Designation NVARCHAR(100) NULL,
  Department NVARCHAR(100) NULL,
  Institution NVARCHAR(200) NULL,
  SetterType VARCHAR(10) NULL CHECK (SetterType IN ('Internal','External')),
  Qualification NVARCHAR(100) NULL,
  ExperienceYears INT NULL,
  AreaOfInterest NVARCHAR(500) NULL,
  RegistrationStatus VARCHAR(10) NOT NULL DEFAULT 'Approved'
      CHECK (RegistrationStatus IN ('Pending','Approved','Rejected')),
  Source VARCHAR(10) NOT NULL DEFAULT 'Upload'
      CHECK (Source IN ('Upload','Register','Seed')),
  RejectionReason NVARCHAR(500) NULL,
  ReviewedBy INT NULL,
  ReviewedAt DATETIME2 NULL,
  IsActive BIT NOT NULL DEFAULT 1,
  CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE UNIQUE INDEX UX_Users_Mobile ON Users(Mobile) WHERE Mobile IS NOT NULL;

CREATE TABLE UserRoles (
  UserId INT NOT NULL REFERENCES Users(Id),
  RoleId INT NOT NULL REFERENCES Roles(Id),
  PRIMARY KEY (UserId, RoleId)
);