using System.Reflection;

namespace NORCE.Drilling.SurveyInstrument.WebApp;

public static class ExternalRazorAssemblies
{
    public static IReadOnlyList<Assembly> All { get; } =
    [
        typeof(NORCE.Drilling.SurveyInstrument.WebPages.SurveyInstrumentMain).Assembly,
    ];
}
