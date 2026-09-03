using OSDC.DotnetLibraries.General.DataManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace OSDC.Drilling.SurveyInstrument.Service.Managers;

public sealed class SurveyInstrumentIdentityManager
{
    private static readonly string[] Defaults =
    [
        "OfficialName", "ManufacturerName", "ModelName", "ProductFamilyName",
        "ToolName", "ToolShortName", "CommonName", "NickName"
    ];

    private readonly SurveyInstrumentCatalogStore<Model.SurveyInstrumentIdentity> store;
    private readonly SqlConnectionManager connections;

    public SurveyInstrumentIdentityManager(SqlConnectionManager connections)
    {
        this.connections = connections;
        store = new(connections, "SurveyInstrumentIdentityTable", "SurveyInstrumentIdentity",
            value => value.MetaInfo, value => value.Name, (value, date) => value.CreationDate = date,
            (value, date) => value.LastModificationDate = date);
    }

    public List<Model.SurveyInstrumentIdentity> GetAll()
    {
        EnsureDefaults();
        return store.All();
    }

    public Model.SurveyInstrumentIdentity? Get(Guid id) => store.ById(id);
    public bool Add(Model.SurveyInstrumentIdentity value) => store.Add(value);
    public bool Update(Guid id, Model.SurveyInstrumentIdentity value) => store.Update(id, value);
    public bool Delete(Guid id) => !IsReferenced(id) && store.Delete(id);

    public bool IsReferenced(Guid id) => ReadSurveyInstruments().Any(value =>
        value.SurveyInstrumentIdentityAssignments?.Any(assignment => assignment.IdentityID == id) == true);

    private IEnumerable<Model.SurveyInstrument> ReadSurveyInstruments()
    {
        using var connection = connections.GetConnection();
        using var command = connection!.CreateCommand();
        command.CommandText = "SELECT SurveyInstrument FROM SurveyInstrumentTable";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            Model.SurveyInstrument? value = JsonSerializer.Deserialize<Model.SurveyInstrument>(reader.GetString(0), JsonSettings.Options);
            if (value != null)
            {
                yield return value;
            }
        }
    }

    private void EnsureDefaults()
    {
        if (store.All().Count > 0)
        {
            return;
        }
        foreach (string name in Defaults)
        {
            store.Add(new() { MetaInfo = new MetaInfo { ID = SurveyInstrumentCatalogId.For($"identity:{name}") }, Name = name });
        }
    }
}
