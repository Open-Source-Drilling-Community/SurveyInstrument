using System.Collections.Generic;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using OSDC.DotnetLibraries.Drilling.Surveying;
using OSDC.DotnetLibraries.Drilling.SemanticCatalogue;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OSDC.Drilling.SurveyInstrument.Service;

/// <summary>Adds the ISCWSA semantics that cannot be inferred from CLR property names.</summary>
internal sealed class SurveyInstrumentSemanticSchemaFilter : ISchemaFilter
{
    private const string PropagationDescription =
        "Mutually exclusive ISCWSA Revision 5 correlation mode: Random (R) is independent between survey stations; " +
        "Systematic (S) is correlated between stations in the same survey leg but independent between legs; " +
        "WellByWell (W) is correlated across legs within the same well but independent between wells; Global (G) is " +
        "fully correlated across all survey stations, legs, and wells in the project or field. Null is reserved for " +
        "readable legacy records whose mode is derived from the " +
        "deprecated boolean flags.";

    private const string ErrorCodeDescription =
        "Closed ISCWSA/survey error-source vocabulary. Revision 5 axial-correction terms use MFIR and MFI_U/OS/OH/OI " +
        "for total magnetic-field uncertainty in tesla, and MDIR and MDI_U/OS/OH/OI for magnetic-dip uncertainty in " +
        "radians. R is random, U is Well-by-Well, and OS/OH/OI are Global crustal-omission terms for standard, " +
        "high-definition, and in-field referencing models. AMIL is axial magnetic interference in tesla; AMID and " +
        "the unsuffixed early OSDC axial terms are legacy read compatibility values and are rejected in new Revision 5 models.";

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (SemanticMetadata.For(context.Type) is { } typeMetadata)
            schema.Extensions[SemanticMetadata.ExtensionName] = OpenApiAnyFactory.CreateFromJson(typeMetadata.ToJsonString());
        foreach (var modelProperty in context.Type.GetProperties())
            if (schema.Properties.TryGetValue(modelProperty.Name, out var target) && SemanticMetadata.For(modelProperty) is { } propertyMetadata)
                target.Extensions[SemanticMetadata.ExtensionName] = OpenApiAnyFactory.CreateFromJson(propertyMetadata.ToJsonString());

        if (context.Type == typeof(ErrorCode))
        {
            schema.Description = ErrorCodeDescription;
            return;
        }

        if (context.Type == typeof(ErrorPropagationMode))
        {
            schema.Description = PropagationDescription;
            return;
        }

        if (context.Type == typeof(ErrorSource))
        {
            schema.Description = "One ISCWSA survey error source. Magnitude is a finite, nonnegative one-sigma standard uncertainty in the SI unit identified by MagnitudeQuantity.";
            schema.Extensions[SemanticMetadata.ExtensionName] = OpenApiAnyFactory.CreateFromJson(SurveyInstrumentProviderSemantics.Metadata("SurveyErrorSource").ToJsonString());
            Describe(schema, "ErrorCode", ErrorCodeDescription, "SurveyErrorSourceCode");
            Describe(schema, "Index", "Implementation ordering field only; it has no independent physical meaning.", "ErrorSourceOrderingIndex");
            Describe(schema, "PropagationMode", PropagationDescription, "ErrorPropagationMode");
            if (schema.Properties.TryGetValue("PropagationMode", out OpenApiSchema? propagation))
            {
                propagation.Nullable = true;
            }
            Describe(schema, "IsSystematic", "Deprecated legacy compatibility flag. For current data use the single PropagationMode value.");
            Describe(schema, "IsRandom", "Deprecated legacy compatibility flag. For current data use the single PropagationMode value.");
            Describe(schema, "IsGlobal", "Deprecated legacy compatibility flag. Global is a distinct PropagationMode, not a flag independent of Random or Systematic.");
            Describe(schema, "IsContinuous", "Gyroscopic-tool operating-mode flag. Continuous and Stationary are mutually exclusive; both may be false when the distinction does not apply.");
            Describe(schema, "IsStationary", "Gyroscopic-tool operating-mode flag. Stationary and Continuous are mutually exclusive; both may be false when the distinction does not apply.");
            Describe(schema, "KOperatorImposed", "Opaque legacy compatibility flag retained until its mathematical behavior and operating boundaries are formally defined.", "KOperatorImposed");
            Describe(schema, "Magnitude", "Finite, nonnegative one-sigma standard uncertainty in the SI unit required by ErrorCode.", "SurveyErrorMagnitude");
            Describe(schema, "MagnitudeQuantity", "Closed UnitConversion physical-quantity identifier defining Magnitude's dimension and canonical SI unit.", "ErrorMagnitudeQuantityIdentifier");
            Describe(schema, "StartInclination", "Start of the applicable inclination interval in radians.", "ErrorApplicabilityStart", "rad");
            Describe(schema, "EndInclination", "End of the applicable inclination interval in radians.", "ErrorApplicabilityEnd", "rad");
            Describe(schema, "InitInclination", "Initial inclination used by the error source in radians when required by the model.", "ErrorInitializationInclination", "rad");
            AddOperatingModeExclusion(schema);
            return;
        }

