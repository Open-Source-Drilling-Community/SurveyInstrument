using OSDC.DotnetLibraries.Drilling.Surveying;
using System;
using System.Collections.Generic;

namespace OSDC.Drilling.SurveyInstrument.Model;

public enum SurveyInstrumentBatchExportScope { Unspecified = 0, All = 1, Selected = 2 }

public sealed class SurveyInstrumentBatchExportRequest
{
    public SurveyInstrumentBatchExportScope Scope { get; set; }
    public List<Guid>? SurveyInstrumentIDs { get; set; }
}

/// <summary>A portable, versioned logical backup containing frozen instruments and their local dependencies.</summary>
public sealed class SurveyInstrumentBatchExportDocument
{
    public const string CurrentFormatIdentifier = "OSDC.Drilling.SurveyInstrument.BatchExport";
    public const int CurrentSchemaVersion = 1;
    public string FormatIdentifier { get; set; } = CurrentFormatIdentifier;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public DateTimeOffset ExportedAtUtc { get; set; }
    public SurveyInstrumentBatchCatalogDependencies CatalogDependencies { get; set; } = new();
    public List<SurveyInstrument> SurveyInstruments { get; set; } = [];
}

public sealed class SurveyInstrumentBatchCatalogDependencies
{
    public List<ErrorSource> ErrorSourceTemplates { get; set; } = [];
    public List<SurveyInstrumentIdentity> Identities { get; set; } = [];
    public List<SurveyInstrumentFeatureCategory> FeatureCategories { get; set; } = [];
}

public enum SurveyInstrumentBatchRestoreConflictPolicy
{
    Unspecified = 0,
    FailIfExists = 1,
    ReplaceExisting = 2
}

public enum SurveyInstrumentBatchCatalogRestorePolicy
{
    Unspecified = 0,
    MapExisting = 1,
    MapOrCreateMissing = 2
}

public sealed class SurveyInstrumentBatchRestoreRequest
{
    public SurveyInstrumentBatchRestoreConflictPolicy ConflictPolicy { get; set; }
    public SurveyInstrumentBatchCatalogRestorePolicy CatalogPolicy { get; set; } =
        SurveyInstrumentBatchCatalogRestorePolicy.MapOrCreateMissing;
    public SurveyInstrumentBatchExportDocument? Document { get; set; }
}

public sealed class SurveyInstrumentBatchRestoreResponse
{
    public DateTimeOffset RestoredAtUtc { get; set; }
    public int CreatedCount { get; set; }
    public int ReplacedCount { get; set; }
    public int CreatedCatalogDefinitionCount { get; set; }
    public List<Guid> SurveyInstrumentIDs { get; set; } = [];
}

public sealed class SurveyInstrumentBatchErrorEnvelope
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<SurveyInstrumentBatchError> Errors { get; set; } = [];
}

public sealed class SurveyInstrumentBatchError
{
    public int? PositionIndex { get; set; }
    public string Property { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
