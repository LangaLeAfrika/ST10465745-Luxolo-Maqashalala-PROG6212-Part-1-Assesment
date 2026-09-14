# ST10465745 Luxolo Maqashalala PROG6212 Part 1 Assesment
# RaceDay Event Management System

## PROG6212 PoE Part 1

RaceDay is a web-based event management system designed for South African road running, walking and cycling events.

The system supports two main user roles:

- Organiser
- Participant

Part 1 focuses on system planning and database design before the API and MVC application are developed.

---

# User Roles

## Organiser

Organisers can:

- Create events
- Edit events
- Delete events
- Manage event categories
- View event enrolments
- Capture participant results
- View information relating to events they manage

## Participant

Participants can:

- Create an account
- Log in
- Browse available events
- Enter an event
- Select an event category
- View their own enrolments
- View their race results
- Track their performance history

---

# Part 1 Deliverables

## Entity Relationship Diagram

The ERD is available in:

`docs/RaceDay_ERD.md`

The ERD contains six entities:

1. Role
2. User
3. Event
4. Category
5. Enrolment
6. Result

The ERD identifies:

- Primary keys
- Foreign keys
- Relationships
- Cardinality
- One-to-many relationships
- The associative Enrolment entity used to resolve the Participant/Event many-to-many relationship

---

# API Endpoint Plan

The planned REST API endpoints are available in:

`docs/RaceDay_API_Endpoint_Plan.md`

The endpoint plan covers:

- Authentication
- User Profiles
- Events
- Categories
- Event Enrolments
- Results

Each endpoint includes:

- HTTP Method
- Route
- Description
- Role
- Required Request Body
- Expected Response

---

# SQL Database

The SQL Server database script is located at:

`docs/RaceDay_Database.sql`

The script creates:

- Role
- User
- Event
- Category
- Enrolment
- Result

The database includes:

- Primary keys
- Foreign keys
- NOT NULL constraints
- UNIQUE constraints
- DEFAULT constraints
- CHECK constraints
- Sample data

---

# Database Setup

## Requirements

- Microsoft SQL Server
- SQL Server Management Studio (SSMS)

## Steps

1. Open SQL Server Management Studio.
2. Connect to your SQL Server instance.
3. Open `docs/RaceDay_Database.sql`.
4. Execute the complete script.
5. Confirm that `RaceDayDB` is created.
6. Confirm that all six tables exist.
7. Check the inserted sample records.
8. Run the verification queries included at the bottom of the script.

---

# Repository Structure

```text
RaceDay/
│
├── README.md
│
├── docs/
│   ├── RaceDay_ERD.md
│   ├── RaceDay_API_Endpoint_Plan.md
│   └── RaceDay_Database.sql
│
└── .github/
    └── workflows/
        └── part1-ci.yml
