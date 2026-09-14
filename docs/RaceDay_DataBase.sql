/*
===========================================================
    RaceDay Event Management System
    PROG6212 PoE Part 1 - Database Script

    Database Platform:
    Microsoft SQL Server

    Entities:
        1. Role
        2. User
        3. Event
        4. Category
        5. Enrolment
        6. Result
===========================================================
*/


/*
===========================================================
    1. CREATE DATABASE
===========================================================
*/

IF DB_ID('RaceDayDB') IS NULL
BEGIN
    CREATE DATABASE RaceDayDB;
END;
GO

USE RaceDayDB;
GO


/*
===========================================================
    2. DROP EXISTING TABLES
    Allows the script to be tested repeatedly.
===========================================================
*/

IF OBJECT_ID('dbo.Result', 'U') IS NOT NULL
    DROP TABLE dbo.Result;

IF OBJECT_ID('dbo.Enrolment', 'U') IS NOT NULL
    DROP TABLE dbo.Enrolment;

IF OBJECT_ID('dbo.Category', 'U') IS NOT NULL
    DROP TABLE dbo.Category;

IF OBJECT_ID('dbo.Event', 'U') IS NOT NULL
    DROP TABLE dbo.Event;

IF OBJECT_ID('dbo.[User]', 'U') IS NOT NULL
    DROP TABLE dbo.[User];

IF OBJECT_ID('dbo.Role', 'U') IS NOT NULL
    DROP TABLE dbo.Role;
GO


/*
===========================================================
    3. ROLE TABLE
===========================================================
*/

CREATE TABLE dbo.Role
(
    RoleID INT IDENTITY(1,1) NOT NULL,
    RoleName NVARCHAR(50) NOT NULL,

    CONSTRAINT PK_Role
        PRIMARY KEY (RoleID),

    CONSTRAINT UQ_Role_RoleName
        UNIQUE (RoleName)
);
GO


/*
===========================================================
    4. USER TABLE
===========================================================
*/

CREATE TABLE dbo.[User]
(
    UserID INT IDENTITY(1,1) NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(255) NOT NULL,
    PasswordHash NVARCHAR(500) NOT NULL,
    RoleID INT NOT NULL,

    CONSTRAINT PK_User
        PRIMARY KEY (UserID),

    CONSTRAINT UQ_User_Email
        UNIQUE (Email),

    CONSTRAINT FK_User_Role
        FOREIGN KEY (RoleID)
        REFERENCES dbo.Role(RoleID)
);
GO


/*
===========================================================
    5. EVENT TABLE
===========================================================
*/

CREATE TABLE dbo.Event
(
    EventID INT IDENTITY(1,1) NOT NULL,
    EventName NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000) NULL,
    EventDate DATE NOT NULL,
    Location NVARCHAR(255) NOT NULL,
    Distance DECIMAL(6,2) NOT NULL,
    EventType NVARCHAR(50) NOT NULL,
    OrganiserID INT NOT NULL,

    CONSTRAINT PK_Event
        PRIMARY KEY (EventID),

    CONSTRAINT FK_Event_Organiser
        FOREIGN KEY (OrganiserID)
        REFERENCES dbo.[User](UserID),

    CONSTRAINT CK_Event_Distance
        CHECK (Distance > 0),

    CONSTRAINT CK_Event_EventType
        CHECK (
            EventType IN
            (
                'Running',
                'Walking',
                'Cycling'
            )
        )
);
GO


/*
===========================================================
    6. CATEGORY TABLE
===========================================================
*/

CREATE TABLE dbo.Category
(
    CategoryID INT IDENTITY(1,1) NOT NULL,
    EventID INT NOT NULL,
    CategoryName NVARCHAR(100) NOT NULL,
    MinimumAge INT NULL,
    MaximumAge INT NULL,

    CONSTRAINT PK_Category
        PRIMARY KEY (CategoryID),

    CONSTRAINT FK_Category_Event
        FOREIGN KEY (EventID)
        REFERENCES dbo.Event(EventID)
        ON DELETE CASCADE,

    CONSTRAINT UQ_Category_Event_CategoryName
        UNIQUE (EventID, CategoryName),

    CONSTRAINT CK_Category_MinimumAge
        CHECK (MinimumAge IS NULL OR MinimumAge >= 0),

    CONSTRAINT CK_Category_MaximumAge
        CHECK (MaximumAge IS NULL OR MaximumAge >= 0),

    CONSTRAINT CK_Category_AgeRange
        CHECK (
            MaximumAge IS NULL
            OR MinimumAge IS NULL
            OR MaximumAge >= MinimumAge
        )
);
GO


/*
===========================================================
    7. ENROLMENT TABLE
===========================================================
*/

CREATE TABLE dbo.Enrolment
(
    EnrolmentID INT IDENTITY(1,1) NOT NULL,
    ParticipantID INT NOT NULL,
    EventID INT NOT NULL,
    CategoryID INT NOT NULL,
    EnrolmentDate DATETIME2 NOT NULL
        CONSTRAINT DF_Enrolment_EnrolmentDate
        DEFAULT SYSDATETIME(),

    CONSTRAINT PK_Enrolment
        PRIMARY KEY (EnrolmentID),

    CONSTRAINT FK_Enrolment_Participant
        FOREIGN KEY (ParticipantID)
        REFERENCES dbo.[User](UserID),

    CONSTRAINT FK_Enrolment_Event
        FOREIGN KEY (EventID)
        REFERENCES dbo.Event(EventID),

    CONSTRAINT FK_Enrolment_Category
        FOREIGN KEY (CategoryID)
        REFERENCES dbo.Category(CategoryID),

    CONSTRAINT UQ_Enrolment_Participant_Event
        UNIQUE (ParticipantID, EventID)
);
GO


