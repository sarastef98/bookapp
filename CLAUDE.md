# BookFlow

ASP.NET Core Web API for managing resource bookings (rooms, tables, etc.).

## Commands
- Build: `dotnet build`
- Test: `dotnet test`
- Format: `dotnet format`
- Run: `dotnet run --project src/BookFlow.Api`

## Architecture
- Controllers are thin: validate input, call a service, map to a DTO. No business logic.
- Business rules live in `Services/`. Data access via EF Core `DbContext` injected into services.
- Never return EF entities from controllers; use DTOs (records) in `Contracts/`.
- Folder layout: `Controllers/`, `Services/`, `Domain/`, `Contracts/`, `Data/`.

## Conventions
- Async all the way: methods return `Task`/`Task<T>`, suffix `Async`, accept `CancellationToken`.
- Use `DateTimeOffset` (UTC) for all times.
- Errors: return `ProblemDetails` via `Results`/`ControllerBase.Problem`; no raw exceptions to clients.
- Naming: PascalCase types/methods, `_camelCase` private fields, interfaces start with `I`.

## Business rules
- A resource cannot have overlapping Confirmed bookings.
- Booking End must be after Start. Past-dated bookings are rejected.

## Testing
- xUnit. Every service method gets tests; overlap logic needs edge cases
  (touching ranges, fully contained, identical, cancelled bookings ignored).

## Do not
- Edit existing EF migrations. Add a new one instead.
- Commit secrets or edit `appsettings.Production.json`.
- Add NuGet packages without asking.