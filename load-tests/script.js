/**
 * Airline API — k6 Chaos Load Test
 * ──────────────────────────────────────────────────────────────────────────────
 * Run:
 *   k6 run \
 *     -e K6_ADMIN_EMAIL=admin@airline.com \
 *     -e K6_ADMIN_PASSWORD=Password123! \
 *     -e BASE_URL=http://localhost:5203 \
 *     --tag application=airline-api \
 *     --out influxdb=http://localhost:8086/k6 \
 *     load-tests/script.js
 *
 * IMPORTANT — Grafana setup for Dashboard 10660:
 *
 *   Fix 1 — "No Data" on top panels:
 *     The --tag application=airline-api flag (above) is REQUIRED. Without it the
 *     panels filter WHERE application =~ /$application$/ and find nothing.
 *     After the run, open Grafana → select "airline-api" in the application dropdown.
 *
 *   Fix 2 — "Invalid interval string" error triangles on VUs / Errors/s / Checks/s
 *            / http_req_duration panels:
 *     Root cause: Grafana auto-computes $__interval as a decimal (e.g. "0.6s") for
 *     short time ranges. InfluxDB 1.x only accepts integer + unit (e.g. "600ms").
 *     Fix: Edit each affected panel → raw InfluxQL mode → replace
 *       GROUP BY time($__interval)
 *     with
 *       GROUP BY time(${__interval_ms}ms)
 *     Save the dashboard. Repeat for all 5 time-series panels.
 *
 * ── Scenarios (weighted chaos dispatch) ──────────────────────────────────────
 *   25%  Concurrency Bomb      POST /tickets/purchase   — RowVersion annihilator
 *   15%  Stale Scan            GET  /flights/search      — ghost routes + wide windows
 *   15%  Thundering Herd       POST /checkin             — same PNR from all VUs
 *   10%  Auth Flood            POST /auth/register+login — double BCrypt per iteration
 *   10%  Deep Pagination       GET  /passengers + search — high OFFSET queries
 *   15%  Inventory Cliff       POST /tickets/purchase   — race to AvailableCapacity=0
 *   10%  CSV Bomb              POST /flights/upload      — 25-row multipart payloads
 *
 * ── Load Profile (3m30s total) ───────────────────────────────────────────────
 *   0s   ──▶ 45s  : ramp-up   0 → 50 VUs
 *   45s  ──▶ 2m45s: peak      50 → 100 VUs  (chaos zone)
 *   2m45s──▶ 3m30s: ramp-down 100 → 0 VUs
 */

import http  from 'k6/http';
import { check, sleep } from 'k6';

// ── Configuration ──────────────────────────────────────────────────────────────
const BASE_URL = (__ENV.BASE_URL || 'http://localhost:5000').replace(/\/$/, '');

export const options = {
  stages: [
    { duration: '45s', target: 50  },  // warm-up ramp
    { duration: '2m',  target: 100 },  // sustained peak — chaos zone
    { duration: '45s', target: 0   },  // ramp-down
  ],
  thresholds: {
    // Relaxed to account for intentional chaos (concurrency 500s, auth queuing)
    http_req_failed:   ['rate<0.10'],   // <10% total failure rate
    http_req_duration: ['p(95)<3000'],  // p95 under 3 s at 100 VUs

    // Primary bug detector: surfaces unhandled DbUpdateConcurrencyException → 500.
    // A rate below 0.90 means the TicketService is leaking 500s under load.
    'checks{check:purchase: no server error}': ['rate>0.90'],

    // BCrypt correctness gate — register must not fail even under CPU saturation.
    'checks{check:auth register: 201}': ['rate>0.95'],
  },
};

// ── Helpers ────────────────────────────────────────────────────────────────────
function randomHex(len) {
  const chars = '0123456789abcdef';
  let s = '';
  for (let i = 0; i < len; i++) s += chars[Math.floor(Math.random() * 16)];
  return s;
}

function randomInt(min, max) {
  return Math.floor(Math.random() * (max - min + 1)) + min;
}

function randomItem(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}

