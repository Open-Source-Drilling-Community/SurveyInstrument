using System.Reflection;

namespace OSDC.Drilling.SurveyInstrument.WebApp;

public static class ExternalRazorAssemblies
{
    public static IReadOnlyList<Assembly> All { get; } =
    [
        typeof(OSDC.Drilling.SurveyInstrument.WebPages.SurveyInstrumentMain).Assembly,
    ];
}
