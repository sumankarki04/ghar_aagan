# Ghar Aagan — Home Services Marketplace

A full-stack home services marketplace (final year project) built with **ASP.NET Core 8 Web API**,
**Entity Framework Core + SQLite**, **JWT authentication**, and a **vanilla HTML/CSS/JS** frontend.

Customers find and book home services (plumbing, electrical, cleaning, etc.), providers list
services and manage job requests, and an admin oversees the platform.

## Tech stack

| Layer     | Technology                                  |
|-----------|---------------------------------------------|
| Backend   | ASP.NET Core 8 Web API (controllers)        |
| Database  | SQLite via EF Core (code-first migrations)  |
| Auth      | JWT bearer tokens, role-based authorization |
| Frontend  | Static HTML + CSS + JavaScript (`wwwroot`)  |
| Docs      | Swagger / OpenAPI                           |

## Roles

- **Customer** — browse/search services, book, pay, review.
- **Provider** — create listings, accept/reject/complete bookings.
- **Admin** — manage categories, view dashboard stats. (Seeded, not self-registered.)

## How to run

### Visual Studio
1. Open `GharAagan.slnx`.
2. Press **F5** (or Ctrl+F5). The database is created and seeded automatically on first run.
3. Browser opens at the site root — the frontend — and `/swagger` shows the API.

### Command line
```bash
cd "E:/FINAL/GHARAAGAN"
dotnet run
```
Then open the URL shown in the console (e.g. `http://localhost:5049`).

## Default admin account
- Email: `admin@gharaagan.com`
- Password: `Admin@123`

(Configurable under `Seed` in `appsettings.json`. **Change these for any real deployment.**)

## Project structure
```
Controllers/   API endpoints (Auth, Categories, Services, Bookings, Reviews, Admin)
Models/        EF Core entities + enums
Dtos/          Request/response shapes
Data/          AppDbContext + DbSeeder
Services/      JWT TokenService, PasswordHasher (PBKDF2), claim helpers
Migrations/    EF Core migrations
wwwroot/       Frontend (index.html, css/, js/)
```

## Booking lifecycle
`Pending` → provider `Accept`/`Reject` → `Accepted` → provider `Complete` → `Completed`.
Customer may `Cancel` before completion. A `Pending` payment is created with each booking;
the customer pays via a **mock gateway** (`/bookings/{id}/pay`). Reviews are allowed only on
`Completed` bookings, one per booking.

## Notes & next steps
- **Payments are simulated.** Real eSewa/Khalti/Stripe integration would verify a gateway
  callback/signature before marking a payment `Paid`.
- The JWT signing key in `appsettings.json` is a placeholder — move it to user-secrets or
  environment variables before deploying.
- A Dockerfile-based deploy and a richer provider profile page are natural follow-ups.