function jsonHeaders(token) {
  const h = { 'Content-Type': 'application/json' };
  if (token) h['Authorization'] = `Bearer ${token}`;
  return h;
}

function addDays(isoDate, days) {
  const d = new Date(isoDate);
  d.setDate(d.getDate() + days);
  return d.toISOString().split('T')[0]; // yyyy-MM-dd — required by ParseDate strict format
}

// ── Static data pools ──────────────────────────────────────────────────────────
const FIRST_NAMES = [
  'James', 'Maria', 'Luca', 'Ayse', 'Mehmet',
  'Emma',  'Noah',  'Fatma', 'Ali', 'Sophie',
  'Yusuf', 'Zeynep', 'Oliver', 'Charlotte', 'Cem',
];
const LAST_NAMES = [
  'Smith',  'Yilmaz', 'Rossi',   'Johnson', 'Demir',
  'Brown',  'Wilson', 'Celik',   'Kaya',    'Davis',
  'Arslan', 'Sahin',  'Martinez','Anderson','Ozturk',
];

// Valid seeded routes (flights exist between these pairs).
const SEEDED_ROUTES = [
  ['IST', 'AYT'], ['AYT', 'IST'],
  ['IST', 'ESB'], ['ESB', 'IST'],
  ['IST', 'ADA'], ['ADB', 'IST'],
  ['ESB', 'ADB'], ['AYT', 'ADB'],
];

// Ghost routes — valid airport codes but NO flights seeded between them.
// Forces a full composite-index scan returning zero rows + COUNT(*) = 0.
const GHOST_ROUTES = [
  ['ADA', 'AYT'], ['AYT', 'ADA'],
  ['ADA', 'ADB'], ['ADB', 'ADA'],
  ['ADB', 'AYT'], ['ADA', 'ESB'],
];

function randomPassengerName() {
  return `${randomItem(FIRST_NAMES)} ${randomItem(LAST_NAMES)}`;
}

