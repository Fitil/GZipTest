using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace GZipTest
{
    class PerfomanceBalance : Compress
    {

        #region params

        /// <summary>Name parameter in app.config</summary>
        public const string NameMinBytes = "MinBytes";
        /// <summary>Internal value for output from app.config</summary>
        private int InternalMinBytes;
        /// <summary>Minimal bytes interval for perfomance function. Initialize from app.config</summary>
        public int MinBytes
        {
            get { return InternalMinBytes; }
            set { this.InternalMinBytes = value; }
        }

        /// <summary>Name parameter in app.config</summary>
        public const string NameMaxBytes = "MaxBytes";
        /// <summary>Internal value for output from app.config</summary>
        private int InternalMaxBytes;
        /// <summary>Maximal bytes interval for perfomance function. Initialize from app.config</summary>
        public int MaxBytes
        {
            get { return InternalMaxBytes; }
            set { this.InternalMaxBytes = value; }
        }

        /// <summary>Name parameter in app.config</summary>
        public const string NameMinThreads = "MinThreads";
        /// <summary>Internal value for output from app.config</summary>
        private int InternalMinThreads;
        /// <summary>Minimal threads interval for perfomance function. Initialize from app.config</summary>
        public int MinThreads
        {
            get { return InternalMinThreads; }
            set { this.InternalMinThreads = value; }
        }

        /// <summary>Name parameter in app.config</summary>
        public const string NameMaxThreads = "MaxThreads";
        /// <summary>Internal value for output from app.config</summary>
        private int InternalMaxThreads;
        /// <summary>Maximal threads interval for perfomance function. Initialize from app.config</summary>
        public int MaxThreads
        {
            get { return InternalMaxThreads; }
            set { this.InternalMaxThreads = value; }
        }

        #endregion

        public PerfomanceBalance(ManualResetEvent MainProgrammEvent)
            : base(MainProgrammEvent) 
        {
            MinBytes = 1024;
            MaxBytes = 1024 * 1024 * 10;
            MinThreads = 4;
            MaxThreads = 8;
            Int32.TryParse(ConfigurationManager.AppSettings[NameMinBytes], out InternalMinBytes);
            Int32.TryParse(ConfigurationManager.AppSettings[NameMaxBytes], out InternalMaxBytes);
            Int32.TryParse(ConfigurationManager.AppSettings[NameMinThreads], out InternalMinThreads);
            Int32.TryParse(ConfigurationManager.AppSettings[NameMaxThreads], out InternalMaxThreads);
        }
        /// <summary>
        /// Calculate perfomance while compress file  using GZipStream
        /// </summary>
        /// <param name="SourceFileFullName">Path to file fo analyze compression speed</param>
        /// <param name="ArchiveFileFullName">Path to file for writing buffer</param>
        /// <returns>return exit flag for check nessesary compering</returns>
        public void CalculatePerfomance(string SourceFileFullName, string ArchiveFileFullName)
        {
            int CompressRate = 0;
            long SourceFileSize = new FileInfo(SourceFileFullName).Length;
            long ArchiveFileSize = 0;
            int readIteration = 0;
            List<PerfomanceByBytes> PerfomList = new List<PerfomanceByBytes>();
            Stopwatch sWatch = new Stopwatch();

            if (ConfigurationManager.AppSettings["CalculateMinimum"] == "1")
            {
                //calculate start for minimal params
                MinThreads = Environment.ProcessorCount;
                MinBytes = MinBytes > SourceFileSize / 1000 ? MinBytes : (int)SourceFileSize / 1000;
            }

            CountThreads = MinThreads;
            CountBytesOneTimeRead = MinBytes;

            events = new ManualResetEvent[CountThreads];

            //calculate count reads for geometric progression *2
            //it will be Log 2 MaxBytes / MinBytes, multiple by max count threads divided by 2
            int CountAllReads = (int)Math.Ceiling((double)MaxBytes / MinBytes);
            CountAllReads = (int)Math.Log(CountAllReads, 2) + 1;
            CountAllReads = (int)Math.Ceiling(CountAllReads * (MaxThreads - MinThreads + 2) / 2.0) ;

            //steps from incriment threads and bytes size while not reaching maximum
            while (CountThreads <= MaxThreads)
            {
                while (CountBytesOneTimeRead <= MaxBytes)
                {
                    sWatch.Start();
                    //read first iteration
                    PrevEvent.Set();
                    FullCompress(SourceFileFullName, ArchiveFileFullName);
                    sWatch.Stop();
                    sWatch.Reset();
                    ArchiveFileSize = new FileInfo(ArchiveFileFullName).Length;
                    CompressRate = (int)Math.Floor(100 - ArchiveFileSize * 100.0 / SourceFileSize);
                    PerfomList.Add(new PerfomanceByBytes(CountBytesOneTimeRead, CountThreads, Math.Round(sWatch.Elapsed.TotalSeconds, 2), CompressRate));

                    //step to next iteration
                    File.Delete(ArchiveFileFullName);
                    CountBytesOneTimeRead *= 2;

                    readIteration++;
                    Console.WriteLine("Perfomance interation {0} of {1}", readIteration, CountAllReads);
                }
                CountBytesOneTimeRead = MinBytes;
                CountThreads += 2;
                //reinitialize events stack
                events = new ManualResetEvent[CountThreads];
            }

            Console.WriteLine("Perfomance interation {0} of {1}", CountAllReads, CountAllReads);

            if (PerfomList.Count() > 0)
                try
                {
                    //select fastest and best copressed iteration byte length
                    PerfomanceByBytes PerfomanceResult = PerfomList.Where(itm => itm.CompressRate > 0)
                        .OrderBy(itm => itm.ArchiveSpeed)
                        .ThenByDescending(itm => itm.CompressRate)
                        .FirstOrDefault();
                    CountBytesOneTimeRead = PerfomanceResult.CountBytes;
                    CountThreads = PerfomanceResult.CountThreads;
                }
                catch (NullReferenceException)
                {
                    //if better compressed result does not exist
                    //select fastest and not worst compressed iteration byte length
                    PerfomanceByBytes PerfomanceResult = PerfomList.OrderByDescending(itm => itm.CompressRate)
                        .ThenBy(itm => itm.ArchiveSpeed)
                        .FirstOrDefault();
                    CountBytesOneTimeRead = PerfomanceResult.CountBytes;
                    CountThreads = PerfomanceResult.CountThreads;
                }
                finally
                {
                    UpdateSetting(NameCountBytesOneTimeRead, CountBytesOneTimeRead.ToString());
                    UpdateSetting(NameCountThreads, CountThreads.ToString());
                    Console.WriteLine("{0} set to {1}", NameCountBytesOneTimeRead, CountBytesOneTimeRead);
                    Console.WriteLine("{0} set to {1}", NameCountThreads, CountThreads);
                }
        }
        
        /// <summary>
        /// Update specific XML parameter from current app.config file
        /// </summary>
        /// <param name="key">Parameter name</param>
        /// <param name="value">Parameter value</param>
        private void UpdateSetting(string key, string value)
        {
            try
            {
                Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                configuration.AppSettings.Settings[key].Value = value;
                configuration.Save();
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch (NullReferenceException)
            {
                Console.WriteLine("Please check parameter name. Parameter not found {0}", key);
            }
        }

    }

    /// <summary>Contener for one perfomance iteration result</summary>
    class PerfomanceByBytes
    {
        /// <summary>Current bytes size for perfomance test</summary>
        public int CountBytes { get; set; }
        /// <summary>Current threads size for perfomance test</summary>
        public int CountThreads { get; set; }
        /// <summary>Number of seconds during archive operation finished. Recommend accuracy .00</summary>
        public double ArchiveSpeed { get; set; }
        /// <summary>Comporession level in percent. If less then zero - compressed file more then original</summary>
        public int CompressRate { get; set; }

        /// <summary>
        /// Contener for one perfomance iteration result
        /// </summary>
        /// <param name="CountBytesP">Current bytes size for perfomance test</param>
        /// <param name="CountThreadsP">Current threads size for perfomance test</param>
        /// <param name="ArchiveSpeedP"> Number of seconds during archive operation finished. Recommend accuracy .00</param>
        /// <param name="CompressRateP">Comporession level in percent</param>
        public PerfomanceByBytes(int CountBytesP, int CountThreadsP, double ArchiveSpeedP, int CompressRateP)
        {
            CountBytes = CountBytesP;
            CountThreads = CountThreadsP;
            ArchiveSpeed = ArchiveSpeedP;
            CompressRate = CompressRateP;
        }
    }
}
