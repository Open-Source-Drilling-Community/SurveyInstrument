using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OSDC.Drilling.SurveyInstrument.Service.Managers;

namespace OSDC.Drilling.SurveyInstrument.ServiceTest;

[TestFixture]
public sealed class SqlConnectionManagerSafetyTests
{
    private ILogger<SqlConnectionManager> _logger = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        ILoggerFactory factory = LoggerFactory.Create(builder => builder.ClearProviders());
        _logger = factory.CreateLogger<SqlConnectionManager>();
    }

    [Test]
    public void Fresh_database_is_created_transactionally_with_the_current_schema_version()
    {
        WithDatabase(path =>
        {
            _ = Manager(path);

            using SqliteConnection connection = Open(path);
            Assert.Multiple(() =>
            {
                Assert.That(TableCount(connection, "ErrorSourceTable"), Is.EqualTo(1));
                Assert.That(TableCount(connection, "SurveyInstrumentTable"), Is.EqualTo(1));
                Assert.That(TableCount(connection, "SurveyInstrumentIdentityTable"), Is.EqualTo(1));
                Assert.That(TableCount(connection, "SurveyInstrumentFeatureCategoryTable"), Is.EqualTo(1));
                Assert.That(ScalarLong(connection, "PRAGMA user_version"), Is.EqualTo(SqlConnectionManager.CURRENT_SCHEMA_VERSION));
            });
        });
    }

    [Test]
    public void Valid_legacy_database_is_adopted_without_changing_existing_rows()
    {
        WithDatabase(path =>
        {
            using (SqliteConnection connection = Open(path))
            {
                CreateExpectedTables(connection);
                Execute(connection, "INSERT INTO ErrorSourceTable (ID,MetaInfo,ErrorSource) VALUES ('error-marker','meta','{\"payload\":\"error-preserve-me\"}')");
                Execute(connection, "INSERT INTO SurveyInstrumentTable (ID,MetaInfo,Name,Description,CreationDate,LastModificationDate,SurveyInstrument) " +
                                    "VALUES ('instrument-marker','meta','preserve-name','preserve-description','created','modified','{\"payload\":\"instrument-preserve-me\"}')");
            }

            _ = Manager(path);
            _ = Manager(path);

            using SqliteConnection verification = Open(path);
            Assert.Multiple(() =>
            {
                Assert.That(ScalarString(verification, "SELECT ErrorSource FROM ErrorSourceTable WHERE ID='error-marker'"),
                    Is.EqualTo("{\"payload\":\"error-preserve-me\"}"));
                Assert.That(ScalarString(verification, "SELECT SurveyInstrument FROM SurveyInstrumentTable WHERE ID='instrument-marker'"),
                    Is.EqualTo("{\"payload\":\"instrument-preserve-me\"}"));
                Assert.That(ScalarString(verification, "SELECT Name FROM SurveyInstrumentTable WHERE ID='instrument-marker'"), Is.EqualTo("preserve-name"));
                Assert.That(ScalarLong(verification, "SELECT COUNT(*) FROM SurveyInstrumentTable"), Is.EqualTo(1));
                Assert.That(TableCount(verification, "SurveyInstrumentIdentityTable"), Is.EqualTo(1));
                Assert.That(TableCount(verification, "SurveyInstrumentFeatureCategoryTable"), Is.EqualTo(1));
                Assert.That(ScalarLong(verification, "PRAGMA user_version"), Is.EqualTo(SqlConnectionManager.CURRENT_SCHEMA_VERSION));
            });
        });
    }

    [Test]
    public void Version_one_database_is_upgraded_additively_without_rewriting_existing_rows()
    {
        WithDatabase(path =>
        {
            using (SqliteConnection connection = Open(path))
            {
                CreateExpectedTables(connection);
                Execute(connection, "INSERT INTO ErrorSourceTable (ID,ErrorSource) VALUES ('error-marker','error-preserve-me')");
                Execute(connection, "INSERT INTO SurveyInstrumentTable (ID,Name,SurveyInstrument) VALUES ('instrument-marker','preserve-me','instrument-preserve-me')");
                Execute(connection, "PRAGMA user_version = 1");
            }

            _ = Manager(path);

            using SqliteConnection verification = Open(path);
            Assert.Multiple(() =>
            {
                Assert.That(ScalarString(verification, "SELECT ErrorSource FROM ErrorSourceTable WHERE ID='error-marker'"), Is.EqualTo("error-preserve-me"));
                Assert.That(ScalarString(verification, "SELECT SurveyInstrument FROM SurveyInstrumentTable WHERE ID='instrument-marker'"), Is.EqualTo("instrument-preserve-me"));
                Assert.That(TableCount(verification, "SurveyInstrumentIdentityTable"), Is.EqualTo(1));
                Assert.That(TableCount(verification, "SurveyInstrumentFeatureCategoryTable"), Is.EqualTo(1));
                Assert.That(ScalarLong(verification, "PRAGMA user_version"), Is.EqualTo(2));
            });
        });
    }

    [Test]
    public void Default_identity_and_feature_catalogs_match_the_domain_taxonomy()
    {
        WithDatabase(path =>
        {
            SqlConnectionManager connections = Manager(path);
            var identities = new SurveyInstrumentIdentityManager(connections).GetAll();
            var features = new SurveyInstrumentFeatureCategoryManager(connections).GetAll();

            Assert.Multiple(() =>
            {
                Assert.That(identities.Select(value => value.Name), Is.EquivalentTo(new[]
                {
                    "OfficialName", "ManufacturerName", "ModelName", "ProductFamilyName",
                    "ToolName", "ToolShortName", "CommonName", "NickName"
                }));
                Assert.That(features, Has.Count.EqualTo(16));
                Assert.That(features.Single(value => value.Name == "AzimuthReference").IsExclusive, Is.True);
                Assert.That(features.Single(value => value.Name == "CertificationStatus").HasValidityPeriod, Is.True);
                Assert.That(features.Single(value => value.Name == "CalibrationMode").HasValidityPeriod, Is.True);
                Assert.That(features.Single(value => value.Name == "CorrectionCapability").Options!.Select(value => value.Name),
                    Does.Contain("DrillstringMagneticInterferenceCorrection"));
                Assert.That(features.All(value => value.Options!.Any(option => option.Name == "Unknown")), Is.True);
            });
        });
    }

    [Test]
    public void Newer_schema_stops_without_modifying_data()
    {
        WithDatabase(path =>
        {
            using (SqliteConnection connection = Open(path))
            {
                CreateExpectedTables(connection);
                Execute(connection, "INSERT INTO SurveyInstrumentTable (ID,Name,SurveyInstrument) VALUES ('marker','preserve-me','payload')");
                Execute(connection, $"PRAGMA user_version = {SqlConnectionManager.CURRENT_SCHEMA_VERSION + 1}");
            }

            Assert.That(() => Manager(path), Throws.InvalidOperationException.With.Message.Contains("newer than supported"));

            using SqliteConnection verification = Open(path);
            Assert.Multiple(() =>
            {
                Assert.That(ScalarString(verification, "SELECT SurveyInstrument FROM SurveyInstrumentTable WHERE ID='marker'"), Is.EqualTo("payload"));
                Assert.That(ScalarLong(verification, "PRAGMA user_version"), Is.EqualTo(SqlConnectionManager.CURRENT_SCHEMA_VERSION + 1));
            });
        });
    }

    [Test]
    public void Unknown_or_malformed_schema_stops_without_dropping_anything()
    {
        WithDatabase(path =>
        {
            using (SqliteConnection connection = Open(path))
            {
                Execute(connection, "CREATE TABLE LegacyData (ID text primary key, Payload text)");
                Execute(connection, "INSERT INTO LegacyData VALUES ('marker','preserve-me')");
            }

            Assert.That(() => Manager(path), Throws.InvalidOperationException.With.Message.Contains("No data was changed"));

            using SqliteConnection verification = Open(path);
            Assert.Multiple(() =>
            {
                Assert.That(TableCount(verification, "LegacyData"), Is.EqualTo(1));
                Assert.That(ScalarString(verification, "SELECT Payload FROM LegacyData WHERE ID='marker'"), Is.EqualTo("preserve-me"));
                Assert.That(TableCount(verification, "SurveyInstrumentTable"), Is.EqualTo(0));
                Assert.That(ScalarLong(verification, "PRAGMA user_version"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Failed_legacy_adoption_rolls_back_completely()
    {
        WithDatabase(path =>
        {
            using (SqliteConnection connection = Open(path))
            {
                CreateExpectedTables(connection);
                Execute(connection, "INSERT INTO SurveyInstrumentTable (ID,Name,SurveyInstrument) VALUES ('marker','preserve-me','payload')");
            }

            Assert.That(
                () => new SqlConnectionManager($"Data Source={path};Mode=ReadOnly;Pooling=False", _logger),
                Throws.TypeOf<SqliteException>());

            using SqliteConnection verification = Open(path);
            Assert.Multiple(() =>
            {
                Assert.That(ScalarString(verification, "SELECT SurveyInstrument FROM SurveyInstrumentTable WHERE ID='marker'"), Is.EqualTo("payload"));
                Assert.That(ScalarLong(verification, "PRAGMA user_version"), Is.EqualTo(0));
                Assert.That(ScalarLong(verification, "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name IN ('ErrorSourceTableIndex','SurveyInstrumentTableIndex')"), Is.EqualTo(0));
            });
        });
    }

    private SqlConnectionManager Manager(string path) =>
        new($"Data Source={path};Pooling=False", _logger);

    private static SqliteConnection Open(string path)
    {
        SqliteConnection connection = new($"Data Source={path};Pooling=False");
        connection.Open();
        return connection;
    }

    private static void CreateExpectedTables(SqliteConnection connection)
    {
        Execute(connection, "CREATE TABLE ErrorSourceTable (ID text primary key,MetaInfo text,ErrorSource text)");
        Execute(connection, "CREATE TABLE SurveyInstrumentTable (ID text primary key,MetaInfo text,Name text,Description text,CreationDate text,LastModificationDate text,SurveyInstrument text)");
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long TableCount(SqliteConnection connection, string name) =>
        ScalarLong(connection, $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{name}'");

    private static long ScalarLong(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static string? ScalarString(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar());
    }

    private static void WithDatabase(Action<string> assertion)
    {
        string directory = Path.Combine(Path.GetTempPath(), "SurveyInstrumentSafetyTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            assertion(Path.Combine(directory, SqlConnectionManager.DATABASE_FILENAME));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
