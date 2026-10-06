using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using OSDC.DotnetLibraries.Drilling.SemanticCatalogue;
using Catalogue = OSDC.DotnetLibraries.Drilling.SemanticCatalogue.SemanticCatalogue;

namespace OSDC.Drilling.SurveyInstrument.Service;

/// <summary>Single reviewed semantic registry shared by OpenAPI and MCP schema emission.</summary>
internal static class SurveyInstrumentProviderSemantics
{
    private static readonly IReadOnlyDictionary<string, (string Concept, string? Role, string? Reference)> Bindings =
        new Dictionary<string, (string, string?, string?)>(StringComparer.Ordinal)
        {
            ["SurveyInstrument"] = (Concepts.SurveyInstrument, null, null),
            ["SurveyInstrumentModel"] = (Concepts.SurveyInstrumentModel, null, null),
            ["SurveyErrorSource"] = (Concepts.SurveyErrorSource, null, null),
            ["SurveyErrorSourceCode"] = (Concepts.SurveyErrorSourceCode, null, null),
            ["ErrorSourceOrderingIndex"] = (Concepts.ErrorSourceOrderingIndex, null, null),
            ["ErrorPropagationMode"] = (Concepts.ErrorPropagationMode, null, null),
            ["KOperatorImposed"] = (Concepts.KOperatorImposed, null, null),
            ["SurveyErrorMagnitude"] = (Concepts.SurveyErrorMagnitude, null, null),
            ["ErrorMagnitudeQuantityIdentifier"] = (Concepts.ErrorMagnitudeQuantityIdentifier, null, null),
            ["MagneticDip"] = (Concepts.MagneticDip, null, null),
            ["MagneticDeclination"] = (Concepts.MagneticDeclination, null, null),
            ["GravityAcceleration"] = (Concepts.GravityAcceleration, null, null),
            ["EarthMagneticFluxDensity"] = (Concepts.EarthMagneticFluxDensity, null, null),
            ["GridConvergence"] = (Concepts.GridConvergence, null, Concepts.GridConvergenceTrueToGridClockwise),
            ["Latitude"] = (Concepts.Latitude, null, Concepts.Wgs84),
            ["EarthAngularVelocity"] = (Concepts.EarthAngularVelocity, null, null),
            ["SurveyInstrumentCantAngle"] = (Concepts.SurveyInstrumentCantAngle, null, Concepts.OrthogonalBodyFrameCantConvention),
            ["SurveyToolRunningSpeed"] = (Concepts.SurveyToolRunningSpeed, null, null),
            ["GyroSwitchingParameter"] = (Concepts.GyroSwitchingParameter, null, null),
            ["GyroReinitializationDistance"] = (Concepts.GyroReinitializationDistance, null, null),
            ["GyroNoiseReductionFactor"] = (Concepts.GyroNoiseReductionFactor, null, null),
            ["ErrorApplicabilityStart"] = (Concepts.WellboreInclination, Concepts.ErrorApplicabilityStart, null),
            ["ErrorApplicabilityEnd"] = (Concepts.WellboreInclination, Concepts.ErrorApplicabilityEnd, null),
            ["ErrorInitializationInclination"] = (Concepts.WellboreInclination, Concepts.ErrorInitializationInclination, null)
        };

    public static JsonObject Metadata(string binding)
    {
        if (!Bindings.TryGetValue(binding, out var value))
            throw new InvalidOperationException($"Unknown reviewed SurveyInstrument semantic binding '{binding}'.");
        var catalogue = Catalogue.Default;
        var definition = catalogue.Get(value.Concept);
        var result = new JsonObject
        {
            ["catalogue"] = catalogue.Document.Id,
            ["catalogueVersion"] = catalogue.Document.Version,
            ["concept"] = value.Concept,
            ["curationStatus"] = definition.Status.ToString(),
            ["assertionSource"] = "provider-binding-registry",
            ["requiredContext"] = new JsonArray(catalogue.RequiredContext(value.Concept).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray())
        };
        if (value.Role != null) result["role"] = value.Role;
        if (value.Reference != null) result["reference"] = value.Reference;
        if (catalogue.SiUnit(value.Concept) is string unit) result["siUnit"] = unit;
        if (catalogue.Quantity(value.Concept) is QuantityIdentity quantity)
        {
            result["physicalQuantityStatus"] = "resolved";
            result["physicalQuantity"] = new JsonObject
            {
                ["catalogue"] = quantity.Catalogue, ["id"] = quantity.Id.ToString(),
                ["name"] = quantity.Name, ["siUnitName"] = quantity.SiUnitName
            };
        }
        return result;
    }
}
