# Assumptions — Airline Ticketing System API

This document records every assumption made during design and implementation where the assignment specification (SE4458 Midterm, Group 1) was silent, ambiguous, or open to interpretation. Each assumption states the gap in the spec, the decision taken, and the rationale.

---

## 1. Check-in Input Fields

**Spec:** "Flight number, Date, Passenger Name"

**Assumption:** Check-in accepts `PnrCode` + `PassengerName` instead of `FlightNumber` + `Date` + `PassengerName`.

**Rationale:** The spec-prescribed input (name + flight + date) allows any party who knows a passenger's name and travel details to check them in without any booking reference. Replacing the flight/date pair with the PNR (booking reference) ties check-in to a specific transaction, preventing name-guessing attacks. The PNR is already returned to the customer at purchase time and is the natural identifier for this operation in real airline systems.

---

## 2. Round-Trip Booking — One Leg at a Time

**Spec:** Query Flight accepts a "one way / round trip" flag, but the Buy Ticket spec is silent on multi-leg handling.

**Assumption:** Buy Ticket handles a single flight leg per call. For a round trip the client calls the endpoint twice — once for the outbound leg, once for the return leg. Each call generates its own PNR.

**Rationale:** A single-leg transaction is simpler to make atomic and keeps the capacity decrement logic self-contained. The spec does not mention a combined transaction, and real booking systems routinely issue separate PNRs per segment.

---

## 3. Sold-Out Exclusion Threshold

**Spec:** "Flights that have no seats should not be listed."

**Assumption:** A flight is excluded from search results when `AvailableCapacity < numberOfPeople` requested, not only when `AvailableCapacity == 0`.

**Rationale:** Returning a flight that technically has seats but cannot accommodate the entire party would require the client to do capacity arithmetic and would result in a failed purchase. Filtering at the query level gives a more useful result set.

---

## 4. Ticket Number (PNR) Format

**Spec:** "Return ticket number" — no format specified.

**Assumption:** The ticket number is a 6-character uppercase alphanumeric string (PNR code) generated with `Guid.NewGuid().ToString("N")[..6].ToUpper()`.

**Rationale:** 6-character alphanumeric PNRs are the de-facto standard in the airline industry (e.g., Turkish Airlines, IATA). The uniqueness risk over a realistic booking volume is negligible; a `UNIQUE` constraint on the column enforces it at the database level.

---

## 5. Sequential Seat Numbering Scope

**Spec:** "Assign seat (simple numbering) to Passenger on flight."

**Assumption:** Seat numbers are sequential integers starting at 1, scoped per flight (not per flight + date). The next number is computed as `MAX(SeatNumber) + 1` across all passengers for that flight at check-in time.

**Rationale:** The spec says "simple numbering" without defining the scope. Per-flight numbering is the most natural interpretation and avoids resetting seat counters per departure date, which would complicate the manifest query.

---

## 6. Airport as a First-Class Entity

**Spec:** "airport-from, airport-to" appear as plain string parameters in flight management.

**Assumption:** Airports are normalized into a dedicated `Airport` table with an IATA code (e.g., `IST`, `JFK`), name, and city. Flights reference airports by foreign key. The CSV and individual flight creation endpoints accept IATA codes, which are resolved to airport IDs in the service layer.

**Rationale:** Storing airport names as free-text strings on each flight would create redundancy and make route-based queries fragile. Normalization to 3NF eliminates duplication and allows airports to be managed independently.

---

## 7. Admin Role — No Self-Registration

**Spec:** "Authentication: YES" for Add Flight, Buy Ticket, and Query Passenger List, but the registration endpoint is not described.

**Assumption:** The public `POST /auth/register` endpoint creates Customer-role accounts only. Admin accounts must be seeded directly into the database. There is no self-registration path for the Admin role.

**Rationale:** Allowing anyone to register as an Admin would defeat the purpose of role-based access control. Admin seeding is a standard operational practice.

---

## 8. Pagination — Fixed Page Size, Not Client-Configurable

**Spec:** "Paging (size of 10)" for Query Flight and Query Passenger List.

**Assumption:** Page size is fixed at 10 and cannot be overridden by the client. The client supplies only `pageNumber` (1-indexed). Endpoints that do not require paging (Buy Ticket, Check-in, Add Flight, Add Flight by File) return all results in a single response.

**Rationale:** The spec states a fixed size of 10. Making it configurable would contradict the spec and introduce an unspecified upper-bound concern.

---

## 9. Rate Limiting — Per Client IP, Gateway-Enforced, Flight Search Only

**Spec:** "Limit calls to 3 per day" under Query Flight.

**Assumption:** The 3-calls-per-day limit applies to `GET /api/v1/flights/search` only, is enforced at the Ocelot API Gateway level (not in the core API), and is keyed on the client's IP address. All other endpoints are not rate-limited.

**Rationale:** The spec scopes the limit to Query Flight. Enforcing it at the gateway is consistent with NFR-02.03 and keeps rate-limiting logic out of business code. IP-based keying is the standard approach for unauthenticated endpoints.

---

## 10. JWT Authentication — HS256, No Refresh Tokens

**Spec:** "For authentication, JWT or OAuth can be implemented."

