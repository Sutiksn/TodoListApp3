# TaskFlow Enterprise

An enterprise-grade collaborative task, project management, and organization governance platform built with **ASP.NET Core Clean Architecture** and a **responsive, modular JavaScript frontend**.

---

## Architecture Overview

```text
TodoListApp3/
├── backend/
│   ├── API/                  # Controllers & HTTP Endpoints
│   ├── Application/          # DTOs, Business Services & Interfaces
│   ├── Domain/               # Entities & Core Business Models
│   └── Infrastructure/       # EF Core DbContext, PostgreSQL & Migrations
├── frontend/
│   ├── css/                  # Modern UI Stylesheets
│   ├── js/
│   │   ├── components/       # Feature Components (Auth, Task, Org, Project, Admin)
│   │   ├── core/             # API Client, Client Router & State Management
│   │   └── utils/            # UI Utilities & Helpers
│   └── index.html            # Single Page Application (SPA)
├── Dockerfile                # Multi-stage production container build
├── render.yaml               # Render Cloud Blueprint deployment
└── README.md
```

---

## How to Execute Locally

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/) (or Visual Studio 2026 / VS Community)
- PostgreSQL database (Local PostgreSQL or cloud instances such as [Neon](https://neon.tech/))

### 1. Configure Secrets & Database
Copy the template configuration to create your local `appsettings.json`:
```powershell
cp backend/appsettings.example.json backend/appsettings.json
```
Edit `backend/appsettings.json` and provide your PostgreSQL connection string:
```json
"ConnectionStrings": {
  "DefaultConnection": "postgresql://<USER>:<PASSWORD>@<HOST>/<DATABASE>?sslmode=require"
}
```

### 2. Run the Application
You can run the application directly using the .NET CLI:
```powershell
cd backend
dotnet run --urls "http://localhost:5000"
```
Or open `TodoListApp3.sln` in Visual Studio Community and press **F5** (`http://localhost:5000`).

### 3. Open in Browser
Navigate to:
```text
http://localhost:5000
```

---

## Application Flow & Role Hierarchy

```text
Platform Admin  ──────► Manages platform telemetry, system users & approves OrgAdmin requests
      │
Org Admin       ──────► Registers & launches collaborative organization workspaces
      │
Manager         ──────► Oversees sprint velocity, project tracks & team task assignments
      │
Employee        ──────► Executes milestone deliverables & updates task status
      │
Standard User   ──────► Private personal focus board & submits organization access requests
```

1. **Sign Up & OTP Verification**: Users register and verify their email via 6-digit OTP.
2. **Personal Workspace**: Automatically provisioned for personal task management.
3. **Role & Organization Requests**: Users can apply to join or manage organizations.
4. **Governance & Isolation**: Org Admins and Employees only see data within their enrolled organization.
5. **Multi-Tab Concurrency**: Independent `sessionStorage` architecture allows Admin and User sessions across separate tabs simultaneously without session collisions.

---

## Cloud Deployment (Render / Docker)

1. Connect your Git repository to **Render**.
2. Render detects `render.yaml` and `Dockerfile` automatically.
3. Add your `DATABASE_URL` environment variable pointing to your Neon PostgreSQL instance.
4. Deploy!
