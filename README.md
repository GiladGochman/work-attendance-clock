# WorkClock — Employee Attendance Tracker

A full-stack attendance tracking system built with **React 18**, **ASP.NET Core 10**, and **MS SQL Server**.

---

## Architecture

```
┌────────────────────────┐     HTTP/JSON     ┌──────────────────────────┐
│  React + Vite          │ ◄───────────────► │  ASP.NET Core 10 Web API │
│  (localhost:5173)      │                   │  (localhost:7000)        │
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

## Getting Started

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

### 2 — Run the API

```bash
cd WorkClock.Api
dotnet run
```

On first start the API automatically applies the EF Core migration and creates the database.  
Swagger UI → `https://localhost:5000/swagger`

### 3 — Run the Frontend

```bash
cd workclock-frontend
npm install
npm run dev
```

Open **http://localhost:5173**.  
Vite proxies `/api/*` to the ASP.NET Core backend — no manual CORS setup needed.

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
  "clockInUtc": "2026-04-27T07:02:14.123Z",
  "clockOutUtc": "2026-04-27T15:58:09.456Z",
  "durationMinutes": 535.9,
  "sourceIp": "::1"
}
```

### Error codes

| HTTP                      | Cause                                             |
| ------------------------- | ------------------------------------------------- |
| `400 Bad Request`         | Clock-out attempted with no active clock-in today |
| `409 Conflict`            | Clock-in attempted when already clocked in today  |
| `503 Service Unavailable` | External time API is unreachable                  |

---

## Design Decisions

### Why UTC in the database

Storing local (wall-clock) time is a persistent source of bugs:

- **DST transitions** make certain times ambiguous or non-existent (e.g., `02:30` on a spring-forward night).
- **Multi-timezone employees** produce data that cannot be compared without knowing each record's origin timezone.
- **Timezone rule changes** (governments do change DST rules) silently corrupt historical records stored in local time.

UTC is a monotonically increasing, timezone-free reference. Both `ClockIn` and `ClockOut` are stored as `datetime2` UTC. The React frontend converts to the browser's local timezone at display time using `Date.prototype.toLocaleString()`, so every user sees their own local clock while the database stays unambiguous.

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
