# Model

`Model` is the domain-support project for the SurveyInstrument solution. `ErrorSource` and the physical survey properties come from `OSDC.DotnetLibraries.Drilling.Surveying`; the local `SurveyInstrument` extends that upstream type with identity and feature assignments.

## Responsibilities

- Provide a lightweight listing/view model for survey instruments through `SurveyInstrumentLight`.
- Define identity/feature catalogs and their instrument assignments.
- Define the versioned logical backup/restore request, document, result, and error contracts.
- Persist and expose in-memory usage statistics through `UsageStatisticsSurveyInstrument`.
- Carry the DocFX inputs used to document the project model surface.

## Why This Project Exists

The core survey-domain objects are shared from external OSDC packages. The SurveyInstrument microservice still needs a few solution-specific types:

- a lightweight record used when the UI only needs metadata for tables and search
- identity and feature definitions plus per-instrument assignments
- versioned logical backup/restore wire contracts
- usage counters for API operations, persisted outside the database

Keeping those types here avoids coupling the service and UI projects to each other.

## Key Files

- `SurveyInstrumentLight.cs`
  - Lightweight representation of a survey instrument.
  - Stores `MetaInfo`, `Name`, `Description`, `CreationDate`, and `LastModificationDate`.
  - Used by the service `LightData` endpoint and by the web UI grid views.
- `SurveyInstrument.cs`
  - Extends the shared surveying model with identity and feature assignments.
- `SurveyInstrumentIdentity*.cs`, `SurveyInstrumentFeature*.cs`
  - Catalog definitions, options, and assignment contracts based on shared data-management interfaces.
- `SurveyInstrumentBatch.cs`
  - Schema-versioned logical backup/restore contracts for instruments and exact-UUID catalog dependencies.
  - Supports `All` and `Selected` export scopes.
  - Carries error-source templates, identity definitions, and feature categories required by the exported instruments.
  - Defines `FailIfExists` and `ReplaceExisting` restore policies plus structured per-position validation errors.
- `UsageStatisticsSurveyInstrument.cs`
  - Defines `CountPerDay`, `History`, and `UsageStatisticsSurveyInstrument`.
  - Tracks per-endpoint usage counts for both `SurveyInstrument` and `ErrorSource` operations.
  - Persists state to `../home/history.json` on a timed backup interval.
- `docfx.json`, `api/`, `articles/`
  - Documentation inputs for generated API/article docs.

## Dependencies

- `OSDC.DotnetLibraries.Drilling.Surveying`
  - Supplies `SurveyInstrument`, `ErrorSource`, `ErrorCode`, `SurveyInstrumentModelType`, and related drilling survey types.
- `OSDC.DotnetLibraries.General.DataManagement`
  - Supplies `MetaInfo`.

## Data Flow

### `SurveyInstrumentLight`

The service stores full survey instrument objects in SQLite as JSON. When the API only needs metadata for list rendering, the service builds `SurveyInstrumentLight` objects from table columns rather than deserializing the entire heavy object graph.

### `UsageStatisticsSurveyInstrument`

The service controllers increment counters on each API call. Those counters are accumulated per UTC day and periodically serialized to `home/history.json`. The statistics endpoint simply returns the singleton state.

This means:

- statistics are file-backed, not database-backed
- statistics survival depends on preserving the `home` directory
- persistence is best-effort, intentionally tolerant of IO errors

### Batch documents

`SurveyInstrumentBatchExportDocument` is a portable logical backup rather than a copy of the SQLite file. The current contract uses format identifier `OSDC.Drilling.SurveyInstrument.BatchExport` and schema version `1`. Embedded instrument error sources remain frozen snapshots; catalog templates are included as dependencies and are not live links.

The model intentionally separates validation errors from the restore result. A successful result reports created instruments, replaced instruments, newly created catalog definitions, and affected UUIDs. The service owns validation and transaction semantics; these model types only describe the wire contract.

## Operational Notes

- `HOME_DIRECTORY` is relative: `..\home\`
  - In practice this resolves correctly when the service runs from its output folder and the repository or container layout preserves the expected structure.
- `UsageStatisticsSurveyInstrument` uses a singleton with a lock
  - good enough for the current service style
  - not designed as a distributed counter
- backup is throttled by `BackUpInterval`
  - default is 5 minutes
  - frequent requests do not force writes on every operation

## Build and Test

Build this project directly:

```powershell
dotnet build .\Model\Model.csproj
```

Unit coverage for this project lives in `ModelTest`.

## Common Maintenance Tasks

- Add new statistics fields when service endpoints are added.
- Keep `SurveyInstrumentLight` aligned with the columns projected by `SurveyInstrumentManager.GetAllSurveyInstrumentLight()`.
- Keep the local `SurveyInstrument` extension limited to microservice-owned metadata and assignments; physical survey behavior remains in the upstream model.
- Increment the batch schema version only for an intentional format change, and keep the service, generated client, UI, MCP schemas, and tests synchronized.