// ── setup() ────────────────────────────────────────────────────────────────────
// Runs once, seeds all required data, returns shared state for all VUs.
export function setup() {
  const adminEmail    = __ENV.K6_ADMIN_EMAIL;
  const adminPassword = __ENV.K6_ADMIN_PASSWORD;

  if (!adminEmail || !adminPassword) {
    throw new Error(
      '[setup] K6_ADMIN_EMAIL and K6_ADMIN_PASSWORD must be set.\n' +
      'Example: k6 run -e K6_ADMIN_EMAIL=admin@airline.com -e K6_ADMIN_PASSWORD=Password123! ...'
    );
  }

  // ── Step 1: Admin login ────────────────────────────────────────────────────
  const loginRes = http.post(
    `${BASE_URL}/api/v1/auth/login`,
    JSON.stringify({ email: adminEmail, password: adminPassword }),
    { headers: { 'Content-Type': 'application/json' } }
  );
  if (loginRes.status !== 200) {
    throw new Error(
      `[setup] Admin login FAILED (HTTP ${loginRes.status}): ${loginRes.body}`
    );
  }
  const adminToken = JSON.parse(loginRes.body).token;
  console.log('[setup] Admin login OK');

  // ── Step 2: Create airports (idempotent) ───────────────────────────────────
  const airports = [
    { code: 'IST', name: 'Istanbul Airport',          city: 'Istanbul' },
    { code: 'AYT', name: 'Antalya Airport',           city: 'Antalya'  },
    { code: 'ESB', name: 'Ankara Esenboga Airport',   city: 'Ankara'   },
    { code: 'ADB', name: 'Izmir Adnan Menderes Apt',  city: 'Izmir'    },
    { code: 'ADA', name: 'Adana Airport',             city: 'Adana'    },
  ];
  for (const ap of airports) {
    http.post(
      `${BASE_URL}/api/v1/airports`,
      JSON.stringify(ap),
      { headers: jsonHeaders(adminToken) }
    );
  }
  console.log('[setup] Airports seeded');

  // ── Step 3: Create flight pool ─────────────────────────────────────────────
  // TK2003 = PRIMARY CONCURRENCY TARGET (all concurrency bomb VUs hit this row)
  // TK9999 = THUNDERBOLT TARGET (all thundering herd VUs hit this single booking)
  // Remaining flights = date-range diversity for stale scan & deep pagination
  const flightDefs = [
    // Inventory cliff target — tiny capacity (10) to trigger precise sold-out races
    ['TK0001', '2026-06-15T06:00:00', '2026-06-15T07:30:00',  90, 'IST', 'AYT',  10],
    // Concurrency bomb target — high capacity to survive the test
    ['TK2003', '2026-06-15T10:00:00', '2026-06-15T12:00:00', 120, 'IST', 'AYT', 300],
    // Thundering herd check-in target — exactly 1 ticket purchased in step 5
    ['TK9999', '2026-06-15T15:00:00', '2026-06-15T17:00:00', 120, 'IST', 'AYT', 200],
    // Return leg (round-trip search tests)
    ['TK3001', '2026-06-15T17:00:00', '2026-06-15T19:00:00', 120, 'AYT', 'IST', 200],
    // Date-range diversity pool
    ['TK1001', '2026-06-15T08:00:00', '2026-06-15T09:30:00',  90, 'IST', 'ESB', 150],
    ['TK4001', '2026-06-16T09:00:00', '2026-06-16T10:30:00',  90, 'ESB', 'ADB', 100],
    ['TK5001', '2026-06-16T13:00:00', '2026-06-16T14:30:00',  90, 'IST', 'ADA', 120],
    ['TK6001', '2026-06-17T07:00:00', '2026-06-17T09:00:00', 120, 'AYT', 'ADB',  80],
    ['TK7001', '2026-06-17T11:00:00', '2026-06-17T12:30:00',  90, 'IST', 'AYT', 160],
    ['TK8001', '2026-06-18T10:00:00', '2026-06-18T11:30:00',  90, 'ESB', 'IST', 100],
    ['TK9001', '2026-06-18T15:00:00', '2026-06-18T17:00:00', 120, 'ADB', 'IST',  90],
    ['TK1002', '2026-06-19T08:00:00', '2026-06-19T09:30:00',  90, 'IST', 'ESB', 130],
    ['TK2004', '2026-06-19T12:00:00', '2026-06-19T14:00:00', 120, 'IST', 'AYT', 150],
    ['TK3002', '2026-06-20T10:00:00', '2026-06-20T12:00:00', 120, 'AYT', 'IST', 180],
    ['TK4002', '2026-06-20T14:00:00', '2026-06-20T15:30:00',  90, 'ADA', 'IST', 120],
    ['TK5002', '2026-06-20T16:00:00', '2026-06-20T17:30:00',  90, 'ADB', 'ESB', 100],
  ];

  const flightPool = [];
  for (const [fn, dep, arr, dur, orig, dest, cap] of flightDefs) {
    const r = http.post(
      `${BASE_URL}/api/v1/flights`,
      JSON.stringify({
        flightNumber:           fn,
        departureDate:          dep,
        arrivalDate:            arr,
        durationMinutes:        dur,
        originAirportCode:      orig,
        destinationAirportCode: dest,
        totalCapacity:          cap,
      }),
      { headers: jsonHeaders(adminToken) }
    );
    if (r.status === 201 || r.status === 400) {
      flightPool.push({ flightNumber: fn, flightDate: dep });
    }
    const tag = r.status === 201 ? 'created' : (r.status === 400 ? 'already exists' : `ERROR ${r.status}`);
    console.log(`[setup] Flight ${fn}: ${tag}`);
  }

  // ── Step 4: Register + login a customer ───────────────────────────────────
  const custEmail    = `setup_${randomHex(8)}@loadtest.io`;
  const custPassword = 'Customer123!';
  http.post(
    `${BASE_URL}/api/v1/auth/register`,
    JSON.stringify({ email: custEmail, password: custPassword }),
    { headers: { 'Content-Type': 'application/json' } }
  );
  const custLoginRes = http.post(
    `${BASE_URL}/api/v1/auth/login`,
    JSON.stringify({ email: custEmail, password: custPassword }),
    { headers: { 'Content-Type': 'application/json' } }
  );
  const customerToken = JSON.parse(custLoginRes.body).token;
  console.log('[setup] Customer token acquired');

  // ── Step 5: Purchase exactly 1 ticket on TK9999 → thunderbolt PNR ─────────
  // All thundering-herd VUs will hammer this single booking row simultaneously.
  // First VU to call /checkin gets Success + seat 1; every other VU gets Failed
  // ("already checked in"), creating maximum read-lock contention on the same row.
  const thunderName = randomPassengerName();
  let thunderboltPnr = null;
  const thunderRes = http.post(
    `${BASE_URL}/api/v1/tickets/purchase`,
    JSON.stringify({
      flightNumber:   'TK9999',
      flightDate:     '2026-06-15T15:00:00',
      passengerNames: [thunderName],
    }),
    { headers: jsonHeaders(customerToken) }
  );
  if (thunderRes.status === 200) {
    const body = JSON.parse(thunderRes.body);
    if (body.status === 'Confirmed' && body.pnrCode) {
      thunderboltPnr = { pnrCode: body.pnrCode, passengerName: thunderName };
      console.log(`[setup] Thunderbolt PNR acquired: ${body.pnrCode}`);
    }
  }
  if (!thunderboltPnr) {
    console.warn('[setup] WARNING: Thunderbolt PNR purchase failed — thundering herd scenario will be skipped');
  }

  console.log(`[setup] Complete — flights: ${flightPool.length}, thunderboltPnr: ${thunderboltPnr ? thunderboltPnr.pnrCode : 'NONE'}`);
  return { adminToken, customerToken, flightPool, thunderboltPnr };
}

