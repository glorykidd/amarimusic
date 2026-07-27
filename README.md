# Amari Music

## Executive Summary

This repository contains the website for **Turley Richards** — vocalist,
producer, vocal coach, and songwriter based in Louisville, KY, with over 50
years in the music industry. The site presents his biography, services
(vocal coaching, songwriting instruction, studio recording, music therapy,
music business consulting, performance bookings), and an album, alongside a
public contact form and a lightweight admin dashboard for managing incoming
inquiries.

The original site was built as a Blazor WebAssembly app, which hurt SEO
because crawlers received an empty shell rather than rendered content. This
project rebuilds it as a **Blazor Web App with Static Server-Side Rendering
(SSR)**, so every page is fully rendered HTML on first response — no
client-side interactivity, no hydration, just server-rendered pages that
crawlers and users alike see immediately.

## Technology

- **.NET 10** / **Blazor Web App** (Static SSR — no `@rendermode` is set
  anywhere in the codebase; every page is server-rendered per request)
- **Entity Framework Core 10** with **SQLite** for contact-form persistence
- **MailKit** for outbound SMTP notification email
- **Bootstrap 5** (local copy) + **Bootstrap Icons** (CDN) for styling
- **Cloudflare Turnstile** for CAPTCHA/bot protection on the contact form and
  admin login
- **xUnit** for the test suite
- **GitHub Actions** (self-hosted Windows runner) for CI and IIS deployment

## Project Structure

```
src/AmariMusic.Web/
  Components/
    App.razor              # HTML shell — <head> meta, Bootstrap CDN, security headers apply site-wide via middleware
    Routes.razor           # Router with NotFound fallback
    Layout/
      MainLayout.razor     # Top nav + <main> + footer wrapper
      NavMenu.razor        # Bootstrap navbar (horizontal, collapses on mobile)
      AdminLayout.razor    # Admin-section nav (dashboard, inquiries, sign out)
    Pages/
      Home.razor           # / — hero, services, therapy, album, performers, TV, CTA
      Bio.razor             # /bio — career narrative + stats cards
      Calendar.razor        # /calendar — Google Calendar embed
      Contact.razor         # /contact — public contact form (rate-limited + CAPTCHA-protected)
      NotFound.razor        # 404
      Error.razor           # Unhandled exception display
      Admin/
        Login.razor         # /admin/login — CAPTCHA-protected login form
        Dashboard.razor      # /admin, /admin/dashboard — inquiry stats
        Contacts.razor       # /admin/contacts — inquiry list
        ContactDetail.razor  # /admin/contacts/{id} — inquiry detail + reply notes
  Data/
    ContactDbContext.cs      # EF Core DbContext for contact submissions
    Migrations/              # EF Core migrations (SQLite)
  Models/
    ContactSubmission.cs     # Contact-form entity
  Services/
    EmailService.cs          # SMTP notification email for new contact submissions
    PasswordHasher.cs        # PBKDF2 hash/verify for AdminAuth:PasswordHash
    AdminAuthValidator.cs    # Startup guard — throws outside Development if AdminAuth is unset
    TurnstileService.cs      # Cloudflare Turnstile CAPTCHA verification (contact form + admin login)
  wwwroot/
    app.css                  # All site styles (no external CSS beyond Bootstrap + Bootstrap Icons)
    images/                  # Copied from original trich.new site (Turley Richards photos/icons)
    lib/bootstrap/           # Bootstrap 5 local copy (used for CSS; JS loaded from lib too)
  Program.cs                 # Startup, middleware pipeline, admin auth + rate-limiting endpoints
tests/
  AmariMusic.Tests/           # xUnit tests for Services/ (PasswordHasher, AdminAuthValidator, TurnstileService)
.github/workflows/
  amari-develop.yml           # Build + test on push to develop
  amari.yml                   # Build, test, and deploy to IIS on push to main
  codeql.yml                  # CodeQL static analysis
```

See `CLAUDE.md` for a deeper architectural walkthrough and the key design
decisions behind the security-related middleware and services.

## Build

