# Ghar Aagan (घर आँगन)

**Mobile-Based Home Service Booking System to Provide Trusted On-Demand Services for Households in Nepal.**

Ghar Aagan connects Nepali households with **verified** local service providers — plumbers, electricians, cleaners, carpenters, painters and appliance technicians — replacing unreliable word-of-mouth and Facebook-group discovery with verification, transparent pricing, and review-based trust.

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core MVC (.NET 8) |
| ORM | Entity Framework Core 8 |
| Database | SQLite (default, zero-setup) — one-line switch to SQL Server |
| Auth | ASP.NET Core Identity (roles: `Customer`, `Provider`, `Admin`) |
| UI | Razor views + Bootstrap 5, custom Nepali-courtyard theme (terracotta / marigold / deep green / cream) |

## Setup (3 commands)

Prerequisite: [.NET SDK 8+](https://dotnet.microsoft.com/download) and the EF tool (`dotnet tool install --global dotnet-ef` if you don't have it).

```bash
dotnet restore
dotnet ef database update
dotnet run
```

Then open the URL shown in the console (default `http://localhost:5063`).

> The app also auto-applies migrations **and seeds demo data on first run**, so even a plain `dotnet run` gives you a fully working demo.

## Seeded Demo Accounts

All passwords: **`Demo@123`**

| Role | Email | Notes |
|---|---|---|
| Admin | `admin@gharaagan.com` | Dashboard, verification queue, all users/bookings |
| Customer | `sita@example.com` | Has completed bookings + reviews |
| Provider | `ram.plumber@example.com` | Plumbing — Kathmandu, verified |
| Provider | `hari.electric@example.com` | Electrical — Kathmandu, verified |
| Provider | `maya.cleaning@example.com` | Cleaning — Lalitpur, verified |
| Provider | `krishna.wood@example.com` | Carpentry — Bhaktapur, verified |
| Provider | `gopal.paint@example.com` | Painting — Kathmandu, verified |

Seed also includes 6 service categories and completed bookings with 4–5★ reviews so ratings and recommendations are demo-ready immediately.

## Features

**Customer** — register/login, browse providers by category + location, view profiles (rating, reviews, experience, rate), book (date/time, address, notes, live price estimate), view/cancel bookings, review completed jobs.

**Provider** — register/login, create/edit profile, set weekly availability, accept/reject/start/complete bookings, dashboard with job history + earnings summary.

**Admin** — stats dashboard, approve/reject provider verification, view all bookings and users.

## Recommendation Logic (viva note)

`Services/RecommendationService.cs` implements `IRecommendationService` — the rule-based stand-in for the proposal's "AI recommendation":

```
Score = (AverageRating / 5) × 0.60   // quality signal from reviews
      +  LocationMatch     × 0.25    // token match between customer & provider area
      +  VerifiedBonus     × 0.15    // admin-verified trust signal
```

All three components are normalized to 0–1 before weighting; weights are constants at the top of the class and fully commented for demo explanation.

## Project Structure

```
Controllers/   Account, Home, Providers (public), Bookings (customer),
               Provider (provider dashboard), Admin
Data/          ApplicationDbContext, SeedData
Models/        Entities + ViewModels/
Services/      IRecommendationService, RecommendationService
Views/         Razor views per controller + shared layout
wwwroot/css/   site.css (custom theme)
Migrations/    EF Core InitialCreate
```

## Switching to SQL Server (one line)

In `Program.cs` replace:

```csharp
options.UseSqlite(connectionString)
```

with:

```csharp
options.UseSqlServer(connectionString)
```

add the `Microsoft.EntityFrameworkCore.SqlServer` package, update `DefaultConnection` in `appsettings.json`, and re-create migrations (`dotnet ef migrations add InitialSqlServer`). `ApplicationDbContext` is provider-agnostic — no other changes.

## Deployment

**Publish:**

```bash
dotnet publish -c Release -o publish
```

- **Azure App Service**: deploy the `publish/` folder (zip deploy or `az webapp up`). SQLite file lives in the app directory — enable *Always On* and note the file resets on redeploy unless placed on persistent storage (`%HOME%\site\data`); for production move to Azure SQL via the one-line switch above.
- **IIS**: install the [.NET 8 Hosting Bundle](https://dotnet.microsoft.com/permalink/dotnetcore-current-windows-runtime-bundle-installer), point a site at `publish/`, app pool = *No Managed Code*. Grant the app pool identity write permission on the folder so SQLite can write.

## Validation & Security

- Data annotations: required fields, Nepali phone format (`^9\d{9}$`), rating 1–5, rate ranges.
- Role-based authorization: `[Authorize(Roles = "...")]` on Bookings (Customer), Provider, and Admin controllers.
- Anti-forgery tokens on all POST forms; ownership checks on every booking mutation.
