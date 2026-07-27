# amarimusic

## Admin credentials

`AdminAuth:Username` / `AdminAuth:PasswordHash` must be set outside the Development
environment — the app throws at startup otherwise. To configure them:

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

## App:BaseUrl

Set `App:BaseUrl` (e.g. `https://turleyrichards.com`) in `appsettings.Production.json`
so admin-notification emails link to an absolute `/admin/contacts/{id}` URL —
without it, the link in the email is relative and won't resolve in an email
client.

## Turnstile CAPTCHA

The contact form and admin login both use [Cloudflare Turnstile](https://developers.cloudflare.com/turnstile/)
as a bot-protection layer. Both pages fall back to no CAPTCHA (widget hidden,
verification skipped) when `Turnstile:SecretKey` is unset — this keeps local
development friction-free, but means Turnstile must be explicitly configured
in production.

1. Create a Turnstile widget in the Cloudflare dashboard for the site's domain.
2. Set `Turnstile:SiteKey` and `Turnstile:SecretKey` in `appsettings.Production.json`.

## Tests

```bash
dotnet test tests/AmariMusic.Tests
```
