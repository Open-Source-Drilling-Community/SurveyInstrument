# SurveyInstrument Solution

The SurveyInstrument repository contains a complete microservice-based solution for storing, editing, and serving drilling survey instrument definitions together with reusable UI components, generated client contracts, and automated tests.

The solution revolves around two main domain families from the upstream OSDC drilling-surveying libraries:

- `SurveyInstrument`
- `SurveyInstrumentIdentity`
- `SurveyInstrumentFeatureCategory`
- `ErrorSource`

The repository adds the infrastructure around those domain types:

- SQLite-backed persistence
- HTTP API
- generated OpenAPI client/model sharing
- Blazor UI
- integration and smoke tests
- per-endpoint usage statistics

## Solution Structure

The Visual Studio solution currently contains seven projects:

- `Model`
  - Small support-model project.
  - Defines `SurveyInstrumentLight` and `UsageStatisticsSurveyInstrument`.
- `ModelSharedOut`
  - OpenAPI merge/code-generation console app.
  - Produces `SurveyInstrumentMergedModel.cs` and the merged JSON schema bundle.
- `ModelTest`
  - NUnit smoke tests for model construction and assumptions.
- `Service`
  - ASP.NET Core API project.
  - Stores data in SQLite and exposes CRUD endpoints.
- `ServiceTest`
  - NUnit integration tests against a running service.
- `WebPages`
  - Reusable Razor class library with the actual SurveyInstrument pages.
- `WebApp`
  - Blazor Server host application for the UI.

## How The Projects Fit Together

### Domain and support types

The full survey-domain types are not authored directly in this repository. They come from:

- `OSDC.DotnetLibraries.Drilling.Surveying`
- `OSDC.DotnetLibraries.General.DataManagement`

The `Model` project adds repository-specific helper types around them.

### Service contract generation

The service generates an OpenAPI schema during Debug builds. `ModelSharedOut` consumes that schema, merges and normalizes it, and generates:

- a shared C# client/model file used by consumers
- a merged OpenAPI document published by the service

### UI composition

`WebPages` contains reusable UI pages and typed API access. `WebApp` hosts those pages under a Blazor Server shell and provides environment-specific configuration.

### Testing

- `ModelTest` checks low-level construction assumptions.
- `ServiceTest` checks end-to-end API behavior through the generated client.

## Runtime Architecture

### API

The service runs under:

```text
/SurveyInstrument/api
```

Main endpoint groups:

- `SurveyInstrument`
- `ErrorSource`
- `SurveyInstrumentUsageStatistics`

### UI

The hosted UI runs under:

```text
/SurveyInstrument/webapp
```

Main routes:

- `/`
  - Home page
- `/SurveyInstrument`
  - survey instrument management
- `/SurveyInstrumentIdentities`
  - identity catalog management
- `/SurveyInstrumentFeatures`
  - feature-category and option management
- `/StatisticsSurveyInstrument`
  - usage statistics

## Persistence

The service uses SQLite for primary persistence and a file in the `home` folder for usage statistics.

- SQLite database
  - `home/SurveyInstrument.db`
- usage statistics
  - `home/history.json`

Default service behavior seeds the database with:

- default `ErrorSource` records
- default `SurveyInstrument` records
- eight default identity definitions
- sixteen feature categories and their options

Catalog defaults are seeded when their new tables are empty. Schema version 2 adds those catalog tables transactionally to a valid legacy database without rewriting existing error-source or survey-instrument rows. Startup never deletes, renames, replaces, or rebuilds existing tables; unknown, malformed, or newer schemas stop startup without changing data.

The service image mounts `/home/` from the historical `surveyinstrument-claim` PVC. The chart uses a `Recreate` deployment strategy to prevent overlapping SQLite writers, retains Helm-managed PVCs, and accepts `persistence.existingClaim` for an explicit identity cutover. The filename remains `SurveyInstrument.db`.

## Generated Artifacts

The repository contains both hand-authored and generated files. The most important generated artifacts are:

- `ModelSharedOut/SurveyInstrumentMergedModel.cs`
  - NSwag-generated client and DTOs
- `Service/wwwroot/json-schema/SurveyInstrumentMergedModel.json`
  - merged OpenAPI bundle served by Swagger
- `ModelSharedOut/json-schemas/SurveyInstrumentFullName.json`
  - service-generated schema input used by the generator

