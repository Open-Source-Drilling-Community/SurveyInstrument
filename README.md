# SurveyInstrument Solution

The SurveyInstrument repository contains a complete microservice-based solution for storing, editing, and serving drilling survey instrument definitions together with reusable UI components, generated client contracts, and automated tests.

The solution revolves around two main domain families from the upstream OSDC drilling-surveying libraries:

- `SurveyInstrument`
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

if the database is empty or appears corrupted.

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

The repository follows the Digiwells/NORCE deployment pattern where service and webapp are hosted as separate containers with matching ingress path bases.

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
