CREATE TABLE Settings (
  [Key] VARCHAR(50) PRIMARY KEY,
  Value NVARCHAR(MAX) NOT NULL
);

INSERT INTO Settings ([Key], Value) VALUES ('CommonInstructions',
N'<ol>
<li>Set questions strictly from the syllabus of the unit shown on each page.</li>
<li>Each question must be complete, with all data, figures and tables needed to answer it.</li>
<li>Follow the question paper framework (parts, number of questions, marks) exactly.</li>
<li>Do not share the question paper with anyone. Do not take a printout of the question paper.</li>
<li>Enter your personal and bank details correctly; the claim is paid to this account.</li>
<li>Submit before the deadline given in the invitation.</li>
</ol>');

CREATE TABLE QpTemplates (
  Id INT IDENTITY PRIMARY KEY,
  RegulationId INT NOT NULL REFERENCES Regulations(Id),
  ExamType VARCHAR(30) NOT NULL DEFAULT 'End Semester',
  Name NVARCHAR(150) NOT NULL,
  Duration NVARCHAR(30) NOT NULL DEFAULT '3 Hours',
  MaxMarks DECIMAL(5,1) NOT NULL,
  Instructions NVARCHAR(MAX) NULL,
  Version INT NOT NULL DEFAULT 1,
  Status VARCHAR(10) NOT NULL DEFAULT 'Draft' CHECK (Status IN ('Draft','Published','Retired')),
  CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE QpTemplateParts (
  Id INT IDENTITY PRIMARY KEY,
  TemplateId INT NOT NULL REFERENCES QpTemplates(Id) ON DELETE CASCADE,
  PartName VARCHAR(5) NOT NULL,
  Title NVARCHAR(150) NOT NULL,
  Distribution VARCHAR(10) NOT NULL CHECK (Distribution IN ('PerUnit','AnyUnit')),
  QuestionCount INT NOT NULL,
  MarksEach DECIMAL(5,1) NOT NULL,
  IsEitherOr BIT NOT NULL DEFAULT 0,
  DisplayOrder INT NOT NULL
);

CREATE TABLE Assignments (
  Id INT IDENTITY PRIMARY KEY,
  CourseId INT NOT NULL REFERENCES Courses(Id),
  SetterId INT NOT NULL REFERENCES Users(Id),
  TemplateId INT NOT NULL REFERENCES QpTemplates(Id),
  ExamSession NVARCHAR(50) NOT NULL,
  DueDate DATE NOT NULL,
  Status VARCHAR(15) NOT NULL DEFAULT 'Assigned'
      CHECK (Status IN ('Assigned','InProgress','Submitted','Cancelled')),
  LinkToken VARCHAR(64) NOT NULL UNIQUE,
  AssignedBy INT NOT NULL,
  AssignedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
  InviteSentAt DATETIME2 NULL,
  SubmittedAt DATETIME2 NULL
);