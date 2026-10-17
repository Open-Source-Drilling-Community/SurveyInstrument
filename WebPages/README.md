# WebPages

This release targets MudBlazor 9.9.0 and the matching OSDC shared web component packages.

`WebPages` is a reusable Razor class library that contains the actual SurveyInstrument Blazor pages and the typed API access layer used by the host application.

It is the main UI feature library of the solution. `WebApp` hosts it, but most of the business-facing UI lives here.

## Responsibilities

- Provide the SurveyInstrument management page.
- Provide the SurveyInstrument edit page.
- Provide identity and feature catalog management pages.
- Provide versioned survey-instrument backup and atomic restore workflows.
- Provide the usage statistics page.
- Wrap the generated NSwag client behind a host-configured abstraction.
- Package the pages as a reusable RCL and NuGet package.

## Architectural Role

This project sits between:

- the generated shared client/model from `ModelSharedOut`
- the deployable Blazor host in `WebApp`

It is where reusable application UI is implemented without tying it to one specific host.

## Main Pages and Components

- `SurveyInstrumentMain.razor`
  - Main management page.
  - Loads error sources and survey instruments in parallel.
  - Displays a searchable MudBlazor grid.
  - Supports add, delete, and row-click edit workflows.
- `SurveyInstrumentEdit.razor`
  - Editor for a selected survey instrument.
  - Supports Wolff and de Wardt and ISCWSA model choices.
  - Supports magnetic and gyro-specific UI behavior.
  - Includes grouped ISCWSA Revision 5 magnetic and gyro error-term sections, including AMIL and the axial-interference correction terms.
  - Preserves environmental fields such as dip, declination, gravity, magnetic field, convergence, latitude, Earth rotation rate, and cant angle while editing.
  - Edits identity values and feature assignments, including validity dates where allowed.
- `SurveyInstrumentIdentities.razor`
  - Manages identity definitions such as official, manufacturer, model, and tool names.
- `SurveyInstrumentFeatures.razor`
  - Manages feature categories, exclusivity/validity flags, and options using the common resource-service catalog grid, bulk selection, validation, deletion confirmation, and add/save/reload actions.
- `SurveyInstrumentBackupRestore.razor`
  - Exports all or selected survey instruments to a versioned JSON file.
  - Previews and validates uploaded backups before an explicitly confirmed atomic restore.
  - Presents the catalog policy before the UUID policy, supporting require-existing or create-missing definitions plus fail-on-conflict or replace-existing instruments.
  - Accepts JSON files up to 256 MiB and requires the current format identifier and schema version.
- `StatisticsSurveyInstrument.razor`
  - Displays total calls, today's calls, tracked endpoint count, and last-save time.
  - Provides a responsive, sortable table by HTTP method, endpoint/operation, logical area, count, and last use.
- `wwwroot/surveyInstrumentBatchBackup.js`
  - Small JavaScript module that downloads the generated backup JSON without navigating away from the app.

## Service Access Layer

- `ISurveyInstrumentWebPagesConfiguration.cs`
  - Contract the host must implement to provide API base URLs.
- `ISurveyInstrumentAPIUtils.cs`
  - Abstraction consumed by the pages.
- `SurveyInstrumentAPIUtils.cs`
  - Concrete implementation using the generated NSwag `Client`.
  - Builds `HttpClient` instances for:
    - SurveyInstrument service
    - UnitConversion service
- `DataUtils.cs`
  - Shared labels, defaults, and small UI utility values.

## Host Integration Requirements

To use this library from a Blazor host:

1. Register an implementation of `ISurveyInstrumentWebPagesConfiguration`.
2. Register `ISurveyInstrumentAPIUtils`.
3. Add this assembly to the host router `AdditionalAssemblies`.

The assembly contributes these routes: `/SurveyInstrument`, `/SurveyInstrumentIdentities`, `/SurveyInstrumentFeatures`, `/SurveyInstrumentBackupRestore`, and `/StatisticsSurveyInstrument`.

`WebApp` already does exactly that in its `Program.cs` and `App.razor`.

## Dependencies

- `MudBlazor`
- `OSDC.UnitConversion.DrillingRazorMudComponents`
- generated source linked from `..\ModelSharedOut\SurveyInstrumentMergedModel.cs`

The linked generated file is compiled into this project as:

```text
GeneratedDtos\SurveyInstrumentMergedModel.cs
```

That gives the pages direct access to the typed NSwag client and DTOs without copying generated code into the repository twice.

## Packaging

This project is configured as a reusable Razor class library with package metadata:

