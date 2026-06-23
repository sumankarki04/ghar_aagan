# Ghar Aagan — Design Document

## Overview

### Goal
A home-services marketplace connecting customers with local service providers
(plumbing, electrical, cleaning, etc.). Customers search and book services,
providers manage listings and jobs, and an admin moderates the platform.

### Non-goals
- Real money movement — payment is a mock gateway (no eSewa/Khalti/Stripe integration).
- Real-time chat, notifications, or geolocation.
- Provider identity/KYC verification beyond an admin "verified" flag.
- Horizontal scale — SQLite is intentional for a single-instance project.

## Architecture

### Overall
```
Browser (wwwroot: HTML + CSS + vanilla JS)
        │  fetch  /api/...   (JWT bearer in Authorization header)
        ▼
ASP.NET Core 8 Web API
  Controllers ──► Services (TokenService, PasswordHasher)
        │
        ▼
  AppDbContext (EF Core) ──► SQLite (gharaagan.db)
```
The frontend is served as static files by the same app (`UseStaticFiles`), so
UI and API share one origin.

### Core components
- **Controllers** — `Auth`, `Categories`, `Services`, `Bookings`, `Reviews`, `Admin`.
  HTTP surface; enforce role-based authorization and business rules.
- **Models** — EF Core entities (`User`, `ServiceCategory`, `ServiceListing`,
  `Booking`, `Review`, `Payment`) + enums. Map to SQLite tables via migrations.
- **Dtos** — request/response shapes, decoupling the API contract from entities.
- **Data** — `AppDbContext` (mappings, concurrency-token stamping) + `DbSeeder`
  (default categories + admin on startup).
- **Services** — `TokenService` (JWT issue), `PasswordHasher` (PBKDF2),
  `ClaimsExtensions` (read user id/role from claims).
- **Frontend** — `api.js` (fetch layer: typed errors, timeout, cancellation,
  GET retry+dedup) and `app.js` (role-driven SPA views).

### Booking lifecycle
```
Pending ──provider accept──► Accepted ──provider complete──► Completed
   │                            │
   └── provider reject ─► Rejected     customer pay (Accepted/Completed only) ─► Paid
   └── customer cancel ─► Cancelled
Review allowed only when Completed AND Paid (one per booking).
```

## Design decisions

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-06-23 | SQLite + EF Core | Zero-setup, file DB, ideal for a first/student project; code-first migrations make it portable to SQL Server later. |
| 2026-06-23 | JWT bearer auth | Stateless, simple role claims; no server session store needed. |
| 2026-06-23 | PBKDF2 password hashing | Built into .NET, no extra dependency; per-user salt + fixed-time compare. |
| 2026-06-23 | Mock payment gateway | Real gateways need keys/callbacks out of scope; mock marks Paid after a simulated success. |
| 2026-06-23 | Guid concurrency token | SQLite lacks native rowversion; a Guid re-stamped on save gives optimistic concurrency on Booking/Payment, preventing double-payment. |
| 2026-06-23 | In-query rating aggregates | `AVG`/`COUNT` as correlated subqueries instead of loading every review row into memory. |

### Tech stack
- **Backend**: ASP.NET Core 8 (C#), EF Core 8, SQLite.
- **Frontend**: vanilla HTML/CSS/JS (no framework) — small surface, no build step.
- **Auth**: JWT bearer + role-based `[Authorize]`.

## Trade-offs

### Known limitations
- **SQLite single-writer**: concurrent writes serialize; fine for a demo, not high concurrency.
- **No decimal SUM in SQLite**: revenue is summed in memory (`AdminController`).
- **Provider "verified" is admin-judgment only** — no document/KYC checks behind it.
- **Token cannot be revoked** before its 12h expiry (no refresh/blocklist).

### Technical debt
- Mock payment — replace with a real gateway + callback signature verification for production.
- Booking UI uses `prompt()` dialogs — functional but should become proper modals.

## Security

### Threat model
- **Token forgery** → mitigated: JWT signing key is a secret, kept in user-secrets/env, never in source; startup fails if key is missing/weak (<32 chars).
- **Privilege escalation** → role checks on every sensitive endpoint; admin cannot be self-registered; only providers can be verified.
- **IDOR** → bookings/listings verify ownership before mutate; cross-user access returns 403.
- **XSS** → all dynamic frontend content escaped (`esc()`); no raw `innerHTML` of user data.
- **Race / double-payment** → optimistic concurrency token → `DbUpdateConcurrencyException` → 409.
- **Injection** → EF Core parameterizes all queries; no string-built SQL.

### Implemented measures
- PBKDF2 (100k iterations, SHA-256) password hashing with per-user salt.
- Restricted CORS to configured origins (not `AllowAnyOrigin`).
- Secrets in user-secrets; `.gitignore` excludes DB and secrets.
- Input validation via DataAnnotations + `[ApiController]` auto-400; role validated with `Enum.IsDefined`.
- Public listing reads hide inactive listings; payment gated to accepted bookings; review gated to paid+completed.

## Change history

### 2026-06-23 — Initial version + hardening
Built the full marketplace (auth, catalog/search, booking lifecycle, mock payment,
reviews, admin). Hardened per code review: secrets out of config, CORS restricted,
payment/lifecycle gating, optimistic concurrency, provider verification, case-insensitive
search, in-query rating aggregates, and a frontend API layer with retry/cancellation.
