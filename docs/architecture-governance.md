# Architecture Governance (NetArchTest)

## Purpose

Fail the build when Clean Architecture layering is violated:

| Layer | May depend on |
|-------|----------------|
| **Domain** | Nothing in the solution (no Application / Adapters / Presentation) |
| **Application** | Domain only (+ NuGet packages) |
| **Adapters** | Application + Domain (implements ports) |
| **Presentation** | Application + Adapters **project reference** for composition root only; non-composition types must not use `Healthcare.Adapters.*` |

## Project

```
Healthcare.AppointmentSystem/Healthcare.ArchitectureTests/
  ArchitectureTests.cs
  Healthcare.ArchitectureTests.csproj
```

## Run locally

```bash
cd Healthcare.AppointmentSystem

dotnet test Healthcare.ArchitectureTests/Healthcare.ArchitectureTests.csproj -c Release
```

Or run with the full suite:

```bash
dotnet test Healthcare.AppointmentSystem.sln -c Release --filter FullyQualifiedName~ArchitectureTests
```

## CI integration

Workflow: `.github/workflows/ci.yml`

1. Job **`architecture-tests`** runs first (no need for unit/integration Docker).
2. Job **`unit-tests`** has `needs: architecture-tests`.
3. **`build-and-push` / deploy** still require unit + integration + frontend; a red architecture job blocks unit-tests and therefore the rest of the backend pipeline.

Any NetArchTest failure → non-zero exit → CI red.

## Composition root allowlist

These Presentation types may reference `Healthcare.Adapters` (DI wiring, migrations, hosted seed):

- `Program` (minimal hosting entry)
- `Healthcare.Presentation.API.Configuration.*`
- `Healthcare.Presentation.API.Services.DatabaseSeeder`

Controllers, middleware, and health checks must use **Application ports / options** only.

## Adding a new rule

1. Edit `Healthcare.ArchitectureTests/ArchitectureTests.cs`.
2. Prefer `AssertArch(result, "clear because message")` for NetArchTest results.
3. Run tests locally before push.

## Related packages

- `NetArchTest.Rules` (central version in `Directory.Packages.props`)
- `FluentAssertions` for readable assertion messages
