using System;
using System.Collections.Generic;

namespace OSDC.Drilling.SurveyInstrument.Model;

/// <summary>Discovery projection; excludes engineering arrays and nested definitions.</summary>
public sealed class ErrorSourceLight
{
    public string? Name => ErrorCode?.ToString();
    public OSDC.DotnetLibraries.General.DataManagement.MetaInfo? MetaInfo { get; set; }
    public global::OSDC.DotnetLibraries.Drilling.Surveying.ErrorCode? ErrorCode { get; set; }
    public string? Description { get; set; }
    public int? Index { get; set; }
    public string? PropagationMode { get; set; }
    public bool IsSystematic { get; set; }
    public bool IsRandom { get; set; }
    public bool IsGlobal { get; set; }
    public bool SingularIssues { get; set; }
    public bool IsContinuous { get; set; }
    public bool IsStationary { get; set; }
    public bool KOperatorImposed { get; set; }
    public double? Magnitude { get; set; }
    public string? MagnitudeQuantity { get; set; }
    public bool UseInclinationInterval { get; set; }
    public double? StartInclination { get; set; }
    public double? EndInclination { get; set; }
    public double? InitInclination { get; set; }
}

/// <summary>A bounded UUID-ordered page. NextOffset is null on the final page.</summary>
public sealed class ErrorSourceDiscoveryPage
{
    public List<ErrorSourceLight> Items { get; set; } = new();
    public int Offset { get; set; }
    public int Limit { get; set; }
    public int TotalCount { get; set; }
    public int? NextOffset { get; set; }
}

/// <summary>A bounded UUID-ordered page. NextOffset is null on the final page.</summary>
public sealed class SurveyInstrumentDiscoveryPage
{
    public List<SurveyInstrumentLight> Items { get; set; } = new();
    public int Offset { get; set; }
    public int Limit { get; set; }
    public int TotalCount { get; set; }
    public int? NextOffset { get; set; }
}

/// <summary>Discovery projection; excludes engineering arrays and nested definitions.</summary>
public sealed class SurveyInstrumentFeatureCategoryLight
{
    public OSDC.DotnetLibraries.General.DataManagement.MetaInfo? MetaInfo { get; set; }
    public string? Name { get; set; }
    public bool IsExclusive { get; set; }
    public bool HasValidityPeriod { get; set; }
    public System.DateTimeOffset? CreationDate { get; set; }
    public System.DateTimeOffset? LastModificationDate { get; set; }
}

/// <summary>A bounded UUID-ordered page. NextOffset is null on the final page.</summary>
public sealed class SurveyInstrumentFeatureCategoryDiscoveryPage
{
    public List<SurveyInstrumentFeatureCategoryLight> Items { get; set; } = new();
    public int Offset { get; set; }
    public int Limit { get; set; }
    public int TotalCount { get; set; }
    public int? NextOffset { get; set; }
}

/// <summary>Discovery projection; excludes engineering arrays and nested definitions.</summary>
public sealed class SurveyInstrumentIdentityLight
{
    public OSDC.DotnetLibraries.General.DataManagement.MetaInfo? MetaInfo { get; set; }
    public string? Name { get; set; }
    public System.DateTimeOffset? CreationDate { get; set; }
    public System.DateTimeOffset? LastModificationDate { get; set; }
}

/// <summary>A bounded UUID-ordered page. NextOffset is null on the final page.</summary>
public sealed class SurveyInstrumentIdentityDiscoveryPage
{
    public List<SurveyInstrumentIdentityLight> Items { get; set; } = new();
    public int Offset { get; set; }
    public int Limit { get; set; }
    public int TotalCount { get; set; }
    public int? NextOffset { get; set; }
}
