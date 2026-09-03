# WebApp

`WebApp` is the deployable Blazor Server host for the SurveyInstrument user interface. It provides the application shell, host-specific configuration, routing, navigation, and deployment packaging around the reusable Razor components in `WebPages`.

## Responsibilities

- Host the Blazor Server application under `/SurveyInstrument/webapp`.
- Register MudBlazor and the reusable `WebPages` services.
- Supply host-specific configuration such as service base URLs.
- Expose the navigation menu, layout, and home page.
- Package the UI into a Docker image and Helm chart for deployment.

## Architectural Role

The solution intentionally separates UI concerns:

- `WebPages`
  - reusable Razor class library with the actual SurveyInstrument pages and API helper abstraction
- `WebApp`
  - concrete host that wires configuration, routing, static files, layout, and deployment

This makes `WebPages` reusable from other hosts while keeping deployment-specific concerns here.

## Key Files

- `Program.cs`
  - Configures Razor Pages, Blazor Server, and MudBlazor.
  - Registers `ISurveyInstrumentWebPagesConfiguration`.
  - Registers `ISurveyInstrumentAPIUtils`.
  - Applies path base `/SurveyInstrument/webapp`.
- `App.razor`
  - Router entry point.
  - Includes `OSDC.Drilling.SurveyInstrument.WebApp.ExternalRazorAssemblies.All` as `AdditionalAssemblies` so routes from `WebPages` are discovered.
- `Shared/MainLayout.razor`
  - Defines the MudBlazor app frame and drawer.
- `Shared/NavMenu.razor`
  - Navigation links for instrument management, identities, features, backup/restore, and usage statistics.
- `Pages/HomePage.razor`
  - Root route for `/SurveyInstrument/webapp/`.
- `WebPagesHostConfiguration.cs`
  - Concrete implementation of the configuration contract required by `WebPages`.
- `Shared/APIUtils.cs`, `Shared/DataUtils.cs`
  - Host helpers used in the UI shell and components.
- `Components/ScatterPlot.razor`
  - Plotly-based chart component.

## Configuration

The host reads at least these configuration values:

- `SurveyInstrumentHostURL`
- `UnitConversionHostURL`

These are passed into `WebPagesHostConfiguration` and then consumed by `SurveyInstrumentAPIUtils` inside the reusable UI library.

If these values are missing, `WebPages` will fail fast because its API utility requires non-empty configuration.

## Routing

Key routes exposed through this host:

- `/`
  - rendered as `HomePage` once combined with the path base, this is `.../SurveyInstrument/webapp/`
- `/SurveyInstrument`
  - main SurveyInstrument management page from `WebPages`
- `/SurveyInstrumentIdentities`
  - identity-definition catalog from `WebPages`
- `/SurveyInstrumentFeatures`
  - feature-category and option catalog from `WebPages`
- `/SurveyInstrumentBackupRestore`
  - versioned JSON backup and atomic restore workflow from `WebPages`
- `/StatisticsSurveyInstrument`
  - summary and sortable per-endpoint usage statistics from `WebPages`

Because the app uses:

```csharp
app.UsePathBase("/SurveyInstrument/webapp");
```

the effective browser URLs all live under that base path.

## Dependencies

- `WebPages`
- `MudBlazor`
- `OSDC.UnitConversion.DrillingRazorMudComponents`
- `Plotly.Blazor`

## Running Locally

```powershell
dotnet run --project .\WebApp\WebApp.csproj
```

The host also depends on the SurveyInstrument API and UnitConversion API being reachable at the configured URLs.

## Build and Packaging

Build:

```powershell
dotnet build .\WebApp\WebApp.csproj
```

Deployment assets included here:

- `Dockerfile`
- Helm chart under `charts/osdcdrillingsurveyinstrumentwebappclient`
- image `docker.io/digiwells/osdcdrillingsurveyinstrumentwebappclient:stable` with `imagePullPolicy: Always`

Keep deployment gated behind successful source verification and confirmed image publication. Use `helm ... --kube-context <context>`, deploy dev first, and verify the home route plus all five `WebPages` routes listed above before production or AWE. The host owns only `/`; the referenced `WebPages` assembly contributes those five feature routes and does not add `/Home` or another root route.

## Common Maintenance Tasks

- Update `NavMenu.razor` when new pages are added.
- Keep `UsePathBase(...)` aligned with ingress configuration.
- Keep appsettings values aligned with actual environment endpoints.
- If `WebPages` adds new routes, ensure `App.razor` continues to include the proper `AdditionalAssemblies`.
- Keep the navigation labels and home-page capability summary synchronized with the reusable pages.

## Typical Failure Modes

- Incorrect `SurveyInstrumentHostURL`
  - UI loads, but data calls fail.
- Incorrect `UnitConversionHostURL`
  - pages using unit conversion helpers fail.
- Path-base mismatch between host and ingress
  - routes and static assets break.
- Locked build outputs while running under Visual Studio
  - local rebuilds can fail until the running host releases the files.
