# WorkClock — Employee Attendance Tracker

A full-stack attendance tracking system built with **React 18**, **ASP.NET Core 10**, and **MS SQL Server**.

---

## Architecture

```
┌────────────────────────┐     HTTP/JSON     ┌──────────────────────────┐
│  React + Vite          │ ◄───────────────► │  ASP.NET Core 10 Web API │
│  (localhost:5173)      │                   │  (localhost:5000)        │
└────────────────────────┘                   └───────────┬──────────────┘
                                                         │ EF Core 8
                                                         ▼
                                             ┌──────────────────────────┐
                                             │  SQL Server 2022         │
                                             │  WorkClockDb             │
                                             └──────────────────────────┘

                                     ┌───────────────────────────────────┐
                                     │  timeapi.io  (external time API)  │
                                     │  GET /api/v1/time/current/zone    │
                                     └───────────────────────────────────┘
```

### Project layout

```
WorkClock.sln
├── WorkClock.Api/
│   ├── Controllers/        AttendanceController.cs
│   ├── Data/               AppDbContext.cs
│   ├── Dtos/               ClockRequestDto.cs  AttendanceRecordDto.cs
│   ├── Exceptions/         TimeServiceException.cs
│   ├── Migrations/         EF Code-First migration
│   ├── Models/             AttendanceRecord.cs
│   ├── Services/           ITimeService.cs  TimeService.cs
│   ├── Program.cs
│   └── appsettings.json
├── WorkClock.Tests/
│   ├── Controllers/        ClockControllerTests.cs
│   └── Services/           TimeServiceTests.cs
└── workclock-frontend/
    ├── src/
    │   ├── components/     ClockControls.jsx  AttendanceTable.jsx
    │   ├── services/       api.js
    │   ├── App.jsx
    │   └── main.jsx
    ├── vite.config.js
    └── package.json
```

---

## First-Time Setup

### Prerequisites

| Tool                                                      | Min version |
| --------------------------------------------------------- | ----------- |
| [.NET SDK](https://dotnet.microsoft.com/download)         | 10.0        |
| [Node.js](https://nodejs.org/)                            | 18          |
| [Docker](https://www.docker.com/products/docker-desktop/) | any recent  |

### 1 — Start SQL Server (Docker)

```bash
docker compose up -d
```

Starts SQL Server 2022 Developer on `localhost:1433`.  
Credentials: `sa` / `WorkClock@2024!`

> If you have a local SQL Server instance, update the connection string in
> `WorkClock.Api/appsettings.json` to point to it instead.

### 2 — Install frontend dependencies

```bash
cd workclock-frontend
npm install
```

Only needed once (or after `package.json` changes).

### 3 — Run the API

```bash
cd WorkClock.Api
dotnet run
```

On first start the API automatically applies the EF Core migration and creates the database.  
Swagger UI → `https://localhost:5000/swagger`

### 4 — Run the Frontend

```bash
cd workclock-frontend
npm run dev
```

Open **http://localhost:5173**.  
Vite proxies `/api/*` to the ASP.NET Core backend — no manual CORS setup needed.

---

## Quick Start (already set up)

If you have already run the setup steps above, starting the app only requires three commands (in separate terminals):

```bash
# Terminal 1 — database
docker compose up -d

# Terminal 2 — API
cd WorkClock.Api && dotnet run

# Terminal 3 — frontend
cd workclock-frontend && npm run dev
```

Open **http://localhost:5173**.

---

## For Developers

### Running the unit tests

The test suite uses **xUnit** and covers the `TimeService` (mocked HTTP) and `ClockController` (EF Core in-memory database). No running database or external services are required.

```bash
# From the solution root
dotnet test

# With verbose output
dotnet test --logger "console;verbosity=detailed"

# Run only a specific test project
dotnet test WorkClock.Tests/WorkClock.Tests.csproj
```

All 30 tests should pass. The suite is split into:

| File | Tests | What it covers |
| ---- | ----- | -------------- |
| `WorkClock.Tests/Services/TimeServiceTests.cs` | 12 | HTTP success, network errors, malformed JSON responses |
| `WorkClock.Tests/Controllers/ClockControllerTests.cs` | 18 | Clock-in/out business rules, status, history, 400/409/503 responses |

---

## API Reference

| Method | Path                                        | Body                         | Description                |
| ------ | ------------------------------------------- | ---------------------------- | -------------------------- |
| `POST` | `/api/attendance/clockin`                   | `{ "employeeId": "EMP001" }` | Record a clock-in          |
| `POST` | `/api/attendance/clockout`                  | `{ "employeeId": "EMP001" }` | Record a clock-out         |
| `GET`  | `/api/attendance/history?employeeId=EMP001` | —                            | Full history, newest first |

### Response shape (all endpoints)

```json
{
  "id": 1,
  "employeeId": "EMP001",
  "clockInUtc": "2026-04-27T09:02:14.123Z",
  "clockOutUtc": "2026-04-27T17:58:09.456Z",
  "durationMinutes": 535.9,
  "sourceIp": "::1"
}
```

> **Note on timestamps:** values are Zurich local time (Europe/Zurich). The trailing `Z` is a
> serialization artifact; the frontend always displays them without offset conversion.

### Error codes

| HTTP                      | Cause                                             |
| ------------------------- | ------------------------------------------------- |
| `400 Bad Request`         | Clock-out attempted with no active clock-in today |
| `409 Conflict`            | Clock-in attempted when already clocked in today  |
| `503 Service Unavailable` | External time API is unreachable                  |

---

## Design Decisions

### Time storage

All timestamps are stored as Zurich local time (`Europe/Zurich`). The authoritative time is
fetched from `timeapi.io` on every clock-in and clock-out; the Zurich wall-clock value is stored
directly in the `datetime2` column. The frontend always renders times as-is without applying any
browser timezone offset, so what you see matches what is in the database.

### How we handle external API availability

The authoritative time is fetched from `timeapi.io` on every clock-in and clock-out. Because this is an external HTTP call it can fail, and we treat it as a hard requirement.

| Layer                                | Mechanism                                                                                                                                                                                           |
| ------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **HttpClientFactory + 10 s timeout** | Prevents socket exhaustion and hanging requests                                                                                                                                                     |
| **`TimeServiceException` boundary**  | All network/parse failures are caught in `TimeService`, logged at `Error` level with the raw response body for diagnostics, and re-thrown as a typed exception                                      |
| **Controller → 503**                 | `AttendanceController` catches `TimeServiceException` and returns `503 Service Unavailable` with a user-friendly JSON error body                                                                    |
| **React loading state**              | Buttons are disabled and an animated note ("Fetching authoritative time from external API…") is shown while the request is in flight, setting the right latency expectation                         |
| **Red error banner**                 | A 503 or any non-OK response surfaces as a dismissing banner in the UI — no silent failures                                                                                                         |
| **No silent fallback**               | We deliberately do **not** fall back to `DateTime.UtcNow`. Falling back would mask failures and could allow time manipulation. If the authoritative source is down, the action is cleanly rejected. |

---

## Database Schema

```sql
CREATE TABLE AttendanceRecords (
    Id          INT           IDENTITY(1,1) PRIMARY KEY,
    EmployeeId  NVARCHAR(100) NOT NULL,
    ClockIn     DATETIME2     NULL,
    ClockOut    DATETIME2     NULL,
    SourceIp    NVARCHAR(45)  NULL
);

CREATE INDEX IX_AttendanceRecords_EmployeeId_ClockIn
    ON AttendanceRecords (EmployeeId, ClockIn);
```
