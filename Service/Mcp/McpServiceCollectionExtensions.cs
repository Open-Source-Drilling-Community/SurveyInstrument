using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace OSDC.Drilling.SurveyInstrument.Service.Mcp;

public static class McpServiceCollectionExtensions
{
    public static IServiceCollection AddLegacyMcpTool<TTool>(this IServiceCollection services)
        where TTool : class, IMcpTool
    {
        services.AddSingleton<TTool>();
        services.AddSingleton<IMcpTool>(sp => sp.GetRequiredService<TTool>());
        services.AddSingleton<McpServerTool>(sp =>
        {
            var tool = sp.GetRequiredService<TTool>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new LegacyMcpServerToolAdapter(tool, loggerFactory);
        });

        return services;
    }

    public static IServiceCollection AddLegacyMcpTool(
        this IServiceCollection services,
        string name,
        string description,
        JsonNode? inputSchema,
        Func<IServiceProvider, JsonObject?, CancellationToken, Task<JsonNode?>> invokeAsync)
    {
        services.AddSingleton<IMcpTool>(sp => new DelegateMcpTool(
            name, description, inputSchema ?? EmptyInputSchema(), InferOutputSchema(name), InferBehavior(name),
            (arguments, cancellationToken) => invokeAsync(sp, arguments, cancellationToken)));
        services.AddSingleton<McpServerTool>(sp => new LegacyMcpServerToolAdapter(
            sp.GetServices<IMcpTool>().Last(tool => tool.Name == name), sp.GetRequiredService<ILoggerFactory>()));
        return services;
    }

    private sealed class DelegateMcpTool : IMcpTool
    {
        private readonly Func<JsonObject?, CancellationToken, Task<JsonNode?>> _invokeAsync;

        public DelegateMcpTool(
            string name,
            string description,
            JsonNode inputSchema,
            JsonNode outputSchema,
            McpToolBehavior behavior,
            Func<JsonObject?, CancellationToken, Task<JsonNode?>> invokeAsync)
        {
            Name = name;
            Description = description;
            InputSchema = inputSchema;
            OutputSchema = outputSchema;
            Behavior = behavior;
            _invokeAsync = invokeAsync;
        }

        public string Name { get; }

        public string Description { get; }

        public McpToolBehavior Behavior { get; }

        public JsonNode InputSchema { get; }

        public JsonNode OutputSchema { get; }

        public Task<JsonNode?> InvokeAsync(JsonObject? arguments, CancellationToken cancellationToken)
        {
            JsonObject? properties = InputSchema["properties"] as JsonObject;
            string? unexpected = arguments?.Select(item => item.Key)
                .FirstOrDefault(key => properties == null || !properties.ContainsKey(key));
            return unexpected == null
                ? _invokeAsync(arguments, cancellationToken)
                : Task.FromResult<JsonNode?>(new JsonObject
                {
                    ["status"] = 400,
                    ["error"] = $"Unexpected argument '{unexpected}'."
                });
        }
    }

    private static McpToolBehavior InferBehavior(string name)
    {
        bool readOnly = name.Contains("_get_", StringComparison.Ordinal) ||
                        name.EndsWith("_get_all", StringComparison.Ordinal) ||
                        name.Contains("_check_", StringComparison.Ordinal) ||
                        name.Contains("_validate_", StringComparison.Ordinal) ||
                        name.Contains("_audit_", StringComparison.Ordinal) ||
                        name.EndsWith("_batch_export", StringComparison.Ordinal);
        bool destructive = name.Contains("_delete_", StringComparison.Ordinal) ||
                           name.EndsWith("_batch_restore", StringComparison.Ordinal);
        bool idempotent = readOnly || name.Contains("_update", StringComparison.Ordinal) ||
                          name.Contains("_patch_", StringComparison.Ordinal) ||
                          name == "survey_instrument_error_source_mutate" || destructive;
        string title = string.Join(' ', name.Split('_')
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
        return new McpToolBehavior(title, readOnly, destructive, idempotent);
    }

    private static JsonNode InferOutputSchema(string name)
    {
        if (name.EndsWith("_get_all_ids", StringComparison.Ordinal))
            return Tools.McpToolArgumentHelpers.CreateIdsOutputSchema();
        if (name.EndsWith("_get_all_meta_info", StringComparison.Ordinal))
            return Tools.McpToolArgumentHelpers.CreateMetaInfoListOutputSchema();
        if (name == "survey_instrument_get_by_id")
            return Tools.McpToolArgumentHelpers.CreateSurveyInstrumentOutputSchema();
        if (name == "survey_instrument_error_source_mutate")
            return Tools.McpToolArgumentHelpers.CreateSurveyInstrumentOutputSchema();
        if (name == "survey_instrument_get_all")
            return Tools.McpToolArgumentHelpers.CreateSurveyInstrumentListOutputSchema();
        if (name == "survey_instrument_get_all_light")
            return Tools.McpToolArgumentHelpers.CreateSurveyInstrumentLightListOutputSchema();
        if (name == "survey_instrument_check_error_source_drift")
            return Tools.McpToolArgumentHelpers.CreateErrorSourceDriftOutputSchema();
        if (name == "survey_instrument_validate_catalog_references")
            return Tools.McpToolArgumentHelpers.CreateCatalogReferenceValidationOutputSchema();
        if (name == "survey_instrument_audit_catalog_references")
            return Tools.McpToolArgumentHelpers.CreateCatalogReferenceAuditOutputSchema();
        if (name == "survey_instrument_batch_export")
            return Tools.McpToolArgumentHelpers.CreateBatchExportOutputSchema();
        if (name == "survey_instrument_batch_restore")
            return Tools.McpToolArgumentHelpers.CreateBatchRestoreOutputSchema();
        if (name == "error_source_get_by_id")
            return Tools.McpToolArgumentHelpers.CreateErrorSourceOutputSchema();
        if (name == "error_source_get_all")
            return Tools.McpToolArgumentHelpers.CreateErrorSourceListOutputSchema();
        if (name == "error_source_update_by_id")
            return Tools.McpToolArgumentHelpers.CreateErrorSourceUpdateImpactOutputSchema();
        if (name.StartsWith("survey_instrument_identity_", StringComparison.Ordinal))
        {
            if (name.EndsWith("_get_by_id", StringComparison.Ordinal))
                return Tools.McpToolArgumentHelpers.CreateIdentityOutputSchema();
            if (name.EndsWith("_get_all", StringComparison.Ordinal))
                return Tools.McpToolArgumentHelpers.CreateIdentityListOutputSchema();
        }
        if (name.StartsWith("survey_instrument_feature_category_", StringComparison.Ordinal))
        {
            if (name.EndsWith("_get_by_id", StringComparison.Ordinal))
                return Tools.McpToolArgumentHelpers.CreateFeatureCategoryOutputSchema();
            if (name.EndsWith("_get_all", StringComparison.Ordinal))
                return Tools.McpToolArgumentHelpers.CreateFeatureCategoryListOutputSchema();
        }
        return Tools.McpToolArgumentHelpers.CreateGenericOutputSchema();
    }

    private static JsonNode EmptyInputSchema() =>
        JsonNode.Parse("""{"type":"object","additionalProperties":false}""")!;
}
