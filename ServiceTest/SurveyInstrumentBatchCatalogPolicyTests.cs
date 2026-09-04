using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OSDC.DotnetLibraries.Drilling.Surveying;
using OSDC.DotnetLibraries.General.DataManagement;
using OSDC.Drilling.SurveyInstrument.Model;
using OSDC.Drilling.SurveyInstrument.Service;
using OSDC.Drilling.SurveyInstrument.Service.Managers;

namespace OSDC.Drilling.SurveyInstrument.ServiceTest;

[TestFixture]
public sealed class SurveyInstrumentBatchCatalogPolicyTests
{
    [Test]
    public void Map_existing_rejects_missing_catalogs_while_create_missing_restores_atomically()
    {
        string directory = Path.Combine(Path.GetTempPath(), "survey-instrument-catalog-policy", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "SurveyInstrument.db");
        try
        {
            var connections = new SqlConnectionManager($"Data Source={path};Pooling=False",
                NullLogger<SqlConnectionManager>.Instance);
            var service = new SurveyInstrumentBatchService(connections);
            Guid identityId = Guid.NewGuid();
            Guid instrumentId = Guid.NewGuid();
            var document = new SurveyInstrumentBatchExportDocument
            {
                ExportedAtUtc = DateTimeOffset.UtcNow,
                CatalogDependencies = new()
                {
                    Identities = [new() { MetaInfo = new MetaInfo { ID = identityId }, Name = "Portable identity" }]
                },
                SurveyInstruments =
                [
                    new()
                    {
                        MetaInfo = new MetaInfo { ID = instrumentId },
                        Name = "Portable instrument",
                        ModelType = SurveyInstrumentModelType.MWD_WolffDeWardt,
                        SurveyInstrumentIdentityAssignments =
                        [
                            new() { ID = Guid.NewGuid(), IdentityID = identityId, Value = "Planning name" }
                        ]
                    }
                ]
            };

            SurveyInstrumentBatchRestoreOutcome missing = service.Restore(new()
            {
                CatalogPolicy = SurveyInstrumentBatchCatalogRestorePolicy.MapExisting,
                ConflictPolicy = SurveyInstrumentBatchRestoreConflictPolicy.FailIfExists,
                Document = document
            });
            Assert.Multiple(() =>
            {
                Assert.That(missing.FailureKind, Is.EqualTo(SurveyInstrumentBatchFailureKind.Conflict));
                Assert.That(missing.Error!.Errors.Any(value => value.Code == "catalog_definition_missing"), Is.True);
                Assert.That(Count(path, "SurveyInstrumentIdentityTable"), Is.Zero);
                Assert.That(Count(path, "SurveyInstrumentTable"), Is.Zero);
            });

            SurveyInstrumentBatchRestoreOutcome restored = service.Restore(new()
            {
                CatalogPolicy = SurveyInstrumentBatchCatalogRestorePolicy.MapOrCreateMissing,
                ConflictPolicy = SurveyInstrumentBatchRestoreConflictPolicy.FailIfExists,
                Document = document
            });
            Assert.Multiple(() =>
            {
                Assert.That(restored.Response, Is.Not.Null);
                Assert.That(restored.Response!.CreatedCatalogDefinitionCount, Is.EqualTo(1));
                Assert.That(restored.Response.CreatedCount, Is.EqualTo(1));
                Assert.That(Count(path, "SurveyInstrumentIdentityTable"), Is.EqualTo(1));
                Assert.That(Count(path, "SurveyInstrumentTable"), Is.EqualTo(1));
            });
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Test]
    public void Missing_catalog_policy_keeps_the_legacy_create_missing_behavior()
    {
        Assert.That(new SurveyInstrumentBatchRestoreRequest().CatalogPolicy,
            Is.EqualTo(SurveyInstrumentBatchCatalogRestorePolicy.MapOrCreateMissing));
    }

    private static long Count(string path, string table)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return Convert.ToInt64(command.ExecuteScalar());
    }
}