**Assumption:** JWT Bearer tokens signed with HS256 are used. Tokens are issued at login with a configurable expiry. No refresh-token mechanism is implemented.

**Rationale:** HS256 JWT is the simplest compliant choice for a backend-only API. Refresh tokens are not mentioned in the spec and would add significant scope.

---

## 11. UserRole Stored as String

**Spec:** No storage format specified for roles.

**Assumption:** The `Role` column in the `User` table stores the role name as a string (e.g., `"Admin"`, `"Customer"`) rather than its enum integer ordinal.

**Rationale:** Storing ordinals creates silent bugs if the enum order is ever changed. String storage is self-documenting and safe to inspect directly in the database.

---

## 12. CSV Validation — Duration Cross-Check

**Spec:** CSV fields include `duration` and date-from / date-to.

**Assumption:** During CSV import, the parser validates that `(ArrivalDate - DepartureDate).TotalMinutes == DurationMinutes`. Rows that fail this check are rejected with a per-row error and do not block the rest of the file.

**Rationale:** The spec provides both a duration field and explicit departure/arrival timestamps. Accepting inconsistent data would corrupt flight schedules. Per-row rejection allows partial success on large uploads.

---

## 13. CSV Duplicate Detection — FlightNumber + DepartureDate Composite Key

**Spec:** "Adds all the flights in the file" — no duplicate handling described.

**Assumption:** A `(FlightNumber, DepartureDate)` combination is treated as a natural unique key. Rows that duplicate an existing database record or a prior row within the same file are rejected with a per-row error. The remaining rows are still processed.

**Rationale:** Inserting duplicate flight schedules would double capacity and corrupt availability data. Composite key uniqueness is a standard operational constraint in airline scheduling.

---

## 14. Passenger Denormalization — FlightId on Passenger Table

**Spec:** No explicit data model prescribed.

**Assumption:** The `Passenger` table carries both `BookingId` (FK to the PNR transaction) and `FlightId` (direct FK to the flight), even though `FlightId` is derivable through the booking. Both are always populated at purchase time.

**Rationale:** The passenger manifest query (`GET /flights/{flightNo}/passengers`) needs to filter passengers by flight without joining through Booking. The direct `FlightId` FK enables an efficient single-table lookup and supports the composite index on `(FlightNumber, DepartureDate)`.

---

## 15. Optimistic Concurrency on AvailableCapacity

**Spec:** "Capacity of flight will be decreased. Return sold out if there are no seats left." — no concurrency mechanism specified.

**Assumption:** `Flight.AvailableCapacity` uses EF Core's optimistic concurrency via a `RowVersion` / `Timestamp` column. A `DbUpdateConcurrencyException` on concurrent purchases is caught and treated as a sold-out signal rather than a server error.

**Rationale:** Without a concurrency control mechanism, concurrent purchases could oversell a flight. Optimistic concurrency is the standard EF Core pattern and avoids pessimistic row locks that would serialize all ticket purchases.

---

## 16. API Versioning — URL Path Prefix

**Spec:** "All REST services must be versionable."

**Assumption:** Versioning is implemented via URL path prefix: all routes are under `/api/v1/`. No query-string or header-based versioning is used.

**Rationale:** URL path versioning is the most explicit and cacheable strategy. It is the approach demonstrated in class examples and is widely understood by API consumers.

---

## 17. API Gateway — Ocelot (Self-Hosted)

**Spec:** "You need to implement an API gateway and configure all APIs in the gateway."

**Assumption:** The gateway is implemented as a separate .NET 8 project (`AirlineSystem.Gateway`) using the Ocelot library and MMLib.SwaggerForOcelot for Swagger aggregation. It runs on port 5000 and proxies all traffic to the core API.

**Rationale:** A self-hosted Ocelot gateway satisfies the requirement without incurring cloud-managed gateway costs (AWS API Gateway charges per million requests). It also keeps the full stack runnable locally via `docker-compose`.

---

## 18. Cloud Deployment — AWS EC2

**Spec:** "Use cloud service Azure, AWS or Google Cloud."

**Assumption:** The system is deployed on a single AWS EC2 instance running Docker Compose. Both the core API and the gateway run as containers on the same host. MySQL runs as a third container on the same instance. Container images are stored in Amazon ECR.

**Rationale:** EC2 with Docker Compose provides a self-contained, cost-effective deployment that mirrors the local development stack. The project was initially deployed to Azure App Service but migrated to AWS EC2 due to subscription credit constraints and simpler port-forwarding control.

---

## 19. No Payment Processing

**Spec:** Buy Ticket returns "transaction status, ticket number" — no payment fields mentioned.

**Assumption:** No payment processing is implemented. A successful capacity check and decrement is sufficient to confirm a ticket purchase. The system is a booking backend, not a payment system.

**Rationale:** The spec does not mention payment, pricing, or currency at any point.

---

## 20. Guest Role — Unauthenticated Users

**Spec:** References "Guest" as a user type for query and check-in but does not describe a Guest registration or token flow.

**Assumption:** Guest access means no authentication token is required. `GET /flights/search` and `POST /checkin` are completely public endpoints (`[AllowAnonymous]`). There is no Guest JWT or session concept.

**Rationale:** The spec's auth table marks both Query Flight and Check-in as "NO" for authentication. A dedicated Guest token flow would add unnecessary complexity.
