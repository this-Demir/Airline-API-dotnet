# Airline Company API

A RESTful backend API for an airline ticketing system built with **.NET 8** and **Clean Architecture**. The system supports flight inventory management, ticket purchasing with atomic seat reservation, passenger check-in with sequential seat assignment, and role-based access control for Admin, Customer, and Guest roles.

---

## Project Documentation (`docs/`)

Before diving into the code, it is highly recommended to review the comprehensive system specifications located in the `docs/` directory. These files serve as the absolute source of truth for the project's domain, rules, and constraints:

* **`docs/reqs.md`**: Contains the complete list of Functional and Non-Functional Requirements (e.g., pagination, rate-limiting, authentication rules).
* **`docs/business_specification.md`**: Details the core business processes, step-by-step operational logic (like the atomic ticket purchasing flow and check-in security), and expected API endpoints.
* **`docs/db_schema.md`**: Includes the Entity-Relationship (ER) diagram, our 3NF normalization strategy, and specific performance/indexing considerations.

--- 

## Tech Stack

| Concern | Technology |
|---|---|
| Framework | .NET 8 Web API |
| Architecture | Clean Architecture (4-layer) |
| Database | MySQL 8+ |
| ORM | Entity Framework Core 8 + `Pomelo.EntityFrameworkCore.MySql` |
| Authentication | JWT Bearer (ASP.NET Core Identity) |
| API Gateway | Ocelot / YARP (`AirlineSystem.Gateway` — added post-core) |
| Documentation | Swagger / OpenAPI (Swashbuckle) |
| Load Testing | k6 / JMeter |

---

## Architecture & Design Decisions

### Layer Structure

```
src/
  AirlineSystem.Domain/         # Pure POCOs, enums, repository interfaces — no external deps
  AirlineSystem.Application/    # DTOs, service interfaces, business logic orchestration
  AirlineSystem.Infrastructure/ # EF Core DbContext, repository implementations, JWT
  AirlineSystem.API/            # Controllers, middleware, DI composition root
```

**Dependency rule:** each layer only references the layer directly below it. The API project additionally references Infrastructure for DI wiring only.

### Key Design Decisions

| Decision | Detail |
|---|---|
| Round-trip booking | Buy Ticket handles one leg at a time. The client calls the endpoint twice for round trips. No multi-leg transaction. |
| `Passenger.FlightId` | Intentional denormalization — direct FK to Flight enables efficient manifest queries without joining through Booking. |
| Atomic seat reservation | `Flight.AvailableCapacity` uses a `RowVersion` (optimistic concurrency) token. EF Core raises a `DbUpdateConcurrencyException` on conflict, which the service layer retries or surfaces as "sold out". |
| Check-in validation | Check-in verifies (1) a Passenger record with that exact name exists for that flight/date, and (2) `IsCheckedIn == false`, before assigning a seat. |
| Seat numbering | Sequential integers starting at 1 per flight — assigned as `MAX(SeatNumber) + 1` at check-in time. |
| `UserRole` enum | Stored as its string name in MySQL (`.HasConversion<string>()` in EF config) to avoid silent bugs on enum reordering. |
| CSV date validation | Import asserts `(ArrivalDate - DepartureDate).TotalMinutes == DurationMinutes` to catch bad source data. |
| Rate limiting | Max 3 flight search requests per day per client IP, enforced at the API Gateway layer. |

---

## Assumptions

*(To be filled in during development)*

---

## Setup Instructions

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- MySQL 8+ server running locally or remotely
- `dotnet-ef` global tool: `dotnet tool install --global dotnet-ef`

### 1. Clone & Configure

```bash
git clone <repo-url>
cd Airline-API-dotnet
```

Copy the development settings template and fill in your values:

```bash
# appsettings.Development.json is gitignored — set your local values directly
```

Edit `src/AirlineSystem.API/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=AirlineSystemDb_Dev;User=root;Password=YOUR_PASSWORD;"
  },
  "JwtSettings": {
    "Secret": "your-secret-key-minimum-32-characters"
  }
}
```

### 2. Apply Migrations

```bash
dotnet ef database update \
  --project src/AirlineSystem.Infrastructure \
  --startup-project src/AirlineSystem.API
```

### 3. Run

```bash
dotnet run --project src/AirlineSystem.API
```

Swagger UI is available at `https://localhost:<port>/swagger`.

### Adding a New Migration

```bash
dotnet ef migrations add <MigrationName> \
  --project src/AirlineSystem.Infrastructure \
  --startup-project src/AirlineSystem.API
```
