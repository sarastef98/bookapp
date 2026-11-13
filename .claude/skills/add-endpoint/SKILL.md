---
name: add-endpoint
description: Use when adding a new entity or API endpoint to BookFlow (controller, service, DTOs, tests). Follows the layering used by the Resource feature.
---

# Add an endpoint to BookFlow

Reference implementation: the Resource feature. Mirror its structure.

## Steps
1. Domain: add the entity in `Domain/` and a `DbSet` in `BookFlowDbContext`.
2. Contracts: add `XDto` and `CreateXRequest` records in `Contracts/` with data-annotation validation.
3. Service: add `IXService` + `XService` in `Services/`. All logic here; async with `CancellationToken`.
4. Register the service in `Program.cs`.
5. Controller: add `XController`, thin, returns DTOs and `ProblemDetails` on errors.
6. Tests: add `XServiceTests` covering happy path, not-found, and validation failures.
7. If the model changed, add a NEW migration (never edit existing ones):
   `dotnet ef migrations add <Name> --project src/BookFlow.Api`
8. Run `dotnet build` and `dotnet test`. Fix failures before finishing.

## Checklist before done
- No EF entities returned from controllers
- No business logic in controllers
- Times are `DateTimeOffset` UTC