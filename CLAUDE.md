# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Run the app (from repo root)
dotnet run --project src/AmariMusic.Web

# Build
dotnet build src/AmariMusic.Web

# Watch (hot reload)
dotnet watch --project src/AmariMusic.Web

# Run tests
dotnet test tests/AmariMusic.Tests

# Generate an AdminAuth:PasswordHash value to paste into appsettings.Production.json
dotnet run --project src/AmariMusic.Web -- hash-password <password>
```

## Architecture

**Blazor Web App (.NET 10) with Static SSR** — server-renders HTML on every request for full SEO. No client-side interactivity (`@rendermode` is not set anywhere; pages are static).

```
src/AmariMusic.Web/
  Components/
    App.razor              # HTML shell — <head> meta, Bootstrap CDN, Bootstrap Icons CDN
    Routes.razor           # Router with NotFound fallback
    Layout/
      MainLayout.razor     # Top nav + <main> + footer wrapper
      NavMenu.razor        # Bootstrap navbar (horizontal, collapses on mobile)
    Pages/
      Home.razor           # / — hero, services, therapy, album, performers, TV, CTA
      Bio.razor            # /bio — career narrative + stats cards
      Calendar.razor       # /calendar — Google Calendar embed
      NotFound.razor       # 404
      Error.razor          # Unhandled exception display
  Services/
    EmailService.cs        # SMTP notification email for new contact submissions
    PasswordHasher.cs      # PBKDF2 hash/verify for AdminAuth:PasswordHash
    AdminAuthValidator.cs  # Startup guard — throws outside Development if AdminAuth is unset
  wwwroot/
    app.css                # All site styles (no external CSS beyond Bootstrap + Bootstrap Icons)
    images/                # Copied from original trich.new site (Turley Richards photos/icons)
    lib/bootstrap/         # Bootstrap 5 local copy (used for CSS; JS loaded from lib too)
tests/
  AmariMusic.Tests/         # xUnit tests for Services/ (PasswordHasher, AdminAuthValidator)
```

## Key design decisions

- **SSR over WASM**: the original site was Blazor WASM (bad for SEO). This app uses Static SSR so crawlers receive full HTML.
- **Bootstrap Icons** loaded via CDN in `App.razor`; icon classes like `bi bi-telephone` are used throughout.
- **`@@` escaping**: Razor treats `@` as a C# expression delimiter. URLs containing `@` (e.g. YouTube `@handle`) must be written as `@@` in `.razor` files.
- **Scoped CSS**: layout components use `.razor.css` co-located files; page-level styles live in `app.css` organized by section.
- **Images**: all static assets in `wwwroot/images/` — referenced as `/images/filename.ext` (absolute paths, no `~`).
- **AdminAuth startup guard**: `AdminAuthValidator` throws at startup outside the Development environment if `AdminAuth:Username`/`AdminAuth:PasswordHash` aren't set, so the app fails fast instead of booting with an unprotected `/admin` login. `appsettings.Production.json` (git-ignored) is the source of these values on the server — see the README for how to generate a password hash.

## Subject matter

This is the website for **Turley Richards** — vocalist, producer, vocal coach, and songwriter based in Louisville, KY. Contact: `turley@turleyrichards.com` / `(502) 452-9011`.
