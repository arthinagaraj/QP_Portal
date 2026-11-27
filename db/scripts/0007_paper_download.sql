-- New permission: CoE / DCoE can view and download submitted question papers (Word / PDF)
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE [Key] = 'paper.download')
BEGIN
    IF COL_LENGTH('Permissions', 'Description') IS NOT NULL
        EXEC('INSERT INTO Permissions ([Key], Description) VALUES (''paper.download'', ''Download submitted question papers'')');
    ELSE
        EXEC('INSERT INTO Permissions ([Key]) VALUES (''paper.download'')');
END

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r CROSS JOIN Permissions p
WHERE r.Name IN ('CoE', 'DCoE') AND p.[Key] = 'paper.download'
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
