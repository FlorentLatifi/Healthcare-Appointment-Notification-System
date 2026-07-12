# Unit test helpers

## EF Core identity testing (`EfCoreSqliteFixture`)

### The problem Moq does not catch

SQL Server (and SQLite) assign integer primary keys on **INSERT / SaveChanges**.  
If application code does:

```csharp
await repo.AddAsync(patient);
// patient.Id is still 0 here — identity not assigned yet
user.LinkToPatient(patient.Id);
await unitOfWork.SaveChangesAsync();
```

…`User.PatientId` (or `DoctorId`) is persisted as **0**. JWT claims and ownership checks then break.

**Moq never generates IDs**, so this path often stays green in pure unit tests. That is how the profile-link regression shipped.

### When to use real EF identity tests

| Prefer **Moq** | Prefer **`EfCoreSqliteFixture`** |
|----------------|----------------------------------|
| Validation / early returns | INSERT then read `entity.Id` for another update |
| Pure domain rules | Scalar FKs / link fields (`User.PatientId`, etc.) |
| Pipeline behaviors with fakes | Unique indexes, real transactions, multi-context re-read |

Rule of thumb: if correctness depends on **database-generated values** or **order of SaveChanges**, add an EF fixture test **in addition to** Moq tests.

### Pattern

```csharp
[Trait("Category", "Integration")]
public sealed class MyHandlerIdentityTests
{
    [Fact]
    public async Task Handler_PropagatesGeneratedIdentity()
    {
        await using var db = await EfCoreSqliteFixture.CreateAsync();
        await using var ctx = db.CreateContext();
        await ctx.Database.EnsureCreatedAsync();

        var userId = await EfCoreIdentityAssertions.SeedUserAsync(
            ctx, "user1", "u1@test.com", UserRole.Patient);

        var uow = db.CreateUnitOfWork(ctx);
        var handler = new MyHandler(uow);

        var result = await handler.HandleAsync(/* command with RequestingUserId = userId */);

        result.IsSuccess.Should().BeTrue(result.Error);
        // Always re-read on a *new* context — do not assert only tracker state.
        await EfCoreIdentityAssertions.AssertUserPatientLinkAsync(db, userId, result.Value);
    }
}
```

### Building blocks

| Type | Role |
|------|------|
| `SqliteCompatibleDbContext` | `HealthcareDbContext` with SQLite-friendly RowVersion / TEXT |
| `EfCoreSqliteFixture` | Shared-memory SQLite, `CreateContext()`, `CreateUnitOfWork()` |
| `EfCoreIdentityAssertions` | Seed user + assert Patient/Doctor link after identity flush |

### Related examples

- `Application/Commands/CreateProfileLinkIdentityRegressionTests.cs` — CreatePatient / CreateDoctor link order  
- `Adapters/Persistence/EntityFramework/EFCorePaymentConcurrencyTests.cs` — concurrency with real SQLite  
- Integration project `SqlServerTestFixture` / Testcontainers — full stack (slower; use when HTTP + SQL Server matter)

### Checklist for new persistence handlers

1. [ ] Moq tests for validation and happy path orchestration  
2. [ ] If Id is read before a second write: **EfCoreSqliteFixture** test that reloads from DB  
3. [ ] Assert linked field `≠ 0` and equals the related entity’s real Id  
4. [ ] Use a **fresh** context for verification (change tracker can hide bugs)
