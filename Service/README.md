# Service

`Service` is the ASP.NET Core Web API project for the SurveyInstrument microservice. It exposes survey instruments, error sources, identity definitions, and feature-category catalogs, stores data in SQLite, publishes a merged OpenAPI document, and serves the usage-statistics endpoint consumed by the UI.

## Responsibilities

- Host the HTTP API under `/SurveyInstrument/api`.
- Persist `SurveyInstrument` and `ErrorSource` records in SQLite.
- Seed default error sources and default survey instruments when the database is empty.
- Seed the standard identity and survey-feature taxonomies.
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
  - CRUD endpoints for full survey instrument resources, metadata/light-list endpoints, and versioned batch export/restore.
- `Controllers/ErrorSourceController.cs`
  - CRUD endpoints for error source resources plus metadata endpoints.
- `Controllers/SurveyInstrumentIdentityController.cs`
  - CRUD endpoints for identity definitions with optimistic concurrency and reference protection.
- `Controllers/SurveyInstrumentFeatureCategoryController.cs`
  - CRUD endpoints for feature categories/options with optimistic concurrency and reference protection.
- `Controllers/SurveyInstrumentUsageStatisticsController.cs`
  - Returns the usage statistics singleton.
- `Managers/SqlConnectionManager.cs`
  - Owns SQLite connection lifecycle and database initialization.
- `Managers/ErrorSourceManager.cs`
  - Handles persistence and default seeding for `ErrorSource`.
- `Managers/SurveyInstrumentManager.cs`
  - Handles persistence, default seeding, identity/feature assignment validation, and model-family semantic validation for `SurveyInstrument`.
- `SurveyInstrumentBatchService.cs`
  - Produces portable logical backups and validates/restores them atomically with exact-UUID catalog dependencies.
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

### Identities and features

`SurveyInstrumentIdentityController` and `SurveyInstrumentFeatureCategoryController` expose IDs, metadata, individual/full-list retrieval, and create/update/delete operations. Survey instruments embed assignments by catalog UUID. The service rejects missing references, mismatched category/option pairs, invalid validity periods, duplicate assignment UUIDs, and overlapping selections in exclusive categories.

## Storage

The service uses SQLite via `Microsoft.Data.Sqlite`.

Important storage characteristics:

- the database file lives under the solution/container `home` directory
- JSON payloads for full objects are stored in SQLite tables
- lightweight list endpoints project selected columns instead of always deserializing full documents
- a fresh database is created in one transaction and then seeded by the resource managers
- schema version 2 transactionally adds identity and feature-category tables to the valid legacy schema without rewriting rows
- unknown, malformed, incomplete-current, and newer schemas fail closed without mutation
- the filename remains `SurveyInstrument.db`

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
- Helm chart under `charts/osdcdrillingsurveyinstrumentservice`

The chart contains Kubernetes manifests for:

- deployment
- service
- ingress
- service account
- PVC
- HPA

The service image is `docker.io/digiwells/osdcdrillingsurveyinstrumentservice:stable`. The chart defaults to `imagePullPolicy: Always`, one replica, and a `Recreate` strategy. It mounts `/home/` from `surveyinstrument-claim`; set `persistence.existingClaim=surveyinstrument-claim` explicitly during the identity cutover. A chart-created PVC has Helm's `keep` policy. Do not uninstall the legacy release or deploy a new image until the PVC, mount, current record counts, and image publication have been confirmed.

Use Helm's `--kube-context` option. Roll out dev first and verify rollout status, the running image digest, service/API health, and unchanged record counts before production or AWE.

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

## MCP server

The service publishes all 27 non-statistics REST operations plus five MCP-only operations—patch, granular error-source snapshot mutation, error-source drift check, single-record catalog-reference validation, and bounded catalog-reference audit—as 32 domain MCP tools. Usage-statistics operations are not exposed.

Tool descriptions distinguish compact discovery (`get_all_ids`, `get_all_meta_info`, and Survey Instrument `get_all_light`) from complete-model retrieval. Create and update tools expose explicit nested JSON Schemas rather than generic object bodies; `ModelType` is an enforcing four-branch discriminator and `ErrorCode` is the complete enum rather than free text. Every tool also publishes an explicit success-output schema, title, and MCP read-only/destructive/idempotent/open-world annotations. Unknown top-level arguments are rejected before controller invocation. Successful calls return structured JSON and a text fallback; failed HTTP-style results and unexpected exceptions are converted to stable sanitized MCP error envelopes.

`survey_instrument_batch_export` creates a schema-version-1 backup of all instruments or a selected set with its catalog dependencies. `survey_instrument_batch_restore` validates format/version, UUID uniqueness, model semantics, references, conflicts, and catalog compatibility before writing anything; restoration is committed in one SQLite transaction.

Survey Instrument update, patch, and delete require `expectedModifiedUtc` from the latest read and return `stale_write` on a mismatch. Patch uses top-level JSON Merge Patch semantics: omitted fields are retained, arrays are replaced as a whole, and resource identity/server timestamps are protected. Error Source CRUD manages a template library. `ErrorSourceList` entries embedded in an instrument are authoritative snapshots; a copied template UUID records provenance, but later template edits do not propagate into stored instruments.

`survey_instrument_check_error_source_drift` compares those frozen snapshots with current same-UUID templates and reports `in_sync`, `drifted`, or `catalog_missing` without modifying either side.

`survey_instrument_validate_catalog_references` and `survey_instrument_audit_catalog_references` verify that identity, feature-category, and category-scoped feature-option assignment UUIDs still resolve in the local catalogs. Normal creates, updates, and restores already enforce these relationships; the diagnostics are intended for legacy imports or externally corrupted databases. The audit is deterministic, UUID ordered, and capped at 100 records per call.

Successful `error_source_update_by_id` calls return the IDs and count of survey instruments carrying same-UUID frozen snapshots, together with a non-fatal warning when any exist. The update never propagates into those snapshots.

`survey_instrument_error_source_mutate` performs a concurrency-protected `add`, `replace`, or `remove` of one embedded snapshot. This avoids whole-array replacement while retaining model-family validation; in particular, removing the final snapshot from an ISCWSA instrument is rejected.

The schemas document the caller-owned `MetaInfo.ID`, the requirement that an update path ID match the body's ID, all four survey model families, embedded `ErrorSourceList` objects, identity and feature assignments, catalog concurrency tokens, classification flags, and inclination intervals.

MCP physical values follow the service's SI convention: angular quantities are radians, gravity is m/s², magnetic flux density is tesla, distance is metres, and relative errors/factors are dimensionless. An error source's `Magnitude` uses the SI unit of the UnitConversion quantity named by `MagnitudeQuantity`; it is not a display-unit value. `Use...` flags determine whether their corresponding optional Wolff-DeWardt parameters participate in that model.

- Streamable HTTP: `/surveyinstrument/api/mcp`
- WebSocket: `/surveyinstrument/api/mcp/ws`
- Utility tool: `ping`
- Optional external MCP-hub registration: configured in `appsettings.json`, disabled by default