// ── Scenario 1: Concurrency Bomb (25%) ────────────────────────────────────────
// Target: EF Core optimistic concurrency on Flight.RowVersion
//
// 100% of iterations buy from TK2003 — the same single DB row — with 3–6
// passengers each. At 100 VUs × 25% = 25 concurrent writers, multiple
// SaveChangesAsync calls will read the same RowVersion value and then race
// to write. The losers throw DbUpdateConcurrencyException, which the
// ExceptionHandlingMiddleware maps to HTTP 500.
//
// The "purchase: no server error" threshold is the detector:
//   rate < 0.90 → unhandled concurrency exceptions are leaking as 500s.
function scenarioConcurrencyBomb(data) {
  const count = randomInt(3, 6);
  const names = Array.from({ length: count }, randomPassengerName);

  const res = http.post(
    `${BASE_URL}/api/v1/tickets/purchase`,
    JSON.stringify({
      flightNumber:   'TK2003',
      flightDate:     '2026-06-15T10:00:00',
      passengerNames: names,
    }),
    {
      headers: jsonHeaders(data.customerToken),
      tags:    { scenario: 'concurrencyBomb' },
    }
  );

  check(res, {
    'purchase: http 200':        r => r.status === 200,
    'purchase: no server error': r => r.status !== 500,
    'purchase: valid status':    r => {
      if (r.status !== 200) return false;
      try {
        const b = JSON.parse(r.body);
        return b.status === 'Confirmed' || b.status === 'SoldOut';
      } catch (_) { return false; }
    },
  });
}

