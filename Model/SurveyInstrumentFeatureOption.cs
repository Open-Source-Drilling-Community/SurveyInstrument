using OSDC.DotnetLibraries.General.DataManagement;
using System;

namespace OSDC.Drilling.SurveyInstrument.Model;

public class SurveyInstrumentFeatureOption : IFeatureOption
{
    public Guid ID { get; set; }
    public string? Name { get; set; }
}
