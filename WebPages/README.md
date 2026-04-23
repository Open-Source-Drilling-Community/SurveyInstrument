# WebPages

`WebPages` is a reusable Razor class library that contains the actual SurveyInstrument Blazor pages and the typed API access layer used by the host application.

It is the main UI feature library of the solution. `WebApp` hosts it, but most of the business-facing UI lives here.

## Responsibilities

- Provide the SurveyInstrument management page.
- Provide the SurveyInstrument edit page.
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
  - Includes grouped ISCWSA magnetic and gyro error-term sections.
- `StatisticsSurveyInstrument.razor`
  - Displays aggregate usage statistics returned by the service.

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

- package id: `NORCE.Drilling.SurveyInstrument.WebPages`
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
- `SurveyInstrumentAPIUtils` currently disables TLS certificate validation for its `HttpClientHandler`.
  - This is practical for some internal environments.
  - It should be revisited if stricter client-side transport security is required.
- The project is reusable, but it is not standalone.
  - It requires a host for routing and DI composition.
