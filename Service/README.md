# Service

`Service` is the ASP.NET Core Web API project for the SurveyInstrument microservice. It exposes CRUD endpoints for survey instruments and error sources, stores data in SQLite, publishes a merged OpenAPI document, and serves the usage-statistics endpoint consumed by the UI.

## Responsibilities

- Host the HTTP API under `/SurveyInstrument/api`.
- Persist `SurveyInstrument` and `ErrorSource` records in SQLite.
- Seed default error sources and default survey instruments when the database is empty.
- Publish a merged OpenAPI/Swagger document generated from `ModelSharedOut`.
- Serve static assets and the generated schema bundle from `wwwroot`.
- Track API usage counts through `UsageStatisticsSurveyInstrument`.

## Runtime Model

The service is configured in `Program.cs` as a conventional ASP.NET Core API application with:

- controllers
- JSON serialization customized through `JsonSettings`
- forwarded-header support
- path-base hosting
- Swagger UI bound to the merged OpenAPI JSON
- CORS configured permissively for current clients

The application path base is:

```text
/SurveyInstrument/api
```

This needs to stay aligned with ingress rules and any client base URLs.

## Key Files

- `Program.cs`
  - Composition root.
  - Registers `SqlConnectionManager`.
  - Adds controllers and Swagger configuration.
  - Applies the `/SurveyInstrument/api` path base.
  - Reads the generated OpenAPI bundle from `wwwroot/json-schema/SurveyInstrumentMergedModel.json`.
- `Controllers/SurveyInstrumentController.cs`
  - CRUD endpoints for full survey instrument resources plus metadata/light-list endpoints.
- `Controllers/ErrorSourceController.cs`
  - CRUD endpoints for error source resources plus metadata endpoints.
- `Controllers/SurveyInstrumentUsageStatisticsController.cs`
  - Returns the usage statistics singleton.
- `Managers/SqlConnectionManager.cs`
  - Owns SQLite connection lifecycle and database initialization.
- `Managers/ErrorSourceManager.cs`
  - Handles persistence and default seeding for `ErrorSource`.
- `Managers/SurveyInstrumentManager.cs`
  - Handles persistence and default seeding for `SurveyInstrument`.
- `SwaggerMiddlewareExtensions.cs`
  - Custom Swagger middleware support for the merged schema file.
- `wwwroot/json-schema/SurveyInstrumentMergedModel.json`
  - Generated OpenAPI bundle served by Swagger.

## API Surface

### Survey instruments

The `SurveyInstrumentController` exposes:

- `GET /SurveyInstrument`
  - list IDs
- `GET /SurveyInstrument/MetaInfo`
  - list `MetaInfo`
- `GET /SurveyInstrument/{id}`
  - fetch full object
- `GET /SurveyInstrument/LightData`
  - fetch lightweight list data
- `GET /SurveyInstrument/HeavyData`
  - fetch all full objects
- `POST /SurveyInstrument`
  - create
- `PUT /SurveyInstrument/{id}`
  - update
- `DELETE /SurveyInstrument/{id}`
  - delete

### Error sources

The `ErrorSourceController` exposes the same shape for `ErrorSource` resources:

- IDs
- `MetaInfo`
- single full resource
- full list
- create/update/delete

### Usage statistics

- `GET /SurveyInstrumentUsageStatistics`

Returns the file-backed statistics object maintained in the `Model` project.

## Storage

The service uses SQLite via `Microsoft.Data.Sqlite`.

Important storage characteristics:

- the database file lives under the solution/container `home` directory
- JSON payloads for full objects are stored in SQLite tables
- lightweight list endpoints project selected columns instead of always deserializing full documents
- managers validate and reseed defaults if the database appears empty or corrupted

This design favors simple deployment and debugging over advanced database normalization.

## Build-Time Swagger Generation

`Service.csproj` contains a Debug build target:

```xml
<Target Name="CreateSwaggerJson" AfterTargets="Build" Condition="$(Configuration)=='Debug'">
```

That target runs `dotnet swagger tofile` and writes:

- `..\ModelSharedOut\json-schemas\SurveyInstrumentFullName.json`

This is the input consumed by `ModelSharedOut` to regenerate the shared client/model.

## Dependencies

- `Model`
- `Microsoft.Data.Sqlite`
- `Microsoft.OpenApi`
- `Microsoft.OpenApi.Readers`
- `Swashbuckle.AspNetCore.SwaggerGen`
- `Swashbuckle.AspNetCore.SwaggerUI`

## Running Locally

```powershell
dotnet run --project .\Service\Service.csproj
```

Useful URLs once running:

- API base: `http://localhost:<port>/SurveyInstrument/api`
- Swagger UI: `http://localhost:<port>/SurveyInstrument/api/swagger`

The exact port depends on `Properties/launchSettings.json` or your hosting environment.

## Deployment

This project includes:

- `Dockerfile`
- Helm chart under `charts/norcedrillingsurveyinstrumentservice`

The chart contains Kubernetes manifests for:

- deployment
- service
- ingress
- service account
- PVC
- HPA

## Cautions

- The service intentionally allows broad CORS access today.
- Authentication and authorization are not implemented here.
- The service trusts generated DTO/schema compatibility; if the generated contract is stale, clients can drift.
- `SurveyInstrumentManager` and `ErrorSourceManager` are singleton-style managers built around a shared SQLite backend. They are simple and pragmatic, not a full repository abstraction.

## Useful Commands

Build:

```powershell
dotnet build .\Service\Service.csproj
```

Regenerate service-side Swagger input for `ModelSharedOut`:

```powershell
dotnet build .\Service\Service.csproj -c Debug
```
