using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NORCE.Drilling.SurveyInstrument.Model
{
    public struct CountPerDay
    {
        public DateTime Date { get; set; }
        public ulong Count { get; set; }

        public CountPerDay()
        {
        }

        public CountPerDay(DateTime date, ulong count)
        {
            Date = date;
            Count = count;
        }
    }

    public class History
    {
        public List<CountPerDay> Data { get; set; } = new();

        public void Increment()
        {
            if (Data.Count == 0 || Data[^1].Date < DateTime.UtcNow.Date)
            {
                Data.Add(new CountPerDay(DateTime.UtcNow.Date, 1));
            }
            else
            {
                Data[^1] = new CountPerDay(Data[^1].Date, Data[^1].Count + 1);
            }
        }
    }

    public class UsageStatisticsSurveyInstrument
    {
        public static readonly string HOME_DIRECTORY = ".." + Path.DirectorySeparatorChar + "home" + Path.DirectorySeparatorChar;

        public DateTime LastSaved { get; set; } = DateTime.MinValue;
        public TimeSpan BackUpInterval { get; set; } = TimeSpan.FromMinutes(5);

        public History GetAllSurveyInstrumentIdPerDay { get; set; } = new();
        public History GetAllSurveyInstrumentMetaInfoPerDay { get; set; } = new();
        public History GetSurveyInstrumentByIdPerDay { get; set; } = new();
        public History GetAllSurveyInstrumentLightPerDay { get; set; } = new();
        public History GetAllSurveyInstrumentPerDay { get; set; } = new();
        public History PostSurveyInstrumentPerDay { get; set; } = new();
        public History PutSurveyInstrumentByIdPerDay { get; set; } = new();
        public History DeleteSurveyInstrumentByIdPerDay { get; set; } = new();

        public History GetAllErrorSourceIdPerDay { get; set; } = new();
        public History GetAllErrorSourceMetaInfoPerDay { get; set; } = new();
        public History GetErrorSourceByIdPerDay { get; set; } = new();
        public History GetAllErrorSourcePerDay { get; set; } = new();
        public History PostErrorSourcePerDay { get; set; } = new();
        public History PutErrorSourceByIdPerDay { get; set; } = new();
        public History DeleteErrorSourceByIdPerDay { get; set; } = new();

        private static readonly object Lock = new();
        private static UsageStatisticsSurveyInstrument? instance_;

        public static UsageStatisticsSurveyInstrument Instance
        {
            get
            {
                if (instance_ == null)
                {
                    string path = HOME_DIRECTORY + "history.json";
                    if (File.Exists(path))
                    {
                        try
                        {
                            lock (Lock)
                            {
                                string jsonStr = File.ReadAllText(path);
                                if (!string.IsNullOrEmpty(jsonStr))
                                {
                                    instance_ = JsonSerializer.Deserialize<UsageStatisticsSurveyInstrument>(jsonStr);
                                }
                            }
                        }
                        catch
                        {
                        }
                    }

                    instance_ ??= new UsageStatisticsSurveyInstrument();
                }

                return instance_;
            }
        }

        public void IncrementGetAllSurveyInstrumentIdPerDay() => Increment(GetAllSurveyInstrumentIdPerDay);
        public void IncrementGetAllSurveyInstrumentMetaInfoPerDay() => Increment(GetAllSurveyInstrumentMetaInfoPerDay);
        public void IncrementGetSurveyInstrumentByIdPerDay() => Increment(GetSurveyInstrumentByIdPerDay);
        public void IncrementGetAllSurveyInstrumentLightPerDay() => Increment(GetAllSurveyInstrumentLightPerDay);
        public void IncrementGetAllSurveyInstrumentPerDay() => Increment(GetAllSurveyInstrumentPerDay);
        public void IncrementPostSurveyInstrumentPerDay() => Increment(PostSurveyInstrumentPerDay);
        public void IncrementPutSurveyInstrumentByIdPerDay() => Increment(PutSurveyInstrumentByIdPerDay);
        public void IncrementDeleteSurveyInstrumentByIdPerDay() => Increment(DeleteSurveyInstrumentByIdPerDay);

        public void IncrementGetAllErrorSourceIdPerDay() => Increment(GetAllErrorSourceIdPerDay);
        public void IncrementGetAllErrorSourceMetaInfoPerDay() => Increment(GetAllErrorSourceMetaInfoPerDay);
        public void IncrementGetErrorSourceByIdPerDay() => Increment(GetErrorSourceByIdPerDay);
        public void IncrementGetAllErrorSourcePerDay() => Increment(GetAllErrorSourcePerDay);
        public void IncrementPostErrorSourcePerDay() => Increment(PostErrorSourcePerDay);
        public void IncrementPutErrorSourceByIdPerDay() => Increment(PutErrorSourceByIdPerDay);
        public void IncrementDeleteErrorSourceByIdPerDay() => Increment(DeleteErrorSourceByIdPerDay);

        private void Increment(History history)
        {
            lock (Lock)
            {
                history.Increment();
                ManageBackup();
            }
        }

        private void ManageBackup()
        {
            if (DateTime.UtcNow <= LastSaved + BackUpInterval)
            {
                return;
            }

            LastSaved = DateTime.UtcNow;
            try
            {
                if (Directory.Exists(HOME_DIRECTORY))
                {
                    File.WriteAllText(HOME_DIRECTORY + "history.json", JsonSerializer.Serialize(this));
                }
            }
            catch
            {
            }
        }
    }
}