```bash
# Restore + build the app
dotnet build src/AmariMusic.Web

# Run locally (Development environment; hits appsettings.Development.json)
dotnet run --project src/AmariMusic.Web

# Run with hot reload
dotnet watch --project src/AmariMusic.Web
```

## Test

```bash
dotnet test tests/AmariMusic.Tests
```

CI runs the same test suite on every push to `develop` and `main` via GitHub
Actions (`amari-develop.yml` / `amari.yml`) — a failing test blocks deploy.

## Configuration

Local development works out of the box against `appsettings.Development.json`
with no additional setup (Turnstile falls back to Cloudflare's public
always-pass test keys, and the admin login is unguarded in the `Development`
environment).

Everywhere else, the app requires a few settings that are **never committed**
to source control:

### Admin credentials

`AdminAuth:Username` / `AdminAuth:PasswordHash` must be set outside the
Development environment — the app throws at startup otherwise.

1. Copy `src/AmariMusic.Web/appsettings.Production.json.template` to
   `appsettings.Production.json` next to the deployed app (this file is
   git-ignored and never committed).
2. Generate a password hash:
   ```bash
   dotnet run --project src/AmariMusic.Web -- hash-password
   # then enter the password at the prompt
   ```
3. Paste the printed value into `AdminAuth:PasswordHash`, and set `AdminAuth:Username`.

For local development, use `dotnet user-secrets` instead of committing real
credentials to `appsettings.Development.json`.

### App:BaseUrl

Set `App:BaseUrl` (e.g. `https://turleyrichards.com`) in
`appsettings.Production.json` so admin-notification emails link to an
absolute `/admin/contacts/{id}` URL — without it, the link in the email is
relative and won't resolve in an email client.

### Turnstile CAPTCHA

The contact form and admin login both use [Cloudflare Turnstile](https://developers.cloudflare.com/turnstile/)
as a bot-protection layer. Both pages fall back to no CAPTCHA (widget hidden,
verification skipped) when `Turnstile:SecretKey` is unset — this keeps local
development friction-free, but means Turnstile must be explicitly configured
in production.

1. Create a Turnstile widget in the Cloudflare dashboard for the site's domain.
2. Set `Turnstile:SiteKey` and `Turnstile:SecretKey` in `appsettings.Production.json`.

### AllowedHosts

`AllowedHosts` should be restricted to the real production hostname(s) (e.g.
`turleyrichards.com;www.turleyrichards.com`) in `appsettings.Production.json`
— the base `appsettings.json` wildcard is left permissive for local
development only.

## Deploy

Deployment targets a self-hosted Windows IIS server and is fully automated
via GitHub Actions (`.github/workflows/amari.yml`), triggered on push to
`main`:

1. Runs the test suite — a failing test aborts the deploy.
2. Verifies `appsettings.Production.json` exists on the server and that
   `AdminAuth:Username`/`AdminAuth:PasswordHash` are set — aborts if not.
3. Backs up the current deployed site to `C:/backups/`.
4. Stops the `Turleyrichards` IIS app pool, publishes the new build to
   `C:/www-root/amarimusic.com`, and restarts the app pool.
5. Sends an email deploy notification.

For a first-time deploy to a fresh server, see the **Configuration** section
above — `appsettings.Production.json` must exist and have valid `AdminAuth`
credentials before the workflow will proceed.

## Contributing

- Work happens on feature/fix branches off `develop`, merged via pull
  request; `develop` is periodically merged into `main` to ship to
  production.
- Every PR runs through automated CI (build + test) and an AI code-review
  pass (gitStream); address flagged findings before merging.
- Run `dotnet test tests/AmariMusic.Tests` locally before opening a PR — new
  `Services/` code should come with corresponding tests (see existing tests
  for `PasswordHasher`, `AdminAuthValidator`, and `TurnstileService` as
  examples).
- See `CLAUDE.md` for architectural context, key design decisions, and
  conventions (e.g. `@@` escaping for `@`-containing URLs in `.razor`
  files) before making changes.
- Licensed under the MIT License (see `LICENSE`).
