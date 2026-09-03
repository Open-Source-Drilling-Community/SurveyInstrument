using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using OSDC.DotnetLibraries.Drilling.Surveying;

namespace OSDC.Drilling.SurveyInstrument.Service.Mcp.Tools;

internal static class McpToolArgumentHelpers
{
    private static readonly HashSet<string> ProtectedSurveyInstrumentFields =
        new(StringComparer.Ordinal) { "MetaInfo", "CreationDate", "LastModificationDate" };

    public static IReadOnlySet<string> SurveyInstrumentPatchFields { get; } = new HashSet<string>(
        new[]
        {
            "Name", "Description", "SurveyInstrumentIdentityAssignments", "SurveyInstrumentFeatureAssignments",
            "ModelType", "ErrorSourceList", "Dip", "Declination", "Gravity", "BField", "Convergence", "Latitude",
            "EarthRotRate", "CantAngle", "GyroRunningSpeed", "ExtRefInitInc", "GyroSwitching", "GyroMinDist",
            "GyroNoiseRed", "UseRelDepthError", "RelDepthError", "UseMisalignment", "Misalignment",
            "UseTrueInclination", "TrueInclination", "UseReferenceError", "ReferenceError", "UseDrillStringMag",
            "DrillStringMag", "UseGyroCompassError", "GyroCompassError"
        }, StringComparer.Ordinal);

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
        "surveyInstrument", SurveyInstrumentSchema(enforceModelFamily: true),
        "Complete survey-instrument representation. JSON property names are case-sensitive and use PascalCase.",
        includeId, "Identifier of the persisted survey instrument. It must equal surveyInstrument.MetaInfo.ID.",
        includeId);

    public static JsonObject CreateSurveyInstrumentDeleteSchema() => CreateConcurrencySchema(
        "Identifier of the persisted survey instrument to delete.");

    public static JsonObject CreateSurveyInstrumentPatchSchema()
    {
        JsonObject fullProperties = (JsonObject)SurveyInstrumentSchema(enforceModelFamily: true)["properties"]!;
        JsonObject patchProperties = new();
        foreach ((string name, JsonNode? propertySchema) in fullProperties)
        {
            if (!ProtectedSurveyInstrumentFields.Contains(name))
            {
                patchProperties[name] = propertySchema?.DeepClone();
            }
        }

        JsonObject schema = CreateConcurrencySchema("Identifier of the persisted survey instrument to patch.");
        ((JsonObject)schema["properties"]!)["patch"] = new JsonObject
        {
            ["type"] = "object",
            ["description"] = "Top-level JSON Merge Patch fields. Omitted fields are retained; arrays are replaced as a whole; null clears only nullable fields. MetaInfo, CreationDate, and LastModificationDate are server-managed and cannot be patched.",
            ["properties"] = patchProperties,
            ["minProperties"] = 1,
            ["additionalProperties"] = false
        };
        ((JsonArray)schema["required"]!).Add("patch");
        return schema;
    }

    public static JsonObject CreateErrorSourceSnapshotMutationSchema()
    {
        JsonObject schema = CreateConcurrencySchema("Identifier of the persisted survey instrument whose ErrorSourceList should change.");
        JsonObject properties = (JsonObject)schema["properties"]!;
        properties["operation"] = EnumSchema("Single snapshot operation.", "add", "replace", "remove");
        properties["errorSource"] = ErrorSourceSchema();
        properties["errorSourceId"] = StringSchema("UUID of the embedded snapshot to remove.", "uuid");
        ((JsonArray)schema["required"]!).Add("operation");
        schema["oneOf"] = new JsonArray(
            new JsonObject
            {
                ["properties"] = new JsonObject { ["operation"] = new JsonObject { ["const"] = "add" } },
                ["required"] = new JsonArray("errorSource"), ["not"] = new JsonObject { ["required"] = new JsonArray("errorSourceId") }
            },
            new JsonObject
            {
                ["properties"] = new JsonObject { ["operation"] = new JsonObject { ["const"] = "replace" } },
                ["required"] = new JsonArray("errorSource"), ["not"] = new JsonObject { ["required"] = new JsonArray("errorSourceId") }
            },
            new JsonObject
            {
                ["properties"] = new JsonObject { ["operation"] = new JsonObject { ["const"] = "remove" } },
                ["required"] = new JsonArray("errorSourceId"), ["not"] = new JsonObject { ["required"] = new JsonArray("errorSource") }
            });
        return schema;
    }

    public static JsonObject CreateErrorSourceSchema(bool includeId = false, bool includeExpected = false)
    {
        JsonObject schema = CreateBodySchema(
            "errorSource", ErrorSourceSchema(),
            "Complete error-source representation. JSON property names are case-sensitive and use PascalCase.",
            includeId, "Identifier of the persisted error source. It must equal errorSource.MetaInfo.ID.");
        if (includeExpected)
        {
            ((JsonObject)schema["properties"]!)["expectedVersionToken"] = VersionTokenSchema();
            ((JsonArray)schema["required"]!).Add("expectedVersionToken");
        }
        return schema;
    }

    public static JsonObject CreateErrorSourceDeleteSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["id"] = StringSchema("UUID of the independently stored error-source template to delete.", "uuid"),
            ["expectedVersionToken"] = VersionTokenSchema()
        },
        ["required"] = new JsonArray("id", "expectedVersionToken"), ["additionalProperties"] = false
    };

    public static JsonObject CreateCatalogSchema(string bodyName, bool feature, bool includeId = false,
        bool includeExpected = false)
    {
        JsonObject body = feature ? FeatureCategorySchema() : IdentitySchema();
        JsonObject properties = new() { [bodyName] = body };
        JsonArray required = new(bodyName);
        if (includeId)
        {
            properties["id"] = StringSchema("Catalog definition UUID.", "uuid");
            required.Add("id");
        }
        if (includeExpected)
        {
            properties["expectedModifiedUtc"] = StringSchema("Exact LastModificationDate returned by the latest read.", "date-time");
            required.Add("expectedModifiedUtc");
        }
        return new JsonObject
        {
            ["type"] = "object", ["properties"] = properties, ["required"] = required,
            ["additionalProperties"] = false
        };
    }

    public static JsonObject CreateCatalogDeleteSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["id"] = StringSchema("Catalog definition UUID.", "uuid"),
            ["expectedModifiedUtc"] = StringSchema("Exact LastModificationDate returned by the latest read.", "date-time")
        },
        ["required"] = new JsonArray("id", "expectedModifiedUtc"),
        ["additionalProperties"] = false
    };

    public static JsonObject CreateBatchExportSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["request"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["Scope"] = EnumSchema("Export all instruments or an explicit ordered selection.", "All", "Selected"),
                    ["SurveyInstrumentIDs"] = NullableArray(StringSchema("Survey-instrument UUID.", "uuid"), "Required only for Selected scope; UUIDs must be non-empty and unique.")
                },
                ["required"] = new JsonArray("Scope"), ["additionalProperties"] = false
            }
        },
        ["required"] = new JsonArray("request"), ["additionalProperties"] = false
    };

    public static JsonObject CreateBatchRestoreSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["request"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["ConflictPolicy"] = EnumSchema("Fail atomically if an instrument UUID exists, or replace existing instruments.", "FailIfExists", "ReplaceExisting"),
                    ["Document"] = BatchDocumentSchema(enforceModelFamily: true)
                },
                ["required"] = new JsonArray("ConflictPolicy", "Document"), ["additionalProperties"] = false
            }
        },
        ["required"] = new JsonArray("request"), ["additionalProperties"] = false
    };

    public static JsonObject CreateCatalogReferenceAuditSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["offset"] = new JsonObject
            {
                ["type"] = "integer", ["minimum"] = 0, ["default"] = 0,
                ["description"] = "Zero-based offset into the deterministic UUID-ordered instrument list."
            },
            ["limit"] = new JsonObject
            {
                ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 100, ["default"] = 50,
                ["description"] = "Maximum number of instruments to validate in this page."
            }
        },
        ["additionalProperties"] = false
    };

    public static JsonObject CreateIdsOutputSchema() => SuccessEnvelope(new JsonObject
    {
        ["type"] = "array",
        ["items"] = StringSchema("Resource UUID.", "uuid")
    });

    public static JsonObject CreateMetaInfoListOutputSchema() => SuccessEnvelope(new JsonObject
    {
        ["type"] = "array",
        ["items"] = MetaInfoSchema("Resource metadata.")
    });

    public static JsonObject CreateSurveyInstrumentOutputSchema() => SuccessEnvelope(SurveyInstrumentSchema());

    public static JsonObject CreateSurveyInstrumentListOutputSchema() => SuccessEnvelope(new JsonObject
    {
        ["type"] = "array", ["items"] = SurveyInstrumentSchema()
    });

    public static JsonObject CreateSurveyInstrumentLightListOutputSchema() => SuccessEnvelope(new JsonObject
    {
        ["type"] = "array",
        ["items"] = new JsonObject
        {
            ["type"] = "object",
            ["description"] = "Lightweight survey-instrument discovery record.",
            ["properties"] = new JsonObject
            {
                ["MetaInfo"] = MetaInfoSchema("Resource metadata."),
                ["Name"] = NullableString("Survey-instrument name."),
                ["Description"] = NullableString("Survey-instrument description."),
                ["CreationDate"] = NullableDateTime("Creation timestamp."),
                ["LastModificationDate"] = NullableDateTime("Last-modification timestamp.")
            },
            ["required"] = new JsonArray("MetaInfo"),
            ["additionalProperties"] = false
        }
    });

    public static JsonObject CreateErrorSourceOutputSchema()
    {
        JsonObject envelope = SuccessEnvelope(ErrorSourceSchema());
        ((JsonObject)envelope["properties"]!)["versionToken"] = VersionTokenSchema();
        ((JsonArray)envelope["required"]!).Add("versionToken");
        return envelope;
    }

    public static JsonObject CreateErrorSourceListOutputSchema() => SuccessEnvelope(new JsonObject
    {
        ["type"] = "array", ["items"] = ErrorSourceSchema()
    });

    public static JsonObject CreateIdentityOutputSchema() => SuccessEnvelope(IdentitySchema());

    public static JsonObject CreateIdentityListOutputSchema() => SuccessEnvelope(new JsonObject
    {
        ["type"] = "array", ["items"] = IdentitySchema()
    });

    public static JsonObject CreateFeatureCategoryOutputSchema() => SuccessEnvelope(FeatureCategorySchema());

    public static JsonObject CreateFeatureCategoryListOutputSchema() => SuccessEnvelope(new JsonObject
    {
        ["type"] = "array", ["items"] = FeatureCategorySchema()
    });

    public static JsonObject CreateGenericOutputSchema() => SuccessEnvelope(new JsonObject());

    public static JsonObject CreateBatchExportOutputSchema() => SuccessEnvelope(BatchDocumentSchema());

    public static JsonObject CreateBatchRestoreOutputSchema() => SuccessEnvelope(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["RestoredAtUtc"] = StringSchema("Restore completion timestamp.", "date-time"),
            ["CreatedCount"] = Integer("Number of created instruments."),
            ["ReplacedCount"] = Integer("Number of replaced instruments."),
            ["CreatedCatalogDefinitionCount"] = Integer("Number of missing catalog definitions created."),
            ["SurveyInstrumentIDs"] = new JsonObject { ["type"] = "array", ["items"] = StringSchema("Restored instrument UUID.", "uuid") }
        },
        ["required"] = new JsonArray("RestoredAtUtc", "CreatedCount", "ReplacedCount", "CreatedCatalogDefinitionCount", "SurveyInstrumentIDs"),
        ["additionalProperties"] = false
    });

    public static JsonObject CreateErrorSourceDriftOutputSchema() => SuccessEnvelope(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["SurveyInstrumentID"] = StringSchema("Survey-instrument UUID.", "uuid"),
            ["HasDrift"] = Boolean("True when any embedded snapshot differs from, or has no match in, the template catalog."),
            ["Results"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["ErrorSourceID"] = StringSchema("Embedded snapshot/template UUID.", "uuid"),
                        ["Status"] = EnumSchema("Snapshot comparison result.", "in_sync", "drifted", "catalog_missing")
                    },
                    ["required"] = new JsonArray("ErrorSourceID", "Status"), ["additionalProperties"] = false
                }
            }
        },
        ["required"] = new JsonArray("SurveyInstrumentID", "HasDrift", "Results"), ["additionalProperties"] = false
    });

    public static JsonObject CreateCatalogReferenceValidationOutputSchema() =>
        SuccessEnvelope(CatalogReferenceValidationSchema());

    public static JsonObject CreateCatalogReferenceAuditOutputSchema() => SuccessEnvelope(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["Offset"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
            ["Limit"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 100 },
            ["CheckedCount"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
            ["ValidCount"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
            ["InvalidCount"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
            ["HasMore"] = new JsonObject { ["type"] = "boolean" },
            ["Results"] = new JsonObject { ["type"] = "array", ["items"] = CatalogReferenceValidationSchema() }
        },
        ["required"] = new JsonArray("Offset", "Limit", "CheckedCount", "ValidCount", "InvalidCount", "HasMore", "Results"),
        ["additionalProperties"] = false
    });

    public static JsonObject CreateErrorSourceUpdateImpactOutputSchema() => SuccessEnvelope(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["ErrorSourceID"] = StringSchema("Updated error-source template UUID.", "uuid"),
            ["VersionToken"] = VersionTokenSchema(),
            ["AffectedSnapshotCount"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
            ["AffectedSurveyInstrumentIDs"] = new JsonObject
            {
                ["type"] = "array", ["items"] = StringSchema("Survey-instrument UUID containing a frozen snapshot.", "uuid")
            },
            ["Warning"] = NullableString("Non-fatal warning that embedded snapshots were deliberately not propagated.")
        },
        ["required"] = new JsonArray("ErrorSourceID", "VersionToken", "AffectedSnapshotCount", "AffectedSurveyInstrumentIDs", "Warning"),
        ["additionalProperties"] = false
    });

    private static JsonObject CatalogReferenceValidationSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["SurveyInstrumentID"] = StringSchema("Validated survey-instrument UUID.", "uuid"),
            ["Status"] = EnumSchema("Whether every identity, feature-category, and feature-option assignment resolves in the local catalogs.", "valid", "invalid"),
            ["Issues"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["Path"] = new JsonObject { ["type"] = "string" },
                        ["Code"] = EnumSchema("Bounded catalog-integrity failure code.", "identity_missing", "feature_category_missing", "feature_option_missing"),
                        ["ReferencedID"] = StringSchema("Missing catalog UUID.", "uuid")
                    },
                    ["required"] = new JsonArray("Path", "Code", "ReferencedID"), ["additionalProperties"] = false
                }
            }
        },
        ["required"] = new JsonArray("SurveyInstrumentID", "Status", "Issues"), ["additionalProperties"] = false
    };

    private static JsonObject BatchDocumentSchema(bool enforceModelFamily = false) => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["FormatIdentifier"] = new JsonObject { ["type"] = "string", ["const"] = "OSDC.Drilling.SurveyInstrument.BatchExport" },
            ["SchemaVersion"] = new JsonObject { ["type"] = "integer", ["const"] = 1 },
            ["ExportedAtUtc"] = StringSchema("UTC export timestamp.", "date-time"),
            ["CatalogDependencies"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["ErrorSourceTemplates"] = new JsonObject { ["type"] = "array", ["items"] = ErrorSourceSchema() },
                    ["Identities"] = new JsonObject { ["type"] = "array", ["items"] = IdentitySchema() },
                    ["FeatureCategories"] = new JsonObject { ["type"] = "array", ["items"] = FeatureCategorySchema() }
                },
                ["required"] = new JsonArray("ErrorSourceTemplates", "Identities", "FeatureCategories"), ["additionalProperties"] = false
            },
            ["SurveyInstruments"] = new JsonObject { ["type"] = "array", ["items"] = SurveyInstrumentSchema(enforceModelFamily) }
        },
        ["required"] = new JsonArray("FormatIdentifier", "SchemaVersion", "ExportedAtUtc", "CatalogDependencies", "SurveyInstruments"),
        ["additionalProperties"] = false
    };

    private static JsonObject SuccessEnvelope(JsonObject data) => new()
    {
        ["type"] = "object",
        ["description"] = "Successful MCP tool response envelope.",
        ["properties"] = new JsonObject
        {
            ["status"] = new JsonObject { ["type"] = "integer", ["minimum"] = 200, ["maximum"] = 299 },
            ["data"] = data
        },
        ["required"] = new JsonArray("status"),
        ["additionalProperties"] = false
    };

    private static JsonObject CreateBodySchema(string key, JsonObject body, string description, bool includeId,
        string idDescription, bool includeExpected = false)
    {
        body["description"] = description;
        var properties = new JsonObject { [key] = body };
        var required = new JsonArray(key);
        if (includeId)
        {
            properties["id"] = StringSchema(idDescription, "uuid");
            required.Add("id");
        }
        if (includeExpected)
        {
            properties["expectedModifiedUtc"] = StringSchema(
                "Exact LastModificationDate returned by the latest read. A stale value is rejected with stale_write.",
                "date-time");
            required.Add("expectedModifiedUtc");
        }
        return new JsonObject
        {
            ["type"] = "object", ["properties"] = properties, ["required"] = required, ["additionalProperties"] = false
        };
    }

    private static JsonObject SurveyInstrumentSchema(bool enforceModelFamily = false)
    {
        JsonObject schema = new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
            ["MetaInfo"] = MetaInfoSchema("Resource metadata. MetaInfo.ID is supplied by the caller and is the persistent survey-instrument identifier."),
            ["Name"] = NullableString("Human-readable instrument or error-model name."),
            ["Description"] = NullableString("Human-readable explanation of the instrument, model revision, or intended use."),
            ["CreationDate"] = NullableDateTime("Creation timestamp in ISO 8601 format. Use a UTC offset where possible."),
            ["LastModificationDate"] = NullableDateTime("Last-modification timestamp in ISO 8601 format. Update it when changing the record."),
            ["SurveyInstrumentIdentityAssignments"] = NullableArray(IdentityAssignmentSchema(), "Identity values assigned to this instrument."),
            ["SurveyInstrumentFeatureAssignments"] = NullableArray(FeatureAssignmentSchema(), "Feature options assigned to this instrument."),
            ["ModelType"] = EnumSchema("Discriminator for the survey error-model family. Consult the oneOf branches on this object for family-specific field guidance.", "MWD_WolffDeWardt", "Gyro_WolffDeWardt", "MWD_ISCWSA", "Gyro_ISCWSA"),
            ["ErrorSourceList"] = NullableArray(ErrorSourceSchema(), "Authoritative error-source snapshots embedded in this instrument, primarily for ISCWSA models. The standalone error_source catalog is a template library: copying a catalog item preserves its MetaInfo.ID as provenance, but later catalog updates do not propagate into this array. Replace a snapshot explicitly to refresh it."),
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
            ["required"] = new JsonArray("MetaInfo", "ModelType"),
            ["additionalProperties"] = false
        };
        if (enforceModelFamily) schema["oneOf"] = ModelTypeBranches();
        return schema;
    }

    private static readonly string[] FamilySpecificFields =
    [
        "ErrorSourceList", "Dip", "Declination", "Gravity", "BField", "Latitude", "EarthRotRate", "CantAngle",
        "GyroRunningSpeed", "ExtRefInitInc", "GyroSwitching", "GyroMinDist", "GyroNoiseRed",
        "UseRelDepthError", "RelDepthError", "UseMisalignment", "Misalignment", "UseTrueInclination",
        "TrueInclination", "UseReferenceError", "ReferenceError", "UseDrillStringMag", "DrillStringMag",
        "UseGyroCompassError", "GyroCompassError"
    ];

    private static JsonArray ModelTypeBranches() => new(
        ModelTypeBranch("MWD_WolffDeWardt", "MWD Wolff-DeWardt model. The Use*/value pairs are the active error parameters; DrillStringMag is MWD-specific.",
            "Dip", "Declination", "Gravity", "BField", "UseRelDepthError", "RelDepthError", "UseMisalignment", "Misalignment",
            "UseTrueInclination", "TrueInclination", "UseReferenceError", "ReferenceError", "UseDrillStringMag", "DrillStringMag"),
        ModelTypeBranch("Gyro_WolffDeWardt", "Gyro Wolff-DeWardt model. The Use*/value pairs are the active error parameters; GyroCompassError is gyro-specific.",
            "Latitude", "EarthRotRate", "CantAngle", "GyroRunningSpeed", "ExtRefInitInc", "GyroSwitching", "GyroMinDist", "GyroNoiseRed",
            "UseRelDepthError", "RelDepthError", "UseMisalignment", "Misalignment", "UseTrueInclination", "TrueInclination",
            "UseReferenceError", "ReferenceError", "UseGyroCompassError", "GyroCompassError"),
        ModelTypeBranch("MWD_ISCWSA", "MWD ISCWSA model. ErrorSourceList plus geomagnetic/gravity context define the model.",
            "ErrorSourceList", "Dip", "Declination", "Gravity", "BField"),
        ModelTypeBranch("Gyro_ISCWSA", "Gyro ISCWSA model. ErrorSourceList plus gyro and Earth-rotation context define the model.",
            "ErrorSourceList", "Latitude", "EarthRotRate", "CantAngle", "GyroRunningSpeed", "ExtRefInitInc", "GyroSwitching", "GyroMinDist", "GyroNoiseRed"));

    private static JsonObject ModelTypeBranch(string modelType, string description, params string[] allowed)
    {
        JsonArray forbiddenPresence = new();
        HashSet<string> allowedFields = allowed.ToHashSet(StringComparer.Ordinal);
        foreach (string property in FamilySpecificFields.Where(field => !allowedFields.Contains(field)))
        {
            forbiddenPresence.Add(new JsonObject { ["required"] = new JsonArray(property) });
        }
        return new JsonObject
        {
            ["title"] = modelType,
            ["description"] = description + " Properties forbidden by this branch are rejected rather than ignored.",
            ["properties"] = new JsonObject { ["ModelType"] = new JsonObject { ["const"] = modelType } },
            ["required"] = new JsonArray("ModelType"),
            ["not"] = new JsonObject { ["anyOf"] = forbiddenPresence }
        };
    }

    private static JsonObject CreateConcurrencySchema(string idDescription) => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["id"] = StringSchema(idDescription, "uuid"),
            ["expectedModifiedUtc"] = StringSchema(
                "Exact LastModificationDate returned by the latest read. A stale value is rejected with stale_write.",
                "date-time")
        },
        ["required"] = new JsonArray("id", "expectedModifiedUtc"),
        ["additionalProperties"] = false
    };

    private static JsonObject IdentitySchema() => new()
    {
        ["type"] = "object",
        ["description"] = "User-managed survey-instrument identity definition.",
        ["properties"] = new JsonObject
        {
            ["MetaInfo"] = MetaInfoSchema("Catalog metadata containing the caller-generated definition UUID."),
            ["Name"] = NullableString("Identity category name."),
            ["CreationDate"] = NullableDateTime("Server-owned creation time."),
            ["LastModificationDate"] = NullableDateTime("Server-owned optimistic-concurrency token.")
        },
        ["required"] = new JsonArray("MetaInfo"), ["additionalProperties"] = false
    };

    private static JsonObject FeatureCategorySchema() => new()
    {
        ["type"] = "object",
        ["description"] = "User-managed survey-instrument feature category and options.",
        ["properties"] = new JsonObject
        {
            ["MetaInfo"] = MetaInfoSchema("Catalog metadata containing the caller-generated category UUID."),
            ["Name"] = NullableString("Feature category name."),
            ["IsExclusive"] = Boolean("Whether overlapping assignments in this category are forbidden."),
            ["HasValidityPeriod"] = Boolean("Whether assignments may carry FromDate and ToDate."),
            ["Options"] = new JsonObject { ["type"] = "array", ["items"] = FeatureOptionSchema() },
            ["CreationDate"] = NullableDateTime("Server-owned creation time."),
            ["LastModificationDate"] = NullableDateTime("Server-owned optimistic-concurrency token.")
        },
        ["required"] = new JsonArray("MetaInfo", "IsExclusive", "HasValidityPeriod", "Options"),
        ["additionalProperties"] = false
    };

    private static JsonObject FeatureOptionSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["ID"] = StringSchema("Stable feature-option UUID.", "uuid"),
            ["Name"] = NullableString("Feature option name.")
        },
        ["required"] = new JsonArray("ID"), ["additionalProperties"] = false
    };

    private static JsonObject IdentityAssignmentSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["ID"] = StringSchema("Caller-generated assignment UUID.", "uuid"),
            ["IdentityID"] = StringSchema("UUID of an existing identity definition.", "uuid"),
            ["Value"] = NullableString("Instrument-specific identity value.")
        },
        ["required"] = new JsonArray("ID", "IdentityID"), ["additionalProperties"] = false
    };

    private static JsonObject FeatureAssignmentSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["ID"] = StringSchema("Caller-generated assignment UUID.", "uuid"),
            ["FeatureCategoryID"] = StringSchema("UUID of an existing feature category.", "uuid"),
            ["FeatureOptionID"] = StringSchema("UUID of an option belonging to that category.", "uuid"),
            ["FromDate"] = NullableDateTime("Optional validity start, allowed only for validity-aware categories."),
            ["ToDate"] = NullableDateTime("Optional validity end; it must not precede FromDate.")
        },
        ["required"] = new JsonArray("ID", "FeatureCategoryID", "FeatureOptionID"),
        ["additionalProperties"] = false
    };

    private static JsonObject ErrorSourceSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["MetaInfo"] = MetaInfoSchema("Resource metadata. MetaInfo.ID is supplied by the caller and is the persistent error-source identifier."),
            ["ErrorCode"] = EnumSchema("Finite ISCWSA/survey-model error-code vocabulary. Use the exact enum spelling.", Enum.GetNames<ErrorCode>()),
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

    private static JsonObject VersionTokenSchema() => new()
    {
        ["type"] = "string", ["pattern"] = "^[0-9a-f]{64}$",
        ["description"] = "Exact lowercase SHA-256 content token returned by error_source_get_by_id. A stale token is rejected with stale_write."
    };
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
