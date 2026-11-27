-- Scrutiny: CoE sends a submitted QP to a DCoE / HoD for review
CREATE TABLE Scrutinies (
    Id           INT IDENTITY PRIMARY KEY,
    AssignmentId INT NOT NULL REFERENCES Assignments(Id),
    ReviewerId   INT NOT NULL REFERENCES Users(Id),
    AssignedBy   INT NOT NULL REFERENCES Users(Id),
    AssignedAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Status       VARCHAR(20) NOT NULL DEFAULT 'Pending',   -- Pending / Approved / ChangesRequested
    Remarks      NVARCHAR(2000) NULL,
    ReviewedAt   DATETIME2 NULL
);
CREATE INDEX IX_Scrutinies_Assignment ON Scrutinies(AssignmentId);
CREATE INDEX IX_Scrutinies_Reviewer ON Scrutinies(ReviewerId);
GO

IF COL_LENGTH('Permissions', 'Description') IS NOT NULL
BEGIN
    EXEC('INSERT INTO Permissions ([Key], Description) VALUES (''scrutiny.assign'', ''Send question papers for scrutiny''), (''scrutiny.review'', ''Review question papers sent for scrutiny'')');
END
ELSE
    EXEC('INSERT INTO Permissions ([Key]) VALUES (''scrutiny.assign''), (''scrutiny.review'')');
GO

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id FROM Roles r CROSS JOIN Permissions p
WHERE (r.Name = 'CoE' AND p.[Key] = 'scrutiny.assign')
   OR (r.Name IN ('DCoE', 'HoD') AND p.[Key] = 'scrutiny.review');
