using OSDC.DotnetLibraries.General.DataManagement;
using System;

namespace OSDC.Drilling.SurveyInstrument.Model;

public class SurveyInstrumentIdentity : IIdentity
{
    public MetaInfo? MetaInfo { get; set; }
    public string? Name { get; set; }
    public DateTimeOffset? CreationDate { get; set; }
    public DateTimeOffset? LastModificationDate { get; set; }
}
