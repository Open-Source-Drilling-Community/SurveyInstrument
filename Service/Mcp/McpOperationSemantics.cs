using System;
using System.Text.Json.Nodes;
using OSDC.DotnetLibraries.Drilling.SemanticCatalogue;

namespace OSDC.Drilling.SurveyInstrument.Service.Mcp;

internal static class McpOperationSemantics
{
    public static JsonNode Apply(string name, JsonNode schema)
    {
        string concept = name.StartsWith("error_source_", StringComparison.Ordinal) ? Concepts.SurveyErrorSource
            : name.StartsWith("survey_instrument_feature_category_", StringComparison.Ordinal) ? Concepts.FeatureCategory
            : name.StartsWith("survey_instrument_identity_", StringComparison.Ordinal) ? Concepts.IdentityDefinition : Concepts.SurveyInstrument;
        schema[SemanticMetadata.ExtensionName] = SemanticMetadata.Create(concept, Classify(name), assertionSource: "provider-mcp-operation");
        return schema;
    }
    private static string Classify(string name) =>
        name.Contains("validate", StringComparison.Ordinal) || name.Contains("audit", StringComparison.Ordinal) || name.Contains("check", StringComparison.Ordinal) ? Concepts.StatelessEvaluation
        : name.Contains("batch_export", StringComparison.Ordinal) || name.Contains("get_all", StringComparison.Ordinal) ? Concepts.ResourceCollectionRetrieval
        : name.EndsWith("_get_by_id", StringComparison.Ordinal) ? Concepts.ResourceRetrieval
        : name.Contains("batch_restore", StringComparison.Ordinal) ? Concepts.ResourceOperation
        : name.EndsWith("_create", StringComparison.Ordinal) ? Concepts.ResourceCreation
        : name.EndsWith("_update_by_id", StringComparison.Ordinal) ? Concepts.ResourceReplacement
        : name.Contains("_patch", StringComparison.Ordinal) || name.Contains("_mutate", StringComparison.Ordinal) ? Concepts.ResourcePartialUpdate
        : name.Contains("_delete", StringComparison.Ordinal) ? Concepts.ResourceDeletion : Concepts.ResourceOperation;
}