In general, these files should be regenerated from the pipeline rather than manually edited.

## Typical Developer Workflow

### Build the whole solution

```powershell
dotnet build .\SurveyInstrument.sln
```

### Run the API

```powershell
dotnet run --project .\Service\Service.csproj
```

### Run the UI host

```powershell
dotnet run --project .\WebApp\WebApp.csproj
```

### Run tests

```powershell
dotnet test .\ModelTest\ModelTest.csproj
dotnet test .\ServiceTest\ServiceTest.csproj
```

### Regenerate the shared client/model

```powershell
dotnet build .\Service\Service.csproj -c Debug
dotnet run --project .\ModelSharedOut\ModelSharedOut.csproj
```

## Deployment Assets

Two deployable projects include Docker and Helm assets:

- `Service`
  - Dockerfile plus service Helm chart
- `WebApp`
  - Dockerfile plus webapp Helm chart

The repository follows the established Digiwells/OSDC deployment pattern where service and webapp are hosted as separate containers with matching ingress path bases.
The migrated OSDC identities are:

- .NET root: `OSDC.Drilling.SurveyInstrument`
- NuGet package: `OSDC.Drilling.SurveyInstrument.WebPages`
- service image/chart: `docker.io/digiwells/osdcdrillingsurveyinstrumentservice:stable` / `osdcdrillingsurveyinstrumentservice`
- webapp image/chart: `docker.io/digiwells/osdcdrillingsurveyinstrumentwebappclient:stable` / `osdcdrillingsurveyinstrumentwebappclient`

Deployment is deliberately separate from source migration. Build and publish both images first. Before any upgrade, inspect the selected context, namespace, current Helm release, deployment image and replicas, PVC, and `/home/` mount. For the cutover, pass `--kube-context <context>` and `--set persistence.existingClaim=surveyinstrument-claim`; never uninstall the old release until volume ownership and record counts are verified. Upgrade dev first, verify rollout, image digest, health/API reads, and existing record counts, and only then repeat for production and AWE. The charts default to `stable` with `Always` pull policy.

## Security Notes

Current repository characteristics:

- no built-in authentication or authorization layer in the service
- permissive CORS configuration in the API
- some client-side HTTP code bypasses certificate validation for practicality in internal environments
- SQLite/file persistence stores data in clear text unless infrastructure adds protection

These are important operational assumptions and should be reviewed before using the solution in a more security-sensitive environment.

## Documentation Strategy

This root README explains the solution at repository level. Each project now also has its own project-local `README.md` describing:

- its purpose
- key files
- dependencies
- build/run/test workflow
- maintenance notes

For project-specific details, start with the README inside the corresponding project directory.

## MCP implementation

The checked-in service publishes 29 domain MCP tools covering Survey Instrument, Error Source, identity-catalog, feature-category, versioned backup/restore, and snapshot-drift inspection, together with `ping`. Usage-statistics endpoints are intentionally excluded. The MCP-only patch and drift-check operations provide guarded partial updates and read-only provenance inspection without expanding the REST update surface.

MCP is available over streamable HTTP at `/surveyinstrument/api/mcp` and WebSocket at `/surveyinstrument/api/mcp/ws`. Registration with an external MCP hub is optional and disabled by default.

The tools provide strict input schemas and explicit success-output schemas for full Survey Instrument and Error Source payloads, identity and feature catalogs, embedded assignments, and backup documents. Survey Instrument write schemas enforce `ModelType` as a tagged union by forbidding incompatible family fields, and the service repeats the semantic validation before persistence. `ErrorCode` publishes the complete finite enum. Every tool publishes a human-readable title and MCP safety annotations. Successes provide structured JSON plus text fallback; validation, not-found, conflict, stale-write, and unexpected server failures produce stable sanitized MCP error envelopes. Core Survey Instrument writes and catalog writes require optimistic-concurrency timestamps. Batch restore is schema-versioned, validates the complete document first, and commits instruments plus missing exact-UUID dependencies in one SQLite transaction. Referenced identity/feature definitions cannot be removed. The standalone Error Source store is explicitly a template library: instruments own embedded snapshots, and template changes never silently rewrite an existing instrument. Physical inputs and outputs use SI: angles are radians, distances are metres, gravity is m/s², magnetic flux density is tesla, and error-source `Magnitude` is expressed in the SI unit identified by `MagnitudeQuantity`.
