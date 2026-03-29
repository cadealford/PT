# PetroTransit API Integration Tests

These tests are designed to run from the console and exercise the core website functionality exposed by the API.

## Run

From the `PetroTransit` folder:

```bash
dotnet test PetroTransit.API.Tests/PetroTransit.API.Tests.csproj
```

Or run the whole solution:

```bash
dotnet test PetroTransit.sln
```

## Coverage in this suite

- Auth flows:
  - register/login
  - forgot username
  - forgot password
  - reset password
  - test-email endpoint
  - admin-only users endpoint
  - promote user
  - delete user
- Airplanes:
  - admin CRUD
  - non-admin write forbidden
- Personnel:
  - admin CRUD
  - non-admin write forbidden
- Reservations:
  - create
  - non-owner update forbidden
  - owner update allowed

## Notes

- Tests use an isolated in-memory SQLite database via a custom `WebApplicationFactory`.
- Test environment sets ASP.NET environment to `Testing` so startup migration/seed logic is skipped.
