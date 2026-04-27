# WorkClock — Employee Attendance System

A full-stack work clock system that lets employees clock in and out of shifts. Clock times are sourced from an **external time API** (`timeapi.io`, Europe/Zurich zone) — never from the browser or local server clock — to prevent tampering.

## Architecture

```
┌─────────────────────┐        HTTP/JSON        ┌──────────────────────┐
│   React Frontend    │ ◄─────────────────────► │  ASP.NET Core API    │
│   (port 5173)       │                         │  (port 5000)         │
└─────────────────────┘                         └──────────┬───────────┘
                                                           │ EF Core
                                                           ▼
                                                ┌──────────────────────┐
                                                │  SQL Server          │
                                                │  (WorkClockDb)       │
                                                └──────────────────────┘
                                                           
                                          ┌────────────────────────────┐
                                          │  timeapi.io (external)     │
                                          │  /api/v1/time/current/zone │
                                          └────────────────────────────┘
```

**Key design decisions:**

- **External time source** — `TimeService` calls timeapi.io for every clock event. A `TimeServiceException` is surfaced as `503 Service Unavailable` so the client can show a meaningful error rather than recording a wrong time.
- **UTC storage** — all timestamps are stored as UTC in `datetime2` columns. The frontend converts to local/Zurich time for display.
- **Idempotency guards** — clocking in while already in, or clocking out while already out, returns `409 Conflict`.
- **Audit trail** — the client IP is recorded on every clock-in for compliance purposes.