// ── Scenario 2: Stale Scan (20%) ──────────────────────────────────────────────
// Target: Full composite-index scans, double eager-load queries, COUNT(*) pressure
//
// Two sub-modes (50/50):
//   Ghost route: airport pair with zero seeded flights → forces a full scan
//     returning empty results. Bypasses any ORM-level result caching and still
//     executes the COUNT(*) pagination query. Tests index selectivity under
//     zero-result conditions.
//   Wide window + round-trip: 30–90 day date range on a valid route with
//     IsRoundTrip=true always → fires two sequential eager-load queries
//     (OriginAirport + DestinationAirport JOINs on both outbound and return).
//     NumberOfPeople=1 prevents early capacity pruning.
function scenarioStaleScan() {
  const useGhostRoute = Math.random() < 0.5;

  let qs;
  if (useGhostRoute) {
    const [origin, dest] = randomItem(GHOST_ROUTES);
    const fromDate = addDays('2026-06-15', randomInt(0, 10));
    const toDate   = addDays(fromDate, randomInt(3, 7));
    qs = [
      `OriginCode=${origin}`,
      `DestinationCode=${dest}`,
      `DepartureFrom=${fromDate}`,
      `DepartureTo=${toDate}`,
      `NumberOfPeople=1`,
      `IsRoundTrip=false`,
      `PageNumber=1`,
    ].join('&');
  } else {
    // Wide window round-trip: always fires 2 eager-load queries
    const [origin, dest] = randomItem(SEEDED_ROUTES);
    const windowDays = randomInt(30, 90);
    const fromDate   = '2026-06-15';
    const toDate     = addDays(fromDate, windowDays);
    const page       = randomInt(8, 15); // beyond result count → hits COUNT(*) but returns []
    qs = [
      `OriginCode=${origin}`,
      `DestinationCode=${dest}`,
      `DepartureFrom=${fromDate}`,
      `DepartureTo=${toDate}`,
      `NumberOfPeople=1`,
      `IsRoundTrip=true`,
      `PageNumber=${page}`,
    ].join('&');
  }

  const res = http.get(`${BASE_URL}/api/v1/flights/search?${qs}`, {
    tags: { scenario: 'staleScan' },
  });

  check(res, {
    'search: status 200': r => r.status === 200,
  });
}

// ── Scenario 3: Thundering Herd (20%) ─────────────────────────────────────────
// Target: SELECT-then-UPDATE race on the same Passenger row / MAX(SeatNumber)
//
// All VUs in this scenario hit the SAME single PNR simultaneously.
// CheckInService flow:
//   1. GetByPnrAsync → SELECT Booking + Passengers (read lock)
//   2. IsCheckedIn check (in memory)
//   3. GetNextSeatNumberAsync → SELECT MAX(SeatNumber) (non-atomic read)
//   4. passenger.SeatNumber = max + 1; SaveChangesAsync
//
// First VU to reach step 4 gets Success + SeatNumber=1.
// Every other VU is also past step 2 (IsCheckedIn==false at read time)
// but will collide at step 4. Depending on MySQL isolation level, they may
// assign the same seat or fail with a constraint violation. Both are bugs.
function scenarioThunderingHerd(data) {
  if (!data.thunderboltPnr) return;

  const res = http.post(
    `${BASE_URL}/api/v1/checkin`,
    JSON.stringify({
      pnrCode:       data.thunderboltPnr.pnrCode,
      passengerName: data.thunderboltPnr.passengerName,
    }),
    {
      headers: { 'Content-Type': 'application/json' },
      tags:    { scenario: 'thunderingHerd' },
    }
  );

  check(res, {
    'checkin: status 200':         r => r.status === 200,
    'checkin: valid status field':  r => {
      if (r.status !== 200) return false;
      try {
        const b = JSON.parse(r.body);
        return b.status === 'Success' || b.status === 'Failed';
      } catch (_) { return false; }
    },
  });
}

// ── Scenario 4: Auth Flood (15%) ──────────────────────────────────────────────
// Target: BCrypt CPU saturation + thread-pool starvation
//
// Two sequential BCrypt operations per iteration, no sleep between them:
//   POST /register → BCrypt.HashPassword (~150ms CPU at cost 10)
//   POST /login    → BCrypt.VerifyHashedPassword (~150ms CPU)
// Net: ~300ms CPU-bound work per VU iteration.
// At 100 VUs × 15% = 15 concurrent auth VUs → 45 concurrent BCrypt operations.
// This validates whether BCrypt queuing degrades response times on other endpoints.
function scenarioAuthFlood() {
  const email    = `lt_${randomHex(8)}@loadtest.io`;
  const password = 'LoadTest99!';

  const regRes = http.post(
    `${BASE_URL}/api/v1/auth/register`,
    JSON.stringify({ email, password }),
    {
      headers: { 'Content-Type': 'application/json' },
      tags:    { scenario: 'authFlood' },
    }
  );
  check(regRes, { 'auth register: 201': r => r.status === 201 });

  // No sleep — immediately chain the login to maximise back-to-back BCrypt load.
  const loginRes = http.post(
    `${BASE_URL}/api/v1/auth/login`,
    JSON.stringify({ email, password }),
    {
      headers: { 'Content-Type': 'application/json' },
      tags:    { scenario: 'authFlood' },
    }
  );
  check(loginRes, { 'auth login: 200': r => r.status === 200 });
}

