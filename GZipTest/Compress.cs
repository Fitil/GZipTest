using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace GZipTest
{
    class Compress : ArchiveMain
    {
        public Compress(ManualResetEvent MainProgrammEvent)
            : base(MainProgrammEvent) { }

        /// <summary>
        /// Compress file using GZipStream
        /// </summary>
        /// <param name="SourceFileFullName">Path to file what need to compress</param>
        /// <param name="ArchiveFileFullName">Path to file for writing compressed bytes</param>
        /// <returns>return exit flag for check nessesary compering</returns>
        public void FullCompress(string SourceFileFullName, string ArchiveFileFullName)
        {
            Console.WriteLine("Start compression {0}", DateTime.Now);
            using (FileStream SourceFile = new FileStream(SourceFileFullName, FileMode.Open))
            {
                using (FileStream DestFile = File.Create(ArchiveFileFullName))
                {
                    //initialize events, by default all events - close
                    for (int i = 0; i < CountThreads; i++)
                        events[i] = new ManualResetEvent(false);
                    ManualResetEvent WaitStart = new ManualResetEvent(false);
                    //Calculate parameters for load indicator
                    int CountAllReads = (int)Math.Ceiling((double)SourceFile.Length / CountBytesOneTimeRead);
                    int IterationRead = 0;

                    for (int CountCurrentThreads = 0; CountCurrentThreads < CountThreads; CountCurrentThreads++)
                    {
                        if (ExitFlag) break;

                        WaitStart.Reset();

                        Thread thr = new Thread(delegate()
                        {
                            PartialCompress(SourceFile, DestFile, events[CountCurrentThreads]
                                , WaitStart, CountAllReads, ref IterationRead, CountCurrentThreads);
                        });
                        thr.IsBackground = true;
                        try { thr.Start(); }
                        catch (ThreadStartException ex) { events[CountCurrentThreads].Set(); ShowErrorMessage(ex, "start compressing thread"); }

                        //Wait while thread gets all params
                        if (!ExitFlag)
                            WaitStart.WaitOne();
                    }
                    //Wait all threads
                    WaitHandle.WaitAll(events);
                    LoadIndication(1, 1, ExitFlag);
                }
            }
            Console.WriteLine("End compression {0}", DateTime.Now);
        }

        /// <summary>
        /// Compress part of file into specific filestream
        /// </summary>
        /// <param name="buffer">Uncompressed bytes</param>
        /// <param name="DestFile">Destination file for writing compressed bytes</param>
        /// <param name="CurEvent">Wait handle. Current thread. Need to send it next thread</param>
        /// <param name="WaitEvent">Wait handle. Wait previous thread</param>
        /// <param name="WaitStartStream">Wait handle. Stop main thread while current not get all params</param>
        /// <param name="CountAllReads">Max reads. Need for display progress</param>
        /// <param name="IterationRead">Current read. Need for display progress</param>
        /// <param name="CountCurrentThreads">Dont get create threads more then CountThread</param>
        private void PartialCompress(FileStream SourceFile, FileStream DestFile, ManualResetEvent CurEvent
            , ManualResetEvent WaitStartStream, int CountAllReads, ref int IterationRead, int i)
        {
            ManualResetEvent WaitEventRead = new ManualResetEvent(false);
            ManualResetEvent CurEventRead = new ManualResetEvent(false);

            //info main stream about getting all params
            WaitStartStream.Set();
            int bytesRead = 0;
            byte[] buffer = new byte[CountBytesOneTimeRead];
            try
            {
                while ((bytesRead = SourceFile.LockRead(buffer, 0, CountBytesOneTimeRead, ReadLocker, ref CurEventRead, ref PrevEvent, ref WaitEventRead)) > 0)
                {
                    using (MemoryStream output = new MemoryStream(bytesRead))
                    {
                        //Lock waiting object because it can be close in previous thread
                        //compress current buffer
                        using (GZipStream cs = new GZipStream(output, CompressionMode.Compress, true))
                            cs.Write(buffer, 0, bytesRead);

                        if (WaitEventRead.WaitOne(10000))
                            WaitEventRead.Close();
                        else
                            ExitFlag = true;

                        //exit if Ctrl+C
                        if (ExitFlag)
                        {
                            CurEventRead.Set();
                            break;
                        }
                        //write to destination file
                        lock (WriteLocker)
                        {
                            DestFile.Write(output.ToArray(), 0, (int)output.Length);
                        }

                    }
                    //display progress
                    lock (LoadIndicationLocker)
                    {
                        LoadIndication(CountAllReads, IterationRead, ExitFlag);
                        IterationRead++;
                    }
                    //set signal to waiting thread
                    CurEventRead.Set();
                }

                if (!CurEvent.WaitOne(0))
                    CurEvent.Set();
                if (!WaitStartStream.WaitOne(0))
                    WaitStartStream.Set();
            }
            catch (Exception ex)
            {
                if (!(ex is ObjectDisposedException || ex is NullReferenceException))
                {
                    WaitStartStream.Set();
                    CurEventRead.Set();
                    CurEvent.Set();
                }
                ShowErrorMessage(ex, "Compress thread process");
            }
        }

    }
}
