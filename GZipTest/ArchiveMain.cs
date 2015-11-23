using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading;

namespace GZipTest
{
    class ArchiveMain : IDisposable
    {

        #region parameters

        #region Byte and Thread params affect on - archive speed, compression force, load HDD and CPU

        /// <summary>Name parameter in app.config</summary>
        public const string NameCountBytesOneTimeRead = "CountBytesOneTimeRead";
        /// <summary>Internal value for output from app.config</summary>
        private int InternalCountBytesOneTimeRead;
        /// <summary>Current size one time bytes read</summary>
        public int CountBytesOneTimeRead
        {
            get { return InternalCountBytesOneTimeRead; }
            set { this.InternalCountBytesOneTimeRead = value; }
        }
        
        /// <summary>Name parameter in app.config</summary>
        public const string NameCountThreads = "CountThreads";
        /// <summary>Internal value for output from app.config</summary>
        private int InternalCountThreads;
        /// <summary>Current threads count</summary>
        public int CountThreads
        {
            get { return InternalCountThreads; }
            set { this.InternalCountThreads = value; }
        }

        #endregion

        #region lock objects nessesary to multithreaded execution

        /// <summary>Locker for Console.WriteLine progress</summary>
        public Object LoadIndicationLocker { get; set; }
        /// <summary>Locker for write in destination file </summary>
        public Object WriteLocker { get; set; }
        /// <summary>Locker for read in source file </summary>
        public Object ReadLocker { get; set; }
        /// <summary>Locker for error message output </summary>
        public Object MessageLocker { get; set; }

        #endregion

        #region ManualResetEvents

        /// <summary>Event massive for multithreading monitor</summary>
        public ManualResetEvent[] events;
        /// <summary>Main thread reset event</summary>
        public ManualResetEvent MainEvent;
        /// <summary>Last readed event waiting for write</summary>
        public ManualResetEvent PrevEvent;

        #endregion

        #region Boolean flags

        /// <summary>flag for unexpected errors or Ctrl+C</summary>
        public bool ExitFlag = false;
        /// <summary>Dispose status</summary>
        private bool disposed = false;

        #endregion

        #endregion

        public ArchiveMain(ManualResetEvent MainProgrammEvent)
        {
            CountBytesOneTimeRead = 1024 * 32;
            CountThreads = 4;
            ExitFlag = false;
            //get values from config file
            Int32.TryParse(ConfigurationManager.AppSettings[NameCountThreads], out InternalCountBytesOneTimeRead);
            Int32.TryParse(ConfigurationManager.AppSettings[NameCountBytesOneTimeRead], out InternalCountBytesOneTimeRead);
            //intialize events stack
            events = new ManualResetEvent[CountThreads];
            MainEvent = MainProgrammEvent;
            PrevEvent = new ManualResetEvent(true);
            //Initialize lock objects
            LoadIndicationLocker = new Object();
            WriteLocker = new Object();
            ReadLocker = new Object();
            MessageLocker = new Object();
        }

        #region Display information - LoadIndication, ShowErrorMessage, ClearConsoleLine

        /// <summary>
        /// Indicate load progress to console
        /// </summary>
        /// <param name="CountAllReads">count equal 100% in values</param>
        /// <param name="InerationRead">current iteration</param>
        /// <remarks>use (1,1) for 100%, or (any, 0) for 0%</remarks>
        public static void LoadIndication(int CountAllReads, int InerationRead, bool ExitIndication)
        {
            if (ExitIndication) return;

            //if somthing goes wrong
            if (CountAllReads < InerationRead)
            {
                ClearConsoleLine();
                Console.WriteLine("Please wait...");
            }
            //prevent devide by zero
            else if (InerationRead == 0)
            {
                Console.WriteLine("Loading 0%");
            }
            else
            {
                //prevent too frequent updates, update only integer of load percent
                double perc = Math.Round(InerationRead * 100.0 / CountAllReads, 1);
                if (Math.Ceiling(perc) == perc)
                {
                    ClearConsoleLine();
                    Console.WriteLine(String.Format("Loading {0}%", perc));
                }
            }
        }

        /// <summary>
        /// Outup to console formatted error massage and stop all threads
        /// </summary>
        /// <param name="ex">Catched exeption</param>
        /// <param name="ActionComment">Text informatin about code it try block</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ShowErrorMessage(Exception ex, string ActionComment)
        {
            lock (MessageLocker)
            {
                //get calls method
                StackTrace st = new StackTrace();
                StackFrame sf = st.GetFrame(1);
                //Show error info
                Console.WriteLine("Error method: {0}", sf.GetMethod().Name);
                Console.WriteLine("Action: {0}", ActionComment);
                Console.WriteLine("Message: {0}", ex.Message);
                Console.WriteLine("Inner Message: {0}", ex.InnerException != null ? ex.InnerException.Message : "not available");
                ExitFlag = true;
            }
        }

        /// <summary>Clear one previous line in console</summary>
        public static void ClearConsoleLine()
        {
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, Console.CursorTop - 1);
        }

        #endregion

        #region Dispose

        /// <summary>
        /// Cleanup and release resources
        /// </summary>
        /// <param name="key">If true, send finilize info for garbage collector</param>
        protected virtual void Dispose(bool suppressFinalize)
        {
            if (!disposed)
            {
                LoadIndicationLocker = null;
                WriteLocker = null;
                ReadLocker = null;
                foreach (ManualResetEvent ev in events)
                    if (ev != null)
                    {
                        try { ev.Close(); }
                        catch { }
                    }
                disposed = true;
            }
            if (!suppressFinalize)
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>Implement IDisposable </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>the finalizer</summary>
        ~ArchiveMain()
        {
            Dispose(false);
        }

        #endregion

        /// <summary>Terminate all process and exit from aplication with zero result</summary>
        public void ExitEvent(object sender, ConsoleCancelEventArgs args)
        {
            Console.WriteLine("Please wait while all partial threads terminate...");
            ExitFlag = true;
            WaitHandle.WaitAll(events, 20000);
            Console.WriteLine("Please wait main thread terminate...");
            MainEvent.WaitOne(5000);
        }


    }
}