// ── Scenario 5: Deep Pagination (10%) ─────────────────────────────────────────
// Target: MySQL OFFSET performance + large page-number queries
//
// Two requests per iteration (50/50):
//   Passenger manifest at high page numbers → SKIP(pageSize * (page-1)) + TAKE(10)
//     on a JOIN across Passenger + Flight. As passenger count grows during the test,
//     these OFFSET queries get progressively slower.
//   Flight search at page numbers beyond result count → COUNT(*) still executes
//     even when items=[]. Tests whether EF Core skips the data query on empty pages.
function scenarioDeepPagination(data) {
  if (Math.random() < 0.5) {
    // Passenger manifest — admin endpoint
    const page = randomInt(5, 20);
    const res = http.get(
      `${BASE_URL}/api/v1/flights/TK2003/date/2026-06-15/passengers?pageNumber=${page}`,
      {
        headers: jsonHeaders(data.adminToken),
        tags:    { scenario: 'deepPagination' },
      }
    );
    check(res, { 'pagination manifest: 200': r => r.status === 200 });
  } else {
    // Flight search beyond result count
    const [origin, dest] = randomItem(SEEDED_ROUTES);
    const page = randomInt(8, 15);
    const qs = [
      `OriginCode=${origin}`,
      `DestinationCode=${dest}`,
      `DepartureFrom=2026-06-15`,
      `DepartureTo=2026-06-20`,
      `NumberOfPeople=1`,
      `IsRoundTrip=false`,
      `PageNumber=${page}`,
    ].join('&');
    const res = http.get(`${BASE_URL}/api/v1/flights/search?${qs}`, {
      tags: { scenario: 'deepPagination' },
    });
    check(res, { 'pagination search: 200': r => r.status === 200 });
  }
}

// ── Scenario 6: Inventory Cliff (15%) ─────────────────────────────────────────
// Target: The exact AvailableCapacity = 0 transition under maximum write pressure
//
// TK0001 is seeded with totalCapacity = 10. With 100 VUs × 15% = 15 concurrent
// writers, the 10 available seats will be exhausted after roughly 7 iterations.
// At the cliff point, multiple VUs simultaneously read AvailableCapacity = 1,
// all attempt to decrement, and only one can commit — the RowVersion losers must
// receive SoldOut or a handled retry, NEVER HTTP 500.
//
// This is stricter than the Concurrency Bomb (300-capacity). The delta between
// AvailableCapacity and the concurrent writer count is deliberately tiny (10 seats
// vs 15 VUs) to maximise the probability of racing right through zero.
function scenarioInventoryCliff(data) {
  const res = http.post(
    `${BASE_URL}/api/v1/tickets/purchase`,
    JSON.stringify({
      flightNumber:   'TK0001',
      flightDate:     '2026-06-15T06:00:00',
      passengerNames: [randomPassengerName()],
    }),
    {
      headers: jsonHeaders(data.customerToken),
      tags:    { scenario: 'inventoryCliff' },
    }
  );

  check(res, {
    'cliff: http 200':        r => r.status === 200,
    'cliff: no server error': r => r.status !== 500,
    'cliff: valid status':    r => {
      if (r.status !== 200) return false;
      try {
        const b = JSON.parse(r.body);
        return b.status === 'Confirmed' || b.status === 'SoldOut';
      } catch (_) { return false; }
    },
  });
}

