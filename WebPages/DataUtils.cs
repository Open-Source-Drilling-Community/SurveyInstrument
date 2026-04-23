namespace NORCE.Drilling.SurveyInstrument.WebPages;

public static class DataUtils
{
    public static string DEFAULT_NAME_SurveyInstrument = "Default SurveyInstrument Name";
    public static string DEFAULT_DESCR_SurveyInstrument = "Default SurveyInstrument Description";

    public static class UnitAndReferenceParameters
    {
        public static string? UnitSystemName { get; set; } = "Metric";
    }

    public static void UpdateUnitSystemName(string val)
    {
        UnitAndReferenceParameters.UnitSystemName = val;
    }

    public static readonly string SurveyInstrumentNameLabel = "SurveyInstrument name";
    public static readonly string SurveyInstrumentDescrLabel = "SurveyInstrument description";
    public static readonly string SurveyInstrumentModelTypeLabel = "Model type";
    public static readonly string SurveyInstrumentErrorSourceListLabel = "Error sources";
}
