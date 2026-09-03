# ServiceTest

`ServiceTest` is the integration test project for the SurveyInstrument microservice. It exercises the running API through the generated NSwag client from `ModelSharedOut`.

This project is not a pure unit-test suite. It assumes a live service is available and reachable.

## Responsibilities

- Validate the externally visible HTTP contract of the service.
- Exercise CRUD behavior for both `ErrorSource` and `SurveyInstrument`.
- Exercise identity/feature catalogs, batch portability, persistence safety, and the MCP contract.
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
- a generated `Client` from `OSDC.Drilling.SurveyInstrument.ModelShared`
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
- `SqlConnectionManagerSafetyTests.cs`
  - Uses temporary SQLite files to prove fresh transactional creation, idempotent legacy adoption, row preservation, and fail-closed handling of unknown or newer schemas.

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

The database-safety and MCP registration tests are self-contained. The CRUD tests in `Tests.cs` are live integration tests and require the service to be running first:

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
- all/selected batch export and atomic restore behavior

### Persistence and contract safety

- fresh database creation and legacy schema adoption
- fail-closed behavior for malformed, unknown, incomplete, or newer schemas
- preservation of existing rows during the schema-version migration
- MCP registration, JSON schemas, annotations, concurrency guards, and HTTP envelopes

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

## MCP coverage

- `McpToolRegistrationTests.cs` verifies all 27 REST-backed tools, the five MCP-only guarded mutation and read-only integrity tools, and `ping`, including strict input/output schemas, enforcing model discrimination, the complete error-code enum, versioned backup contracts, granular snapshot mutation, snapshot drift, catalog-reference diagnostics, template-update impact warnings, timestamp- and content-token concurrency, titles, safety annotations, and pre-invocation rejection of unknown arguments.
- `McpServerHttpTests.cs` exercises initialization, tool discovery, structured and fallback success content, schema/model-family rejection, batch round trips, stale timestamp and error-source content-token writes, snapshot warnings and mutation, catalog-reference diagnostics, and stable MCP error envelopes against a running service.

The live HTTP tests require the SurveyInstrument service at the configured test base URL.
