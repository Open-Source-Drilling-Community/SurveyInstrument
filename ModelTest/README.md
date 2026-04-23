# ModelTest

`ModelTest` contains NUnit-based tests for the domain model layer used by the SurveyInstrument solution.

At the moment the suite is intentionally small. It is closer to a smoke test than to a full behavioral specification. Its current purpose is to ensure that the upstream surveying types and the solution's expected object construction patterns still work together.

## Responsibilities

- Verify that core `ErrorSource` and `SurveyInstrument` objects can be instantiated.
- Confirm a few key values survive assignment and retrieval.
- Catch obvious package or serialization-shape regressions early.

## Key Files

- `Tests.cs`
  - Creates an `ErrorSource` instance and asserts key properties such as `Magnitude`.
  - Creates a `SurveyInstrument` instance with one error source and validates basic structure and enum usage.

## What Is Actually Tested

The current test suite validates:

- construction of `OSDC.DotnetLibraries.Drilling.Surveying.ErrorSource`
- construction of `OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument`
- expected linkage between a survey instrument and its error source list
- selected default gyro ISCWSA-related fields

It does not currently validate:

- service persistence
- OpenAPI generation
- Blazor UI behavior
- detailed survey mathematics
- default data seeded by `Service`

## Dependencies

- `NUnit`
- `NUnit3TestAdapter`
- `coverlet.collector`
- project reference to `Model`

Although the project references `Model`, most assertions currently exercise types from the OSDC surveying package that `Model` depends on.

## Running Tests

```powershell
dotnet test .\ModelTest\ModelTest.csproj
```

## When To Extend This Project

Add tests here when you change:

- `SurveyInstrumentLight`
- usage statistics behavior in `UsageStatisticsSurveyInstrument`
- assumptions about default property values used by callers

If the behavior depends on an actual running API, prefer `ServiceTest` instead.

## Recommended Future Coverage

- tests for `History.Increment()` date rollover behavior
- tests for `UsageStatisticsSurveyInstrument` backup throttling and singleton load behavior
- tests that exercise `SurveyInstrumentLight` construction paths used by the service
