INSERT INTO Roles (Name) VALUES ('CoE'), ('DCoE'), ('HoD'), ('Setter');

INSERT INTO Permissions ([Key], Description) VALUES
 ('setter.upload',       'Upload QP setter database'),
 ('faculty.approve',     'Approve self-registered faculty'),
 ('course.upload',       'Upload courses, regulations and syllabus'),
 ('instructions.manage', 'Edit common instructions for setters'),
 ('template.manage',     'Create QP framework per regulation'),
 ('setter.assign',       'Map setters to subjects with deadline, send links'),
 ('report.view',         'View QP and claim received status'),
 ('claim.export',        'Download bank settlement Excel'),
 ('paper.edit',          'Enter own question paper and claim');

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
JOIN Permissions p ON
     (r.Name IN ('CoE', 'DCoE') AND p.[Key] <> 'paper.edit')
  OR (r.Name = 'HoD'    AND p.[Key] = 'report.view')
  OR (r.Name = 'Setter' AND p.[Key] = 'paper.edit');