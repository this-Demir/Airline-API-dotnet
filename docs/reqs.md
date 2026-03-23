# Software Requirements Specification (SRS)

**Project:** Airline Company API  
**Document Type:** Requirements Specification (reqs.md)

---

## 1. Introduction
This document defines the functional and non-functional requirements for the Airline Ticketing System API. The system shall provide backend services for flight management, query operations, ticket purchasing, and passenger check-in.

## 2. User Roles and Access Levels
The system shall support a Role-Based Access Control (RBAC) mechanism.

* **Admin:** Shall have full access to system management endpoints, including bulk flight uploads and passenger reporting.
* **Customer:** Shall represent a registered, authenticated user capable of purchasing tickets.
* **Guest:** Shall represent an unauthenticated user capable of searching for flights and performing flight check-in.

---

## 3. Functional Requirements (FR)

### FR-01: Identity and Access Management (IAM)
* **FR-01.01:** The system shall provide an endpoint for new user registration (Customer role).
* **FR-01.02:** The system shall provide an authentication endpoint that issues a JSON Web Token (JWT) or OAuth token upon successful login.

### FR-02: System Management (Administrative CRUD)
* **FR-02.01:** The system shall expose standard Create, Read, Update, and Delete (CRUD) endpoints for internal management of Airports and Flights.
* **FR-02.02:** Access to system management CRUD endpoints shall be strictly restricted to the Admin role.

### FR-03: Add Flight by File
* **FR-03.01:** The system shall allow administrators to upload a .csv file to populate the flight schedule.
* **FR-03.02:** The .csv file must contain the following fields: Flight number, date-from, date-to, airport-from, airport-to, duration, and capacity.
* **FR-03.03:** The endpoint shall return a transaction status and the file processing status.
* **FR-03.04:** This endpoint shall require authentication.
* **FR-03.05:** This endpoint shall not support paging.

### FR-04: Query Flight
* **FR-04.01:** The system shall allow users to search for flights using the following parameters: date-from, date-to, airport-from, airport-to, number of people, and one-way/round trip.
* **FR-04.02:** The system shall return a list of available flights, including Flight number and duration.
* **FR-04.03:** The system shall explicitly exclude flights that have no available seats (capacity = 0) from the search results.
* **FR-04.04:** The endpoint shall enforce rate limiting, allowing a maximum of 3 calls per day per client.
* **FR-04.05:** This endpoint shall implement pagination with a fixed page size of 10.
* **FR-04.06:** This endpoint shall not require authentication.

### FR-05: Buy Ticket
* **FR-05.01:** The system shall allow authenticated customers to purchase a ticket by submitting: Flight number, Date, and Passenger Name(s).
* **FR-05.02:** Upon a successful transaction, the system shall decrease the available capacity of the specific flight.
* **FR-05.03:** The system shall return the transaction status and a generated ticket number.
* **FR-05.04:** The system shall return a "sold out" status and reject the transaction if there are no seats left on the requested flight.
* **FR-05.05:** This endpoint shall require authentication.
* **FR-05.06:** This endpoint shall not support paging.

### FR-06: Passenger Check-in
* **FR-06.01:** The system shall allow users to check in by providing: Flight number, Date, and Passenger Name.
* **FR-06.02:** The system shall assign a seat to the passenger using a simple sequential numbering logic.
* **FR-06.03:** The system shall return the transaction status and a list containing the passenger and their assigned seat.
* **FR-06.04:** This endpoint shall not require authentication.
* **FR-06.05:** This endpoint shall not support paging.

### FR-07: Query Flight Passenger List
* **FR-07.01:** The system shall allow administrators to retrieve the passenger list for a specific flight by providing the Flight number and Date.
* **FR-07.02:** The system shall return the transaction status and the list of passengers.
* **FR-07.03:** This endpoint shall require authentication.
* **FR-07.04:** This endpoint shall implement pagination with a fixed page size of 10.

---

## 4. Non-Functional Requirements (NFR)

### NFR-01: System Architecture and Design
* **NFR-01.01:** The system shall be developed according to service-oriented principles.
* **NFR-01.02:** The API layer shall not contain direct database operations; business logic must be handled via intermediate service layers.
* **NFR-01.03:** The system shall utilize Data Transfer Objects (DTOs) for all client-server communication.
* **NFR-01.04:** All RESTful services shall support versioning.

### NFR-02: API Gateway and Rate Limiting
* **NFR-02.01:** The architecture must incorporate an API Gateway.
* **NFR-02.02:** All backend APIs must be configured and routed through the API Gateway.
* **NFR-02.03:** The API Gateway shall enforce the daily rate limit of 3 requests for the Query Flight endpoint.

### NFR-03: API Documentation
* **NFR-03.01:** The system shall generate and expose API documentation using Swagger UI or an equivalent OpenAPI standard.

### NFR-04: Deployment and Hosting
* **NFR-04.01:** The API and database must be deployed to a recognized cloud provider: Azure, AWS, or Google Cloud.
* **NFR-04.02:** The use of hosting services such as Render or Vercel is strictly prohibited.

### NFR-05: Performance and Load Testing
* **NFR-05.01:** The system shall be evaluated through basic load testing using tools such as k6 or JMeter.
* **NFR-05.02:** At least two core API endpoints (e.g., Query Flight, Buy Ticket) shall be subjected to the load test.
* **NFR-05.03:** The load test shall simulate concurrent usage across three scenarios: Normal Load (20 virtual users), Peak Load (50 virtual users), and Stress Load (100 virtual users).
* **NFR-05.04:** Each load test scenario shall run for a minimum duration of 30 seconds.
* **NFR-05.05:** The load testing process must collect and report the following metrics: Average response time, 95th percentile response time (p95), Requests per second, and Error rate.