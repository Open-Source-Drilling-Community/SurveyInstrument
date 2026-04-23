# Model

`Model` is the small domain-support project for the SurveyInstrument solution. It does not define the full `SurveyInstrument` and `ErrorSource` classes itself; those come from `OSDC.DotnetLibraries.Drilling.Surveying`. Instead, this project provides local types that the service and UI need around that upstream model.

## Responsibilities

- Provide a lightweight listing/view model for survey instruments through `SurveyInstrumentLight`.
- Persist and expose in-memory usage statistics through `UsageStatisticsSurveyInstrument`.
- Carry the DocFX inputs used to document the project model surface.

## Why This Project Exists

The core survey-domain objects are shared from external OSDC packages. The SurveyInstrument microservice still needs a few solution-specific types:

- a lightweight record used when the UI only needs metadata for tables and search
- usage counters for API operations, persisted outside the database

Keeping those types here avoids coupling the service and UI projects to each other.

## Key Files

- `SurveyInstrumentLight.cs`
  - Lightweight representation of a survey instrument.
  - Stores `MetaInfo`, `Name`, `Description`, `CreationDate`, and `LastModificationDate`.
  - Used by the service `LightData` endpoint and by the web UI grid views.
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
- Avoid duplicating upstream OSDC model types here; this project is for solution-specific support types only.