- package id: `OSDC.Drilling.SurveyInstrument.WebPages`
- package README: `README.md`
- repository URL pointing to the SurveyInstrument GitHub repository

## Build

```powershell
dotnet build .\WebPages\WebPages.csproj
```

## Common Maintenance Tasks

- Update `SurveyInstrumentEdit.razor` when new error-source families are introduced.
- Keep UI labels and defaults in `DataUtils.cs` aligned with service expectations.
- Regenerate `ModelSharedOut` when the service contract changes, then rebuild this project.
- Keep configuration fail-fast behavior in `SurveyInstrumentAPIUtils` so host misconfiguration surfaces early.

## Important Implementation Notes

- The pages assume the SurveyInstrument service follows the NSwag-generated contract from `ModelSharedOut`.
- Backup restore is all-or-nothing on the server. `MapExisting` requires exact dependency UUIDs locally, while `MapOrCreateMissing` creates absent catalog definitions. `FailIfExists` rejects an existing instrument UUID; `ReplaceExisting` replaces matching instruments only after the entire document and its dependencies pass validation.
- Embedded error sources in a backup remain frozen snapshots. Included standalone error-source templates preserve provenance but do not become live links.
- `SurveyInstrumentAPIUtils` currently disables TLS certificate validation for its `HttpClientHandler`.
  - This is practical for some internal environments.
  - It should be revisited if stricter client-side transport security is required.
- The project is reusable, but it is not standalone.
  - It requires a host for routing and DI composition.

## Bounded light discovery

Resource and catalog discovery returns light pages, with `offset` defaulting to 0 and `limit` defaulting to 50 (allowed range 1–200). Names and relevant resource-reference filters are evaluated on the server. Unbounded full-object `HeavyData` routes and tools, including filtered full-object lists, have been removed. Existing UUID and HTTP-metadata listings remain available; use named light pages for routine discovery.

REST routes and MCP tool names (relative to the service's existing API path base):

- `/ErrorSource/LightData` / `error_source_discover`
- `/SurveyInstrument/LightData` / `survey_instrument_discover`
- `/SurveyInstrumentFeatureCategory/LightData` / `survey_instrument_feature_category_discover`
- `/SurveyInstrumentIdentity/LightData` / `survey_instrument_identity_discover`

Pages contain `Items`, `Offset`, `Limit`, `TotalCount`, and `NextOffset`; `NextOffset` is null on the final page. Offset pages are individually consistent reads, not a snapshot spanning requests: concurrent inserts/deletes can change later pages. Refresh discovery when the underlying collection changes. EarthGeodesy's existing search pages instead expose `Items[].Value`, `Offset`, `Limit`, and `TotalCount`.

Single-record reads, result/chunk reads, and deliberate backup/export/restore operations are preserved. Backups can still contain complete resources and should be treated as explicit bulk operations.

This is a breaking REST/MCP contract change: update generated clients, UIs, and external integrations together with the service release. Removed heavy-list names are not compatibility aliases. No persisted engineering shapes, database names, or storage volumes are changed. Historical usage counters are retained; bounded discovery has a separate additive `DiscoveryRequestsPerDay` history in resource services.


Tables/selectors use light discovery. Editors retrieve the selected complete resource; category options load only for chosen or already assigned definitions. Slot context uses paginated light locations, and rig-history context reads the selected wellbore. Backup selection uses discovery while the dedicated export/restore APIs retain full backup semantics.

## Package release 2.0.0

This release packages the current WebPages and generated contracts. Update consuming WebApps to `OSDC.Drilling.SurveyInstrument.WebPages` version `2.0.0` after it is available on nuget.org. This is a major update for the bounded light discovery contracts; rebuild consumers together with their updated service clients.

## Trusted NuGet publishing

The `publish-webpages-nuget.yml` workflow authenticates with GitHub OIDC through `NuGet/login@v1`; no long-lived `NUGET_API_KEY` secret is used. Set `NUGET_USER` to the nuget.org profile username as a repository variable (a repository secret is also accepted). The publishing job requests `contents: read` and `id-token: write` and obtains its temporary credential immediately before uploading.

On nuget.org, the package owner must configure a trusted-publishing policy for `Open-Source-Drilling-Community/SurveyInstrument`, workflow filename `publish-webpages-nuget.yml`, with permission to push new versions of `OSDC.Drilling.SurveyInstrument.WebPages`. Leave the policy environment empty because the workflow does not declare a GitHub environment. Trigger the workflow manually with the intended version or push a `webpages-v*` tag. Already published versions are skipped.
