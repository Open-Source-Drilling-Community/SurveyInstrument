using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OSDC.DotnetLibraries.Drilling.Surveying;
using OSDC.DotnetLibraries.General.DataManagement;
using OSDC.Drilling.SurveyInstrument.Service;
using OSDC.Drilling.SurveyInstrument.Service.Managers;
using SurveyInstrumentModel = OSDC.Drilling.SurveyInstrument.Model.SurveyInstrument;

namespace OSDC.Drilling.SurveyInstrument.ServiceTest;

[TestFixture]
public sealed class SurveyInstrumentManagerPersistenceTests
{
    [Test]
    public void Create_and_update_accept_apostrophes_and_create_assigns_server_timestamps()
    {
        WithManager((manager, _) =>
        {
            var instrument = new SurveyInstrumentModel
            {
                MetaInfo = new MetaInfo { ID = Guid.NewGuid() },
                Name = "U1's instrument",
                Description = "Main features of well U1's survey instrument",
                CreationDate = DateTimeOffset.UnixEpoch,
                LastModificationDate = DateTimeOffset.UnixEpoch,
                ModelType = SurveyInstrumentModelType.MWD_WolffDeWardt
            };

            DateTimeOffset before = DateTimeOffset.UtcNow;
            Assert.That(manager.AddSurveyInstrument(instrument), Is.True);
            DateTimeOffset after = DateTimeOffset.UtcNow;
            SurveyInstrumentModel stored = manager.GetSurveyInstrumentById(instrument.MetaInfo.ID)!;

            Assert.Multiple(() =>
            {
                Assert.That(stored.Name, Is.EqualTo(instrument.Name));
                Assert.That(stored.Description, Is.EqualTo(instrument.Description));
                Assert.That(stored.CreationDate, Is.InRange(before, after));
                Assert.That(stored.LastModificationDate, Is.EqualTo(stored.CreationDate));
            });

            stored.Description = "It's still valid after an update";
            Assert.That(manager.UpdateSurveyInstrumentById(stored.MetaInfo!.ID, stored, stored.LastModificationDate), Is.True);
            Assert.That(manager.GetSurveyInstrumentById(stored.MetaInfo.ID)!.Description, Is.EqualTo(stored.Description));
        });
    }

    [Test]
    public void Legacy_record_without_timestamps_can_be_updated_with_the_visible_epoch_token()
    {
        WithManager((manager, connections) =>
        {
            var instrument = new SurveyInstrumentModel
            {
                MetaInfo = new MetaInfo { ID = Guid.NewGuid() },
                Name = "Legacy instrument",
                ModelType = SurveyInstrumentModelType.MWD_WolffDeWardt
            };
            using (SqliteConnection connection = connections.GetConnection()!)
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO SurveyInstrumentTable " +
                    "(ID, MetaInfo, Name, Description, CreationDate, LastModificationDate, SurveyInstrument) " +
                    "VALUES ($id, $meta, $name, '', '', '', $document)";
                command.Parameters.AddWithValue("$id", instrument.MetaInfo.ID);
                command.Parameters.AddWithValue("$meta", System.Text.Json.JsonSerializer.Serialize(instrument.MetaInfo, JsonSettings.Options));
                command.Parameters.AddWithValue("$name", instrument.Name);
                command.Parameters.AddWithValue("$document", System.Text.Json.JsonSerializer.Serialize(instrument, JsonSettings.Options));
                Assert.That(command.ExecuteNonQuery(), Is.EqualTo(1));
            }

            SurveyInstrumentModel stored = manager.GetSurveyInstrumentById(instrument.MetaInfo.ID)!;
            Assert.That(stored.LastModificationDate, Is.EqualTo(DateTimeOffset.UnixEpoch));
            stored.Description = "Claude's update";
            Assert.That(manager.UpdateSurveyInstrumentById(stored.MetaInfo!.ID, stored, stored.LastModificationDate), Is.True);
        });
    }

    private static void WithManager(Action<SurveyInstrumentManager, SqlConnectionManager> assertion)
    {
        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"SurveyInstrumentPersistence_{Guid.NewGuid():N}.db");
        using ILoggerFactory loggers = LoggerFactory.Create(builder => builder.ClearProviders());
        try
        {
            var connections = new SqlConnectionManager($"Data Source={path};Pooling=False",
                loggers.CreateLogger<SqlConnectionManager>());
            ConstructorInfo constructor = typeof(SurveyInstrumentManager).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                [typeof(ILogger<SurveyInstrumentManager>), typeof(ILogger<ErrorSourceManager>), typeof(SqlConnectionManager)], null)!;
            var manager = (SurveyInstrumentManager)constructor.Invoke(
                [loggers.CreateLogger<SurveyInstrumentManager>(), loggers.CreateLogger<ErrorSourceManager>(), connections]);
            assertion(manager, connections);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