// ── Scenario 7: CSV Bomb (10%) ─────────────────────────────────────────────────
// Target: Multipart upload parsing, CsvHelper deserialisation, EF Core bulk insert,
//         and per-row duplicate (FlightNumber, DepartureDate) detection.
//
// Each iteration generates a 25-row CSV:
//   - Flight numbers drawn from a fixed 10-code pool (TB0001–TB0010).
//   - Departure dates drawn randomly from a 30-day window.
//   - ~5 rows are intentional within-payload duplicates (same fn + date picked twice).
//   - ArrivalDate = DepartureDate + 90 min, so the date-math assertion passes.
// The small code × date pool means DB-level conflicts accumulate across VUs over
// time, exercising the duplicate-rejection path without failing the HTTP call.
// Check: only 200 (partial success) or 400 (rejected) is acceptable — no 500.
function scenarioCsvBomb(data) {
  const BOMB_FNS  = ['TB0001','TB0002','TB0003','TB0004','TB0005',
                     'TB0006','TB0007','TB0008','TB0009','TB0010'];
  const AIRPORTS  = ['IST','AYT','ESB','ADB','ADA'];

  const header = 'FlightNumber,DepartureDate,ArrivalDate,DurationMinutes,OriginAirportCode,DestinationAirportCode,TotalCapacity';

  // Build 20 unique rows
  const rows = [];
  for (let i = 0; i < 20; i++) {
    const fn        = BOMB_FNS[i % BOMB_FNS.length];
    const dayOffset = randomInt(0, 29);
    const hour      = randomInt(6, 20);
    const dep       = new Date(Date.UTC(2026, 6, 1 + dayOffset, hour, 0, 0)); // July 2026
    const arr       = new Date(dep.getTime() + 90 * 60 * 1000);
    const depStr    = dep.toISOString().replace('.000Z', '');
    const arrStr    = arr.toISOString().replace('.000Z', '');
    const orig      = AIRPORTS[i % AIRPORTS.length];
    const dest      = AIRPORTS[(i + 2) % AIRPORTS.length];
    rows.push(`${fn},${depStr},${arrStr},90,${orig},${dest},100`);
  }

  // Append 5 duplicates (first 5 rows repeated → collision within the same upload)
  for (let i = 0; i < 5; i++) rows.push(rows[i]);

  // Shuffle so duplicates aren't predictably at the end
  for (let i = rows.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    const t = rows[i]; rows[i] = rows[j]; rows[j] = t;
  }

  const csvContent = [header, ...rows].join('\n');

  const res = http.post(
    `${BASE_URL}/api/v1/flights/upload`,
    { file: http.file(csvContent, 'bomb.csv', 'text/csv') },
    {
      headers: { 'Authorization': `Bearer ${data.adminToken}` },
      tags:    { scenario: 'csvBomb' },
    }
  );

  check(res, {
    'csv: no server error': r => r.status !== 500,
    'csv: 200 or 400':      r => r.status === 200 || r.status === 400,
  });
}

// ── Default function — weighted chaos dispatcher ───────────────────────────────
// [0.00, 0.25) → Concurrency Bomb   (25%)
// [0.25, 0.40) → Stale Scan         (15%)
// [0.40, 0.55) → Thundering Herd    (15%)
// [0.55, 0.65) → Auth Flood         (10%)
// [0.65, 0.75) → Deep Pagination    (10%)
// [0.75, 0.90) → Inventory Cliff    (15%)
// [0.90, 1.00) → CSV Bomb           (10%)
export default function (data) {
  const roll = Math.random();

  if (roll < 0.25) {
    scenarioConcurrencyBomb(data);
  } else if (roll < 0.40) {
    scenarioStaleScan();
  } else if (roll < 0.55) {
    scenarioThunderingHerd(data);
  } else if (roll < 0.65) {
    scenarioAuthFlood();
  } else if (roll < 0.75) {
    scenarioDeepPagination(data);
  } else if (roll < 0.90) {
    scenarioInventoryCliff(data);
  } else {
    scenarioCsvBomb(data);
  }

  // Think time: 500–1000 ms (prevents request bursts within a single VU).
  sleep(0.5 + Math.random() * 0.5);
}