/*
===========================================================
    8. RESULT TABLE
===========================================================
*/

CREATE TABLE dbo.Result
(
    ResultID INT IDENTITY(1,1) NOT NULL,
    EnrolmentID INT NOT NULL,
    FinishTime TIME(0) NULL,
    FinishingPosition INT NULL,

    CONSTRAINT PK_Result
        PRIMARY KEY (ResultID),

    CONSTRAINT FK_Result_Enrolment
        FOREIGN KEY (EnrolmentID)
        REFERENCES dbo.Enrolment(EnrolmentID)
        ON DELETE CASCADE,

    CONSTRAINT UQ_Result_Enrolment
        UNIQUE (EnrolmentID),

    CONSTRAINT CK_Result_FinishingPosition
        CHECK (
            FinishingPosition IS NULL
            OR FinishingPosition > 0
        )
);
GO


/*
===========================================================
    9. INSERT ROLES
===========================================================
*/

INSERT INTO dbo.Role
(
    RoleName
)
VALUES
('Organiser'),
('Participant');
GO


/*
===========================================================
    10. INSERT USERS
===========================================================
*/

INSERT INTO dbo.[User]
(
    FirstName,
    LastName,
    Email,
    PasswordHash,
    RoleID
)
VALUES
(
    'Thabo',
    'Mokoena',
    'thabo.mokoena@raceday.co.za',
    'HASHED_PASSWORD_001',
    1
),
(
    'Lerato',
    'Naidoo',
    'lerato.naidoo@raceday.co.za',
    'HASHED_PASSWORD_002',
    1
),
(
    'Sipho',
    'Dlamini',
    'sipho.dlamini@example.com',
    'HASHED_PASSWORD_003',
    2
),
(
    'Amara',
    'Peters',
    'amara.peters@example.com',
    'HASHED_PASSWORD_004',
    2
);
GO


/*
===========================================================
    11. INSERT EVENTS
===========================================================
*/

INSERT INTO dbo.Event
(
    EventName,
    Description,
    EventDate,
    Location,
    Distance,
    EventType,
    OrganiserID
)
VALUES
(
    'Cape Coastal Marathon',
    'A scenic road marathon along the Cape coastline.',
    '2026-10-18',
    'Cape Town, Western Cape',
    42.20,
    'Running',
    1
),
(
    'Nelson Mandela Bay Cycle Tour',
    'A competitive road cycling event through Gqeberha.',
    '2026-11-08',
    'Gqeberha, Eastern Cape',
    80.00,
    'Cycling',
    2
),
(
    'Garden Route Community Walk',
    'A family-friendly community walking event.',
    '2026-12-06',
    'George, Western Cape',
    10.00,
    'Walking',
    1
);
GO


/*
===========================================================
    12. INSERT CATEGORIES
===========================================================
*/

INSERT INTO dbo.Category
(
    EventID,
    CategoryName,
    MinimumAge,
    MaximumAge
)
VALUES
-- Cape Coastal Marathon
(
    1,
    'Open Men',
    18,
    NULL
),
(
    1,
    'Open Women',
    18,
    NULL
),
(
    1,
    'Veterans',
    40,
    NULL
),

-- Nelson Mandela Bay Cycle Tour
(
    2,
    'Elite',
    18,
    NULL
),
(
    2,
    'Open',
    18,
    NULL
),
(
    2,
    'Junior',
    16,
    17
),

-- Garden Route Community Walk
(
    3,
    'Adult',
    18,
    NULL
),
(
    3,
    'Youth',
    10,
    17
),
(
    3,
    'Family',
    NULL,
    NULL
);
GO


/*
===========================================================
    13. INSERT ENROLMENTS
===========================================================
*/

INSERT INTO dbo.Enrolment
(
    ParticipantID,
    EventID,
    CategoryID,
    EnrolmentDate
)
VALUES
(
    3,
    1,
    1,
    '2026-08-01 09:15:00'
),
(
    4,
    1,
    2,
    '2026-08-03 14:30:00'
),
(
    3,
    2,
    5,
    '2026-08-10 11:00:00'
),
(
    4,
    3,
    7,
    '2026-08-15 16:45:00'
);
GO


/*
===========================================================
    14. INSERT RESULTS
===========================================================
*/

INSERT INTO dbo.Result
(
    EnrolmentID,
    FinishTime,
    FinishingPosition
)
VALUES
(
    1,
    '03:42:18',
    47
),
(
    2,
    '04:11:35',
    83
),
(
    3,
    '02:26:42',
    21
);
GO


/*
===========================================================
    15. VERIFICATION QUERIES
===========================================================
*/

SELECT *
FROM dbo.Role;

SELECT *
FROM dbo.[User];

SELECT *
FROM dbo.Event;

SELECT *
FROM dbo.Category;

SELECT *
FROM dbo.Enrolment;

SELECT *
FROM dbo.Result;
GO


/*
===========================================================
    16. JOINED VERIFICATION QUERY
===========================================================
*/

SELECT
    e.EnrolmentID,
    CONCAT(u.FirstName, ' ', u.LastName) AS Participant,
    ev.EventName,
    c.CategoryName,
    r.FinishTime,
    r.FinishingPosition,
    e.EnrolmentDate
FROM dbo.Enrolment e
INNER JOIN dbo.[User] u
    ON e.ParticipantID = u.UserID
INNER JOIN dbo.Event ev
    ON e.EventID = ev.EventID
INNER JOIN dbo.Category c
    ON e.CategoryID = c.CategoryID
LEFT JOIN dbo.Result r
    ON e.EnrolmentID = r.EnrolmentID
ORDER BY e.EnrolmentID;
GO
