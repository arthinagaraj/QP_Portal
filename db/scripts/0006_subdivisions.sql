-- Sub-divisions: a question can be split into (i), (ii), (iii)... with their own marks
DECLARE @uq sysname = (SELECT name FROM sys.key_constraints
                       WHERE parent_object_id = OBJECT_ID('Questions') AND type = 'UQ');
IF @uq IS NOT NULL EXEC('ALTER TABLE Questions DROP CONSTRAINT ' + @uq);
GO
ALTER TABLE Questions ADD SubLabel VARCHAR(4) NOT NULL CONSTRAINT DF_Questions_SubLabel DEFAULT '';
GO
ALTER TABLE Questions ADD CONSTRAINT UQ_Questions_Slot UNIQUE (AssignmentId, PartName, QNo, OptionLetter, SubLabel);
GO
ALTER TABLE QpTemplateParts ADD MaxSubdivisions INT NOT NULL CONSTRAINT DF_QpTemplateParts_MaxSub DEFAULT 0;
GO
-- existing frameworks: allow (i)(ii) for questions of 10 marks or more
UPDATE QpTemplateParts SET MaxSubdivisions = 2 WHERE MarksEach >= 10;
