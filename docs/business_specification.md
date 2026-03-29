
---
# Airline Company API - Business Specification

## Identified Critical Business Processes

| **Process** | **Description** | **Primary Actors** | **System Components Involved** |
| --- | --- | --- | --- |
| **P1 – Identity & Auth** | Customer registration, login, and JWT-based role management (Admin/Customer/Guest). | Admin, Customer, Guest | Auth Service, Identity Provider, Database |
| **P2 – Inventory Setup** | Manual and bulk management of flights and airports. | Admin | Flight Service, File Processor, Database |
| **P3 – Discovery & Search** | Querying available flights with capacity validation and rate limiting. | Guest, Customer | Search Engine, API Gateway, Cache/Rate-Limiter |
| **P4 – Ticketing & Sale** | Atomic seat reservation, capacity decrement, and PNR generation. | Customer | Booking Service, Transaction Manager, Database |
| **P5 – Boarding & Check-in** | Sequential seat assignment and passenger validation. | Guest, Customer | Check-in Service, Seat Allocator, Database |
| **P6 – Reporting** | Monitoring passenger lists for specific flight schedules. | Admin | Reporting Service, Database |

---

## Process 2: Inventory Setup & Management 

| **Step** | **Action** | **System Component** | **Required Data** | **Estimated API Endpoint** |
| --- | --- | --- | --- | --- |
| 1 | Admin logs in and provides CSV file for bulk flight entry. | File Processor | `.csv` (FlightNo, Date, Route, Capacity) | `POST /api/v1/flights/upload` |
| 2 | System validates CSV format and ensures no duplicate FlightNo/Date exists. | Flight Service | `List<FlightDTO>` | `POST /api/v1/flights/upload` |
| 3 | System creates individual flight records in the database. | Database | `FlightEntity` | `POST /api/v1/flights` |
| 4 | Admin manages individual airports (Create/Update/Delete) for route definitions. | Airport Service | `AirportName`, `Code`, `City` | `POST /api/v1/airports` |

---

## Process 3: Discovery & Search 

| **Step** | **Action** | **System Component** | **Required Data** | **Estimated API Endpoint** |
| --- | --- | --- | --- | --- |
| 1 | User initiates flight search; API Gateway checks daily call limit (Max 3). | API Gateway | `ClientIP`, `EndpointPath` | `GET /api/v1/flights/search` |
| 2 | System filters flights by route and date range. | Search Service | `From`, `To`, `DateRange` | `GET /api/v1/flights/search` |
| 3 | System excludes flights with zero capacity or insufficient seats for 'Number of people'. | Business Logic | `RequiredSeats`, `CurrentCapacity` | `GET /api/v1/flights/search` |
| 4 | For Round-Trip, system performs secondary search for return leg. | Search Service | `ReturnDate`, `OriginalFrom/To` | `GET /api/v1/flights/search` |
| 5 | System returns paginated results (Size: 10). | API Layer | `PageNumber`, `PageSize` | `GET /api/v1/flights/search` |

---

## Process 4: Ticketing & Sale 

| **Step** | **Action** | **System Component** | **Required Data** | **Estimated API Endpoint** |
| --- | --- | --- | --- | --- |
| 1 | Customer selects flight and provides passenger names. | Booking Service | `FlightId`, `PassengerNames` | `POST /api/v1/tickets/purchase` |
| 2 | System performs atomic check on current capacity vs request. | Transaction Manager | `FlightId`, `Count` | `POST /api/v1/tickets/purchase` |
| 3 | If capacity < seats requested, system returns "Sold Out". | Business Logic | `AvailabilityStatus` | `POST /api/v1/tickets/purchase` |
| 4 | System decrements capacity and generates unique Ticket Number (PNR). | Database / Generator | `FlightId`, `NewCapacity` | `POST /api/v1/tickets/purchase` |

---

## Process 5: Boarding & Check-in Logic

| **Step** | **Action** | **System Component** | **Required Data** | **Estimated API Endpoint** |
| --- | --- | --- | --- | --- |
| 1 | User provides PNR code and Passenger Name for check-in. | Check-in Service | `PnrCode`, `PassengerName` | `POST /api/v1/checkin` |
| 2 | System looks up the booking by PNR and verifies the passenger name matches. | Database | `PnrCode`, `PassengerName` | `POST /api/v1/checkin` |
| 3 | System assigns next available sequential seat number (1, 2, 3...). | Seat Allocator | `FlightId`, `LastAssignedSeat` | `POST /api/v1/checkin` |
| 4 | System persists seat assignment and returns full passenger/seat list. | Database | `SeatNo`, `PassengerId` | `POST /api/v1/checkin` |

---