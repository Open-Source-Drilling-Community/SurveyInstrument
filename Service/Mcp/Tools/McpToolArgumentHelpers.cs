using System;
using System.Text.Json.Nodes;

namespace NORCE.Drilling.SurveyInstrument.Service.Mcp.Tools;

internal static class McpToolArgumentHelpers
{
    public static JsonObject CreateEmptySchema() => new()
    {
        ["type"] = "object", ["properties"] = new JsonObject(), ["additionalProperties"] = false
    };

    public static JsonObject CreateGuidSchema(string key, string description) => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject { [key] = StringSchema(description, "uuid") },
        ["required"] = new JsonArray(key),
        ["additionalProperties"] = false
    };

    public static JsonObject CreateSurveyInstrumentSchema(bool includeId = false) => CreateBodySchema(
        "surveyInstrument", SurveyInstrumentSchema(),
        "Complete survey-instrument representation. JSON property names are case-sensitive and use PascalCase.",
        includeId, "Identifier of the persisted survey instrument. It must equal surveyInstrument.MetaInfo.ID.");

    public static JsonObject CreateErrorSourceSchema(bool includeId = false) => CreateBodySchema(
        "errorSource", ErrorSourceSchema(),
        "Complete error-source representation. JSON property names are case-sensitive and use PascalCase.",
        includeId, "Identifier of the persisted error source. It must equal errorSource.MetaInfo.ID.");

    private static JsonObject CreateBodySchema(string key, JsonObject body, string description, bool includeId, string idDescription)
    {
        body["description"] = description;
        var properties = new JsonObject { [key] = body };
        var required = new JsonArray(key);
        if (includeId)
        {
            properties["id"] = StringSchema(idDescription, "uuid");
            required.Add("id");
        }
        return new JsonObject
        {
            ["type"] = "object", ["properties"] = properties, ["required"] = required, ["additionalProperties"] = false
        };
    }

    private static JsonObject SurveyInstrumentSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["MetaInfo"] = MetaInfoSchema("Resource metadata. MetaInfo.ID is supplied by the caller and is the persistent survey-instrument identifier."),
            ["Name"] = NullableString("Human-readable instrument or error-model name."),
            ["Description"] = NullableString("Human-readable explanation of the instrument, model revision, or intended use."),
            ["CreationDate"] = NullableDateTime("Creation timestamp in ISO 8601 format. Use a UTC offset where possible."),
            ["LastModificationDate"] = NullableDateTime("Last-modification timestamp in ISO 8601 format. Update it when changing the record."),
            ["ModelType"] = EnumSchema("Survey error-model family. MWD values model measurement-while-drilling instruments; Gyro values model gyroscopic instruments.", "MWD_WolffDeWardt", "Gyro_WolffDeWardt", "MWD_ISCWSA", "Gyro_ISCWSA"),
            ["ErrorSourceList"] = NullableArray(ErrorSourceSchema(), "Full error-source definitions used by this instrument, primarily for ISCWSA models. These are embedded objects, not UUID references."),
            ["Dip"] = Number("Geomagnetic dip (inclination) in radians."),
            ["Declination"] = Number("Geomagnetic declination in radians."),
            ["Gravity"] = Number("Local gravitational acceleration in metres per second squared (m/s²)."),
            ["BField"] = Number("Local geomagnetic flux density in tesla (T)."),
            ["Convergence"] = Number("Grid convergence angle in radians."),
            ["Latitude"] = Number("Geodetic latitude in radians."),
            ["EarthRotRate"] = Number("Earth angular rotation rate in radians per second (rad/s)."),
            ["CantAngle"] = Number("Gyro cant angle in radians."),
            ["GyroRunningSpeed"] = NullableNumber("Optional gyro-model running-speed parameter. Supply the model's SI value."),
            ["ExtRefInitInc"] = NullableNumber("Optional external-reference initial inclination in radians."),
            ["GyroSwitching"] = NullableNumber("Optional dimensionless gyro switching parameter used by the selected gyro model."),
            ["GyroMinDist"] = NullableNumber("Optional minimum gyro distance in metres (m)."),
            ["GyroNoiseRed"] = NullableNumber("Optional dimensionless gyro noise-reduction factor."),
            ["UseRelDepthError"] = Boolean("Whether RelDepthError participates in the Wolff-DeWardt model."),
            ["RelDepthError"] = NullableNumber("Relative measured-depth error as a dimensionless proportion; for example, 0.001 means 0.1%."),
            ["UseMisalignment"] = Boolean("Whether Misalignment participates in the Wolff-DeWardt model."),
            ["Misalignment"] = NullableNumber("Instrument misalignment angle in radians."),
            ["UseTrueInclination"] = Boolean("Whether TrueInclination participates in the Wolff-DeWardt model."),
            ["TrueInclination"] = NullableNumber("True-inclination error angle in radians."),
            ["UseReferenceError"] = Boolean("Whether ReferenceError participates in the Wolff-DeWardt model."),
            ["ReferenceError"] = NullableNumber("Reference error angle in radians."),
            ["UseDrillStringMag"] = Boolean("Whether DrillStringMag participates in the MWD Wolff-DeWardt model."),
            ["DrillStringMag"] = NullableNumber("Drill-string magnetization error angle in radians."),
            ["UseGyroCompassError"] = Boolean("Whether GyroCompassError participates in the gyro Wolff-DeWardt model."),
            ["GyroCompassError"] = NullableNumber("Gyro-compass error angle in radians.")
        },
        ["required"] = new JsonArray("MetaInfo"), ["additionalProperties"] = false
    };

    private static JsonObject ErrorSourceSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["MetaInfo"] = MetaInfoSchema("Resource metadata. MetaInfo.ID is supplied by the caller and is the persistent error-source identifier."),
            ["ErrorCode"] = StringSchema("ISCWSA or survey-model error-code identifier, such as DRFR, DSFS, ABXY_TI1S, or XCLA. Use the exact ErrorCode enum spelling accepted by the service."),
            ["Description"] = NullableString("Human-readable explanation of the physical error source."),
            ["Index"] = Integer("Ordering or model index assigned to this error source."),
            ["IsSystematic"] = Boolean("Whether the source is treated as a systematic error."),
            ["IsRandom"] = Boolean("Whether the source is treated as a random error."),
            ["IsGlobal"] = Boolean("Whether the source is globally correlated rather than local to one survey station."),
            ["SingularIssues"] = Boolean("Whether the source requires special handling at singular orientations."),
            ["IsContinuous"] = Boolean("Whether the source acts continuously along the trajectory."),
            ["IsStationary"] = Boolean("Whether the source is stationary under the selected error model."),
            ["KOperatorImposed"] = Boolean("Whether the source uses an imposed K-operator treatment."),
            ["Magnitude"] = NullableNumber("Error magnitude expressed in the SI unit of the physical quantity named by MagnitudeQuantity. Do not supply a display-unit value."),
            ["MagnitudeQuantity"] = NullableString("UnitConversion physical-quantity name that defines Magnitude's dimension and SI unit, for example PlaneAngleDrilling, AccelerationDrilling, or ProportionSmall."),
            ["UseInclinationInterval"] = Boolean("Whether the source applies only over the inclination interval described by StartInclination and EndInclination."),
            ["StartInclination"] = NullableNumber("Start of the applicable inclination interval in radians."),
            ["EndInclination"] = NullableNumber("End of the applicable inclination interval in radians."),
            ["InitInclination"] = NullableNumber("Initial inclination used by this error source in radians, when required by the model.")
        },
        ["required"] = new JsonArray("MetaInfo"), ["additionalProperties"] = false
    };

    private static JsonObject MetaInfoSchema(string description) => new()
    {
        ["type"] = "object", ["description"] = description,
        ["properties"] = new JsonObject
        {
            ["ID"] = StringSchema("Non-empty UUID that identifies the resource. Generate this before create; the service does not assign it.", "uuid"),
            ["HttpHostName"] = NullableString("Optional host metadata maintained for compatibility with the shared resource model."),
            ["HttpHostBasePath"] = NullableString("Optional HTTP base-path metadata maintained for compatibility with the shared resource model."),
            ["HttpEndPoint"] = NullableString("Optional endpoint metadata maintained for compatibility with the shared resource model.")
        },
        ["required"] = new JsonArray("ID"), ["additionalProperties"] = false
    };

    private static JsonObject StringSchema(string description, string? format = null)
    {
        var schema = new JsonObject { ["type"] = "string", ["description"] = description };
        if (format is not null) schema["format"] = format;
        return schema;
    }
    private static JsonObject NullableString(string description) => Typed(new JsonArray("string", "null"), description);
    private static JsonObject NullableDateTime(string description) { var value = Typed(new JsonArray("string", "null"), description); value["format"] = "date-time"; return value; }
    private static JsonObject Number(string description) => Typed("number", description);
    private static JsonObject NullableNumber(string description) => Typed(new JsonArray("number", "null"), description);
    private static JsonObject Integer(string description) => Typed("integer", description);
    private static JsonObject Boolean(string description) => Typed("boolean", description);
    private static JsonObject Typed(JsonNode type, string description) => new() { ["type"] = type, ["description"] = description };
    private static JsonObject EnumSchema(string description, params string[] values)
    {
        var enumValues = new JsonArray(); foreach (string value in values) enumValues.Add(value);
        return new JsonObject { ["type"] = "string", ["description"] = description, ["enum"] = enumValues };
    }
    private static JsonObject NullableArray(JsonObject items, string description) => new()
    {
        ["type"] = new JsonArray("array", "null"), ["description"] = description, ["items"] = items
    };

    public static bool TryParseGuid(JsonObject? arguments, string key, out Guid value, out JsonNode? error)
    {
        value = Guid.Empty; error = null; var node = arguments?[key];
        if (node is null) { error = McpToolResponses.CreateValidationError($"Argument '{key}' is required."); return false; }
        if (!Guid.TryParse(node.ToString(), out value)) { error = McpToolResponses.CreateValidationError($"Argument '{key}' must be a valid UUID."); return false; }
        return true;
    }

    public static bool TryParseDouble(JsonObject? arguments, string key, out double value, out JsonNode? error)
    {
        value = 0d; error = null; var node = arguments?[key];
        if (node is null) { error = McpToolResponses.CreateValidationError($"Argument '{key}' is required."); return false; }
        try { value = node.GetValue<double>(); }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException) { error = McpToolResponses.CreateValidationError($"Argument '{key}' must be a number."); return false; }
        if (double.IsNaN(value) || double.IsInfinity(value)) { error = McpToolResponses.CreateValidationError($"Argument '{key}' must be a finite number."); return false; }
        return true;
    }
}
