using OSDC.DotnetLibraries.General.DataManagement;
using System;

namespace OSDC.Drilling.SurveyInstrument.Model;

public class SurveyInstrumentFeatureAssignment : IFeatureAssignment
{
    public Guid ID { get; set; }
    public Guid? FeatureCategoryID { get; set; }
    public Guid? FeatureOptionID { get; set; }
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
}
