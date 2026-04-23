# ServiceTest

`ServiceTest` is the integration test project for the SurveyInstrument microservice. It exercises the running API through the generated NSwag client from `ModelSharedOut`.

This project is not a pure unit-test suite. It assumes a live service is available and reachable.

## Responsibilities

- Validate the externally visible HTTP contract of the service.
- Exercise CRUD behavior for both `ErrorSource` and `SurveyInstrument`.
- Confirm common success and failure paths, including:
  - create
  - fetch
  - update
  - delete
  - empty-guid bad requests
  - duplicate create conflicts

## Test Strategy

Tests use:

- a real `HttpClient`
- a generated `Client` from `NORCE.Drilling.SurveyInstrument.ModelShared`
- the running API hosted at a configured local base URL

Because of that, failures can come from:

- the service itself
- stale generated shared client code
- incompatible schema generation
- local environment issues such as ports, certificates, or missing seed data

## Key Files

- `Tests.cs`
  - Contains integration tests for both resource families.
  - Builds test payloads through helper methods:
    - `ConstructErrorSource`
    - `ConstructSurveyInstrument`
- `GlobalUsings.cs`
  - Centralizes shared test usings.

## Runtime Assumptions

The current tests default to:

```text
http://localhost:8080/SurveyInstrument/api/
```

This base URL is hard-coded in `Tests.cs` and should be updated if your local service runs elsewhere.

The suite also disables TLS certificate validation in the handler for convenience. That is acceptable for local integration testing, but it should not be copied into production code.

## Dependencies

- `ModelSharedOut`
  - Provides the generated NSwag `Client` and DTOs.
- `NUnit`
- `Microsoft.NET.Test.Sdk`
- `Microsoft.Extensions.Logging`

## Running Tests

Start the service first, then run:

```powershell
dotnet test .\ServiceTest\ServiceTest.csproj
```

If the service runs on another port, update `host` in `Tests.cs` before running.

## What Is Covered

### `ErrorSource`

- `GET` workflows
- `POST` workflows
- `PUT` workflows
- `DELETE` workflows

### `SurveyInstrument`

- `GET` workflows
- `POST` workflows
- `PUT` workflows
- `DELETE` workflows
- `LightData` retrieval through the generated client

## Known Limitations

- Tests mutate the backing database.
- Cleanup is performed inside each test, but a crashed run can leave residual records.
- The suite does not isolate state with a temporary database.
- The suite does not currently verify usage statistics or Swagger endpoints.

## Recommended Maintenance

- Keep the test base URL aligned with your local launch configuration.
- Regenerate `ModelSharedOut` if the service contract changes.
- Add assertions for new endpoints as the service grows.
- Consider a dedicated ephemeral database strategy if test concurrency becomes important.
