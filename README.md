# amarimusic

## Admin credentials

`AdminAuth:Username` / `AdminAuth:PasswordHash` must be set outside the Development
environment — the app throws at startup otherwise. To configure them:

1. Copy `src/AmariMusic.Web/appsettings.Production.json.template` to
   `appsettings.Production.json` next to the deployed app (this file is
   git-ignored and never committed).
2. Generate a password hash:
   ```bash
   dotnet run --project src/AmariMusic.Web -- hash-password <password>
   ```
3. Paste the printed value into `AdminAuth:PasswordHash`, and set `AdminAuth:Username`.

For local development, use `dotnet user-secrets` instead of committing real
credentials to `appsettings.Development.json`.

## Tests

```bash
dotnet test tests/AmariMusic.Tests
```
