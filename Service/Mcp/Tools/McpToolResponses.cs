using System.Text.Json.Nodes;

namespace OSDC.Drilling.SurveyInstrument.Service.Mcp.Tools;

internal static class McpToolResponses
{
    public static JsonNode CreateValidationError(string message)
    {
        return new JsonObject
        {
            ["status"] = 400,
            ["error"] = message
        };
    }

    public static JsonNode CreateConflict(string code, string message)
    {
        return new JsonObject
        {
            ["status"] = 409,
            ["message"] = message,
            ["data"] = new JsonObject { ["error"] = code }
        };
    }

}