---

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ | `dotnet --version` to check |
| [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) | 2019+ or LocalDB | LocalDB ships with Visual Studio |
| [Node.js](https://nodejs.org/) | 18+ | For the React frontend |

---

## Backend Setup & Run

### 1. Clone and restore

```bash
git clone <repo-url>
cd "dotnet work clock"
dotnet restore
```

### 2. Configure the database connection

Open [WorkClock.Api/appsettings.json](WorkClock.Api/appsettings.json). The default connection string uses Windows Authentication on `localhost`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=WorkClockDb;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

If you use SQL Server Express or LocalDB, change it to:

```json
"DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=WorkClockDb;Trusted_Connection=True;"
```

### 3. Run the API

```bash
dotnet run --project WorkClock.Api/WorkClock.Api.csproj
```

On first run the app automatically applies EF Core migrations and creates the `WorkClockDb` database. You should see:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started.
```

> **No SQL Server?** The API will still start and Swagger will load; only the clock endpoints will fail until a database is available.

### 4. Explore the API in Swagger

Open **http://localhost:5000/swagger** in your browser.

You will see four endpoints:

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/clock/in` | Clock an employee in |
| `POST` | `/api/clock/out` | Clock an employee out |
| `GET` | `/api/clock/status/{employeeId}` | Current status for an employee |
| `GET` | `/api/clock/history/{employeeId}` | Full attendance history |

---

## Testing the API Manually

### Clock in

```bash
curl -X POST http://localhost:5000/api/clock/in \
  -H "Content-Type: application/json" \
  -d '{"employeeId": "EMP001"}'
```

**Expected response (201 Created):**
```json
{
  "id": 1,
  "employeeId": "EMP001",
  "clockInUtc": "2026-04-27T09:00:00Z",
  "clockOutUtc": null,
  "status": "ClockedIn"
}
```

### Clock out

```bash
curl -X POST http://localhost:5000/api/clock/out \
  -H "Content-Type: application/json" \
  -d '{"employeeId": "EMP001"}'
```

**Expected response (200 OK):**
```json
{
  "id": 1,
  "employeeId": "EMP001",
  "clockInUtc": "2026-04-27T09:00:00Z",
  "clockOutUtc": "2026-04-27T17:00:04Z",
  "status": "ClockedOut"
}
```

### Check status

```bash
curl http://localhost:5000/api/clock/status/EMP001
```

### View history

```bash
curl http://localhost:5000/api/clock/history/EMP001
```

### Edge cases to try

| Scenario | How to trigger | Expected result |
|---|---|---|
| Clock in twice | Call `/api/clock/in` for the same `employeeId` twice | Second call returns `409 Conflict` |
| Clock out without clocking in | Call `/api/clock/out` with a fresh `employeeId` | Returns `409 Conflict` |
| Empty employee ID | Send `{"employeeId": ""}` | Returns `400 Bad Request` |

---

## Running the Unit Tests

```bash
dotnet test WorkClock.Tests/WorkClock.Tests.csproj --logger "console;verbosity=normal"
```

**No SQL Server or internet connection is required** — tests use an EF Core in-memory database and a mocked HTTP client.

You should see output similar to:

```
Passed!  - Failed: 0, Passed: 19, Skipped: 0, Total: 19
```

### Test coverage summary

| Test class | What it covers |
|---|---|
| `TimeServiceTests` | Happy-path UTC conversion, network failure, timeout, non-200 HTTP, invalid JSON, missing `date_time` field, unparseable date value |
| `ClockControllerTests` | Clock-in success/already-in/empty-id/time-service-down, clock-out success/not-in/already-out/time-service-down, status (in/out/not-found/most-recent), history (filtered by employee, ordered newest-first, empty) |

To run with code coverage:

```bash
dotnet test WorkClock.Tests/WorkClock.Tests.csproj --collect:"XPlat Code Coverage"
```

---

## Frontend Setup & Run

> The React frontend lives in the `work-clock-ui/` directory.

```bash
cd work-clock-ui
npm install
npm run dev
```

Open **http://localhost:5173** in your browser.

### What the UI looks like

**Main screen — employee not clocked in:**

```
┌────────────────────────────────────────┐
│           Work Clock                   │
│                                        │
│  Employee ID:  [ EMP001          ]     │
│                                        │
│         [ Clock In ]                   │
│                                        │
│  Status: Not clocked in                │
└────────────────────────────────────────┘
```

**After clocking in:**

```
┌────────────────────────────────────────┐
│           Work Clock                   │
│                                        │
│  Employee ID:  [ EMP001          ]     │
│                                        │
│         [ Clock Out ]                  │
│                                        │
│  Status:  ● Clocked in                 │
│  Since:   09:02:14 (Europe/Zurich)     │
└────────────────────────────────────────┘
```

**After clocking out:**

```
┌────────────────────────────────────────┐
│           Work Clock                   │
│                                        │
│  Employee ID:  [ EMP001          ]     │
│                                        │
│         [ Clock In ]                   │
│                                        │
│  Status:  ○ Clocked out                │
│  Duration: 7 h 58 m                    │
└────────────────────────────────────────┘
```

**Error — time service unavailable:**

```
┌────────────────────────────────────────┐
│  ⚠ Could not record clock time.        │
│  The external time service is          │
│  temporarily unavailable. Please       │
│  try again in a moment.                │
└────────────────────────────────────────┘
```

---

## Project Structure

```
dotnet work clock/
├── WorkClock.Api/
│   ├── Controllers/
│   │   ├── ClockController.cs     # POST /in, POST /out, GET /status, GET /history
│   │   └── Dtos.cs                # ClockRequest, ClockResponse records
│   ├── Data/
│   │   └── AppDbContext.cs        # EF Core context + model configuration
│   ├── Exceptions/
│   │   └── TimeServiceException.cs
│   ├── Migrations/                # EF Core migrations (auto-applied on startup)
│   ├── Models/
│   │   └── AttendanceRecord.cs    # Database entity
│   ├── Properties/
│   │   └── launchSettings.json    # Sets ASPNETCORE_ENVIRONMENT=Development
│   ├── Services/
│   │   ├── ITimeService.cs
│   │   └── TimeService.cs         # Calls timeapi.io, converts to UTC
│   ├── appsettings.json
│   └── Program.cs
├── WorkClock.Tests/
│   ├── Controllers/
│   │   └── ClockControllerTests.cs
│   └── Services/
│       └── TimeServiceTests.cs
└── WorkClock.sln
```
