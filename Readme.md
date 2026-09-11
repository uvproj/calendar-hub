# Calendar Hub

Calendar Hub provides a command-line calendar client, a shared calendar library, and an authenticated HTTP API for a future tablet interface. It supports local JSON-backed calendars and Google Calendar.

## Architecture

- `Calendar.Core` owns event models, provider integrations, configured-service management, multi-calendar queries, and confirmed batch creation.
- `calendar-cli` provides terminal commands over `Calendar.Core`.
- `Calendar.Api` is an ASP.NET Core BFF with local Identity accounts, SQLite persistence, cookie authentication, antiforgery protection, and rate limiting.
- `Calendar.Core.Tests`, `calendar-cli.Tests`, and `Calendar.Api.Tests` cover the corresponding layers without live Google requests.

The API calls `Calendar.Core` directly. It does not launch or parse the CLI executable.

## Capabilities

- Creating events with a name, date/time, description, location, and invitees
- Creating timed, duration-based, and all-day events
- Listing events for the upcoming window or for a specific month
- Deleting events by identifier
- Managing named calendar services, including setting a default service
- Aggregating multiple configured calendars while preserving per-calendar failures
- Creating a confirmed event batch in one explicitly selected calendar
- Previewing deterministic or OpenAI-compatible interpretations of free-form multi-event text
- Managing local family accounts through administrator-only API endpoints
- Falling back to a local filesystem calendar when no service is configured

## CLI usage

For the full command reference, switches, and examples, see:

- [calendar-cli/CLI_USAGE.md](calendar-cli/CLI_USAGE.md)

## API configuration

`Calendar.Api` uses these configuration keys. Supply sensitive values through environment variables, .NET user secrets, or another external configuration provider. Do not place secrets in `appsettings.json`.

| Key | Environment variable | Purpose |
| --- | --- | --- |
| `Bootstrap:Secret` | `Bootstrap__Secret` | One-time secret required to create the first administrator. |
| `CalendarHub:DataDirectory` | `CalendarHub__DataDirectory` | Directory containing the Identity SQLite database. Defaults to `%LOCALAPPDATA%\calendar-hub`. |
| `CalendarHub:DatabasePath` | `CalendarHub__DatabasePath` | Optional explicit SQLite database path. |
| `CalendarHub:ServicesPath` | `CalendarHub__ServicesPath` | Optional service-registry path. When omitted, the API shares `%LOCALAPPDATA%\calendar-cli\services.json` with the CLI. |
| `CalendarHub:TimeZoneId` | `CalendarHub__TimeZoneId` | Time zone used by the host. Defaults to `UTC`. |
| `CalendarAi:Provider` | `CalendarAi__Provider` | `Fake` or `OpenAI`. Development defaults to `Fake`; other environments require an explicit value. |
| `CalendarAi:BaseUrl` | `CalendarAi__BaseUrl` | Absolute OpenAI-compatible endpoint. Defaults to the public OpenAI API URL placeholder. |
| `CalendarAi:Model` | `CalendarAi__Model` | Model or deployment name used by the server. |
| `CalendarAi:ApiKey` | `CalendarAi__ApiKey` | Server-side API key. Set only through environment variables, user secrets, or another external provider. |

For local development, send the exact text `sample: dinner-and-picnic` with an explicit destination calendar to `POST /api/calendar-interpretations`. The deterministic fake returns one timed dinner and one all-day picnic relative to the configured current date. Other fake inputs return a clarification error and never contact a model.

To use an OpenAI-compatible provider, set all required values outside source control:

```powershell
$env:CalendarAi__Provider = "OpenAI"
$env:CalendarAi__BaseUrl = "https://api.openai.com/v1"
$env:CalendarAi__Model = "your-model-name"
$env:CalendarAi__ApiKey = "your-server-side-key"
```

Interpretation is preview-only. It returns editable structured drafts, warnings, and clarification errors; it never creates, updates, or deletes calendar events. A client must submit reviewed drafts separately to `POST /api/events/batch` for confirmation. Provider configuration failures and invalid model output are returned as safe typed errors without prompts, raw responses, keys, or exception details.

The local database is created on startup with `EnsureCreated`. This is suitable for the current single-host MVP; introduce EF Core migrations before evolving the production schema.

## First administrator

1. Set a bootstrap secret outside source control:

	```powershell
	$env:Bootstrap__Secret = "use-a-long-random-value"
	```

2. Start the API and request `GET /api/auth/antiforgery`.
3. Send its token in the `X-CSRF-TOKEN` header with `POST /api/auth/bootstrap` and the matching bootstrap secret, username, display name, and passcode.
4. Bootstrap becomes unavailable after the first account exists. Administrators create additional family members through `/api/admin/members`.

Passcodes must contain 8 to 128 characters. Failed sign-in attempts are locked out, and authentication endpoints are rate limited.

## Run and test

```powershell
dotnet restore .\calendar-hub.slnx
dotnet test .\calendar-hub.slnx
dotnet run --project .\Calendar.Api\Calendar.Api.csproj
```

The development URLs are printed by ASP.NET Core when the host starts. OpenAPI is available at `/openapi/v1.json` to authenticated users.

### Browser development

The tablet client requires Node.js and npm. Run the API and Vite in separate terminals:

```powershell
$env:Bootstrap__Secret = "use-a-long-random-value"
dotnet run --project .\Calendar.Api\Calendar.Api.csproj

Set-Location .\calendar-web
npm ci
npm run dev
```

Vite prints the browser URL and proxies `/api`, `/health`, and `/openapi` to the local ASP.NET Core host. The browser uses same-origin cookie authentication through that proxy.

Frontend quality gates:

```powershell
Set-Location .\calendar-web
npm test
npm run lint
npm run build
```

The Vite production build is written to `Calendar.Api\wwwroot`. `dotnet publish` runs `npm ci` and `npm run build` automatically before collecting publish files. Set `SkipCalendarWebBuild=true` only when a trusted pipeline has already produced the assets.

All state-changing requests require the antiforgery cookie and the matching `X-CSRF-TOKEN` header. Except for health checks and the account bootstrap/login flow, API endpoints require an authenticated cookie session.

## LAN security

Use HTTPS before exposing the host to a tablet or another machine. For a home LAN, use a certificate trusted by the tablet or place the application behind a local HTTPS reverse proxy. Do not send passcodes or authenticated cookies over plain HTTP. Restrict firewall access to the trusted LAN.

## Data locations

- Identity database: `%LOCALAPPDATA%\calendar-hub\calendar-hub.db` by default
- Calendar service registry: `%LOCALAPPDATA%\calendar-cli\services.json` by default
- Default filesystem events: `%LOCALAPPDATA%\calendar-cli\events.json`
- Google OAuth tokens: `%LOCALAPPDATA%\calendar-cli\google-tokens\<service-identity>`

## Projects

- `Calendar.Core/`
- `Calendar.Api/`
- `calendar-cli/`
