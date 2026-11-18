# TaskFlow

TaskFlow is a full-stack task and project management application designed to help users organise projects and manage tasks efficiently. The application combines an ASP.NET Core Web API with a React frontend and is currently developed as a local portfolio project.

## Table of Contents

- [Implemented Features](#implemented-features)
- [Technology Stack](#technology-stack)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Installation and Setup](#installation-and-setup)
- [Running the Application](#running-the-application)
- [API Documentation](#api-documentation)
- [Testing](#testing)
- [Future Improvements](#future-improvements)
- [Author](#author)

## Implemented Features

- User registration and login
- JWT-based authentication
- Password hashing with BCrypt
- Protected API endpoints
- Project creation, listing, editing and deletion
- Task creation, listing, editing and deletion
- Project-specific task management
- Task statuses and priorities
- Task due dates and optional assignees
- Responsive React interface for desktop and mobile
- Frontend project and task interfaces connected to the API

The following items are planned improvements rather than completed features:

- Dashboard statistics connected to live API data
- Improved task filtering
- Assignee selection through a user-list interface
- Enhanced validation and error handling
- Production deployment
- Additional security hardening

## Technology Stack

| Area | Technology | Verified Version or Target |
|---|---|---|
| Language | C# | .NET 10 target |
| Backend framework | ASP.NET Core Web API | .NET 10 |
| ORM | Entity Framework Core | 10.0.15 |
| Local database | SQLite | EF Core SQLite provider 10.0.15 |
| Authentication | JWT bearer authentication | ASP.NET Core package 10.0.15 |
| Password hashing | BCrypt.Net-Next | 4.2.0 |
| Frontend | React | 19.3.1 |
| Frontend language | JavaScript / JSX | - |
| Frontend build tool | Vite | 8.3.6 package / 8.3.8 installed build version |
| Routing | React Router DOM | 7.18.5 |
| HTTP client | Axios | 1.21.1 |
| Icons | Lucide React | 1.74.0 |
| Testing | xUnit | 2.9.3 |
| Source control | Git and GitHub | - |

## Project Structure

```text
TaskFlow/
├── TaskFlow.slnx
├── README.md
├── TaskFlow.API/
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   ├── ProjectsController.cs
│   │   ├── TasksController.cs
│   │   ├── DashboardController.cs
│   │   └── WeatherForecastController.cs
│   ├── DTOs/
│   │   ├── AuthDtos.cs
│   │   ├── ProjectDtos.cs
│   │   └── TaskDtos.cs
│   ├── Models/
│   │   ├── User.cs
│   │   ├── Project.cs
│   │   └── TaskItem.cs
│   ├── Services/
│   │   └── AuthService.cs
│   ├── Data/
│   │   └── ApplicationDbContext.cs
│   ├── Migrations/
│   ├── Settings/
│   │   └── JwtSettings.cs
│   ├── Properties/
│   │   └── launchSettings.json
│   ├── Program.cs
│   ├── appsettings.json
│   └── TaskFlow.API.csproj
├── TaskFlow.Tests/
│   ├── AuthServiceTests.cs
│   ├── ProjectsControllerTests.cs
│   ├── TasksControllerTests.cs
│   └── TaskFlow.Tests.csproj
└── TaskFlow.Client/
    ├── index.html
    ├── package.json
    └── src/
        ├── App.jsx
        ├── main.jsx
        ├── App.css
        ├── index.css
        ├── components/
        │   ├── layout/
        │   └── common/
        ├── pages/
        │   ├── Dashboard.jsx
        │   ├── Projects.jsx
        │   ├── Tasks.jsx
        │   ├── Login.jsx
        │   └── Register.jsx
        ├── context/
        │   ├── AuthContext.jsx
        │   └── useAuth.js
        └── services/
            ├── api.js
            └── authService.js
```

## Prerequisites

Install the following before running the project:

- .NET 10 SDK
- Node.js 22
- npm
- Git
- VS Code, optional but recommended

Verify the installations:

```bash
dotnet --version
node --version
npm --version
git --version
```

## Installation and Setup

### 1. Clone the repository

```bash
git clone <repository-url>
cd TaskFlow
```

Replace `<repository-url>` with your GitHub repository URL.

### 2. Restore backend dependencies

From the project root:

```bash
dotnet restore TaskFlow.slnx
```

### 3. Install frontend dependencies

```bash
cd TaskFlow.Client
npm install
```

### 4. Configure the JWT signing key with User Secrets

The JWT signing key must not be committed to source control. It is loaded from configuration through .NET User Secrets during development.

From `TaskFlow.API`:

```bash
cd ../TaskFlow.API
dotnet user-secrets init
dotnet user-secrets set "JwtSettings:Key" "<development-signing-key>"
```

Replace `<development-signing-key>` with a strong, privately generated development key. Do not commit the key.

In production, provide the key through the environment variable:

```bash
JwtSettings__Key=<production-signing-key>
```

### 5. Configure SQLite

The default connection in `TaskFlow.API/appsettings.json` is:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=taskflow.db"
  }
}
```

The SQLite database file is created by EF Core in the API working directory. No SQL Server installation is required for local development.

### 6. Apply EF Core migrations

From `TaskFlow.API`:

```bash
dotnet ef migrations list
dotnet ef database update
```

If `dotnet ef` is not available as a local command, install the EF Core CLI tool:

```bash
dotnet tool install --global dotnet-ef
```

The existing `InitialCreate` migration creates the database schema for users, projects and tasks.

### 7. Configure the frontend API URL

The client uses `VITE_API_BASE_URL` when it is set and falls back to:

```text
http://localhost:5258/api
```

An example environment file is available at:

```text
TaskFlow.Client/.env.example
```

To create a local environment file:

```bash
cd ../TaskFlow.Client
cp .env.example .env
```

## Running the Application

### Start the backend

From `TaskFlow.API`:

```bash
cd TaskFlow.API
dotnet run --launch-profile http
```

Backend URL:

```text
http://localhost:5258
```

Health endpoint:

```text
http://localhost:5258/api/health
```

API base URL:

```text
http://localhost:5258/api
```

### Start the frontend

From `TaskFlow.Client`:

```bash
cd TaskFlow.Client
npm run dev
```

The default Vite development URL is:

```text
http://localhost:5173
```

## API Documentation

All project and task endpoints require JWT authentication. Authentication is performed with an `Authorization: Bearer <token>` header.

### Authentication

| Method | Route | Description |
|---|---|---|
| POST | `/api/auth/register` | Register a user and return a JWT with user details. |
| POST | `/api/auth/login` | Log in by username or email and return a JWT with user details. |
| GET | `/api/auth/me` | Return the authenticated user summary. |

Registration request:

```json
{
  "username": "username",
  "email": "user@example.com",
  "password": "password"
}
```

Login request:

```json
{
  "identifier": "username-or-email",
  "password": "password"
}
```

Authentication success returns:

```json
{
  "token": "JWT",
  "user": {
    "id": 1,
    "username": "username",
    "email": "user@example.com"
  }
}
```

Backend authentication failures return a JSON object containing `message`.

### Projects

| Method | Route | Description |
|---|---|---|
| GET | `/api/projects` | List projects owned by the authenticated user. |
| GET | `/api/projects/{id}` | Retrieve an owned project. |
| POST | `/api/projects` | Create a project for the authenticated user. |
| PUT | `/api/projects/{id}` | Partially update an owned project. |
| DELETE | `/api/projects/{id}` | Delete an owned project and its associated tasks. |

Create and update fields:

```json
{
  "name": "Project name",
  "description": "Optional description",
  "dueDate": "2027-01-31T00:00:00Z"
}
```

Create requires a name containing 1–100 characters. Description is optional and limited to 1000 characters. Update fields are optional to support partial updates.

### Tasks

| Method | Route | Description |
|---|---|---|
| GET | `/api/projects/{projectId}/tasks` | List tasks in an owned project. |
| GET | `/api/projects/{projectId}/tasks/{taskId}` | Retrieve a task from an owned project. |
| POST | `/api/projects/{projectId}/tasks` | Create a task in an owned project. |
| PUT | `/api/projects/{projectId}/tasks/{taskId}` | Partially update a task in an owned project. |
| DELETE | `/api/projects/{projectId}/tasks/{taskId}` | Delete a task from an owned project. |

Create task fields:

```json
{
  "title": "Task title",
  "description": "Optional description",
  "status": 0,
  "priority": 1,
  "dueDate": "2027-01-31T00:00:00Z",
  "assigneeId": 2
}
```

Task statuses:

| Numeric value | Status |
|---|---|
| 0 | ToDo |
| 1 | InProgress |
| 2 | Review |
| 3 | Done |
| 4 | Blocked |

Task priorities:

| Numeric value | Priority |
|---|---|
| 0 | Low |
| 1 | Medium |
| 2 | High |
| 3 | Urgent |

## Testing

Run the complete .NET test suite:

```bash
dotnet test TaskFlow.slnx --nologo
```

Frontend lint:

```bash
cd TaskFlow.Client
npm run lint
```

Frontend production build:

```bash
cd TaskFlow.Client
npm run build
```

## Future Improvements

- Connect dashboard statistics to real API data
- Improve task filtering and sorting
- Add assignee selection using a user-list interface
- Enhance validation and error handling
- Deploy the application to a public environment
- Add additional authentication and API security hardening

## Author

Simphiwe Mbatha
