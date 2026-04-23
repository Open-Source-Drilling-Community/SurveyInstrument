# ModelSharedOut

`ModelSharedOut` is the code-generation project that produces the shared OpenAPI artifacts consumed by clients of the SurveyInstrument service.

It is intentionally not a hand-authored domain library. Its job is to merge OpenAPI schema inputs, normalize schema names, generate a C# client/model file, and emit a bundled OpenAPI document for the service to publish.

## Responsibilities

- Read OpenAPI JSON fragments from `json-schemas/`.
- Merge them into a single OpenAPI bundle.
- Normalize schema references so generated type names are short and usable.
- Generate `SurveyInstrumentMergedModel.cs` with NSwag.
- Emit `SurveyInstrumentMergedModel.json` into `Service/wwwroot/json-schema/`.

## Why This Project Exists

The solution follows a distributed shared model pattern:

- the service defines and publishes an OpenAPI contract
- clients consume a generated contract instead of manually maintaining DTOs

This project is the bridge between those two worlds. It turns OpenAPI definitions into strongly typed client-side code and a bundled JSON schema that the service can expose.

## Key Files

- `Program.cs`
  - Main console entry point.
  - Walks upward to find the solution root.
  - Reads `json-schemas/*.json`.
  - Merges schemas and paths into one `OpenApiDocument`.
  - Forces OpenAPI output version from `3.0.4` to `3.0.3` for downstream tooling compatibility.
  - Generates the C# client/model file through NSwag.
- `OpenApiSchemaReferenceUpdater.cs`
  - Central helper for schema merging and deep reference rewriting.
  - Clones schemas, renames keys, and updates nested references across paths, request bodies, responses, parameters, and component schemas.
- `SurveyInstrumentMergedModel.cs`
  - Generated output.
  - Referenced by `WebPages` and `ServiceTest`.
  - Should generally not be edited manually.
- `json-schemas/SurveyInstrumentFullName.json`
  - Input schema bundle currently produced from the service build.

## Generated Outputs

### C# output

- `ModelSharedOut\SurveyInstrumentMergedModel.cs`
- Namespace: `NORCE.Drilling.SurveyInstrument.ModelShared`

This file contains:

- NSwag-generated DTOs
- an NSwag-generated `Client`
- request/response support types used by consumers such as `ServiceTest` and `WebPages`

### JSON output

- `Service\wwwroot\json-schema\SurveyInstrumentMergedModel.json`

This file is what the service exposes through its custom Swagger middleware.

## Dependencies

- `Microsoft.OpenApi.Readers`
  - Parses and serializes OpenAPI documents.
- `NSwag.CodeGeneration.CSharp`
  - Generates the client and DTO code.

## Typical Workflow

1. Build the service in Debug so Swagger output is refreshed into `ModelSharedOut/json-schemas/SurveyInstrumentFullName.json`.
2. Run `ModelSharedOut` to regenerate the merged client/model artifacts.
3. Rebuild any consumers that compile `SurveyInstrumentMergedModel.cs`.

Example:

```powershell
dotnet build .\Service\Service.csproj -c Debug
dotnet run --project .\ModelSharedOut\ModelSharedOut.csproj
```

## Important Constraints

- Treat generated outputs as derived artifacts.
- If type names look wrong, the fix usually belongs in:
  - the upstream OpenAPI schema generation
  - the reference updater
  - the NSwag generator settings
- `Program.cs` is interactive when outputs already exist and asks for overwrite confirmation.

## Common Maintenance Tasks

- Add more schema inputs to `json-schemas/` when this service depends on additional microservice models.
- Adjust `CustomTypeNameGenerator` if naming collisions appear.
- Keep the OpenAPI compatibility workaround aligned with the Swagger UI/tooling actually used by the service.