        if (typeof(OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument).IsAssignableFrom(context.Type))
        {
            schema.Description = "Survey-instrument error model with canonical SI values and, for ISCWSA families, authoritative embedded error-source snapshots.";
            schema.Extensions[SemanticMetadata.ExtensionName] = OpenApiAnyFactory.CreateFromJson(SurveyInstrumentProviderSemantics.Metadata("SurveyInstrument").ToJsonString());
            Describe(schema, "Dip", "Geomagnetic dip (inclination) in radians.", "MagneticDip", "rad");
            Describe(schema, "Declination", "Geomagnetic declination, positive east of true north, in radians.", "MagneticDeclination", "rad");
            Describe(schema, "Gravity", "Local gravitational acceleration in metres per second squared.", "GravityAcceleration", "m/s2");
            Describe(schema, "BField", "Local total geomagnetic flux density in tesla.", "EarthMagneticFluxDensity", "T");
            Describe(schema, "Convergence", "Grid convergence angle in radians.", "GridConvergence", "rad");
            Describe(schema, "Latitude", "Geodetic latitude in radians.", "Latitude", "rad");
            Describe(schema, "EarthRotRate", "Earth angular velocity in radians per second.", "EarthAngularVelocity", "rad/s");
            Describe(schema, "CantAngle", "Planar angle in radians relative to the orthogonal body reference frame's transverse axes, perpendicular to the along-hole tool z-axis. Its sign remains positive while tool inclination is less than or equal to 90 degrees.", "SurveyInstrumentCantAngle", "rad");
            Describe(schema, "GyroRunningSpeed", "Optional gyroscope angular velocity in radians per second.", "SurveyToolRunningSpeed", "rad/s");
            Describe(schema, "GyroSwitching", "Optional dimensionless gyro switching parameter.", "GyroSwitchingParameter", "1");
            Describe(schema, "GyroMinDist", "Optional minimum distance between gyro initializations in metres.", "GyroReinitializationDistance", "m");
            Describe(schema, "GyroNoiseRed", "Optional dimensionless gyro noise-reduction factor at initialization.", "GyroNoiseReductionFactor", "1");
        }
    }

    private static void Describe(OpenApiSchema schema, string propertyName, string description,
        string? semantic = null, string? unit = null)
    {
        if (!schema.Properties.TryGetValue(propertyName, out OpenApiSchema? property)) return;
        property.Description = description;
        if (semantic != null) property.Extensions[SemanticMetadata.ExtensionName] = OpenApiAnyFactory.CreateFromJson(SurveyInstrumentProviderSemantics.Metadata(semantic).ToJsonString());
        if (unit != null) property.Extensions["x-si-unit"] = new OpenApiString(unit);
    }

    private static void AddOperatingModeExclusion(OpenApiSchema schema)
    {
        schema.AllOf ??= [];
        schema.AllOf.Add(new OpenApiSchema
        {
            Not = new OpenApiSchema
            {
                Required = new HashSet<string> { "IsContinuous", "IsStationary" },
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["IsContinuous"] = new() { Enum = [new OpenApiBoolean(true)] },
                    ["IsStationary"] = new() { Enum = [new OpenApiBoolean(true)] }
                }
            }
        });
    }
}
