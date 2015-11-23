using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace GZipTest
{
    class Decompress : ArchiveMain
    {

        public Decompress(ManualResetEvent MainProgrammEvent)
            : base(MainProgrammEvent) { }

        /// <summary>
        /// decompress file using GZipStream
        /// </summary>
        /// <param name="ArchiveFileFullName">Path to file what need to decompress</param>
        /// <param name="DecFileFullName">Path to file for writing decompressed bytes</param>
        public void FullDecompress(string ArchiveFileFullName, string DecFileFullName)
        {
            Console.WriteLine("Start decompression {0}", DateTime.Now);
            using (FileStream SourceFile = new FileStream(ArchiveFileFullName, FileMode.Open))
            {
                using (FileStream DestFile = File.Create(DecFileFullName))
                {
                    //initialize events, by default all events - close
                    for (int i = 0; i < CountThreads; i++)
                        events[i] = new ManualResetEvent(false);
                    AutoResetEvent WaitStart = new AutoResetEvent(false);
                    //Calculate parametsr for load indicator
                    int CountAllReads = (int)Math.Ceiling((double)SourceFile.Length / CountBytesOneTimeRead);
                    int IterationRead = 0;
                    byte[] buffer;

                    //check is file multiheaded
                    SourceFile.MoveToNextGZip(out buffer);
                    if (buffer == null)
                    {
                        #region decompress 1 headed file
                        //create new decompression stream
                        using (GZipStream Decompress = new GZipStream(SourceFile, CompressionMode.Decompress))
                        {
                            buffer = new byte[CountBytesOneTimeRead];
                            int bytesLen = 0;
                            //read file by parts while nothing read
                            while ((bytesLen = Decompress.Read(buffer, 0, CountBytesOneTimeRead)) > 0)
                            {
                                //display compressing progress
                                LoadIndication(CountAllReads, IterationRead, ExitFlag);
                                IterationRead++;
                                DestFile.Write(buffer, 0, bytesLen);
                            }
                            //display 100% load
                            LoadIndication(1, 1, ExitFlag);
                        }
                        #endregion
                    }
                    else
                    {
                        #region decompress multiple headed files
                        SourceFile.Seek(0, SeekOrigin.Begin);
                        for (int CountCurrentThreads = 0; CountCurrentThreads < CountThreads; CountCurrentThreads++)
                        {
                            if (ExitFlag) break;

                            Thread thr = new Thread(delegate()
                            {
                                PartialDecompress(SourceFile, DestFile, events[CountCurrentThreads]
                                    , WaitStart, CountAllReads, ref IterationRead);
                            });
                            thr.IsBackground = true;
                            try { thr.Start(); }
                            catch (ThreadStartException ex) { events[CountCurrentThreads].Set(); ShowErrorMessage(ex, "start decompressing thread"); }

                            //Wait while thread gets all params
                            if (!ExitFlag)
                                WaitStart.WaitOne();

                        }
                        #endregion
                    }
                    //wait while all decompressing threads do they job
                    WaitHandle.WaitAll(events);
                    LoadIndication(1, 1, ExitFlag);
                }
            }
            Console.WriteLine("End decompression {0}", DateTime.Now);
        }

        /// <summary>
        /// Decompress part of file into specific filestream
        /// </summary>
        /// <param name="ArchiveBMass">Compressed bytes</param>
        /// <param name="DestFile">Destination file for writing decimpressed bytes</param>
        /// <param name="CurEvent">Wait handle. Current thread. Need to send it next thread</param>
        /// <param name="WaitEvent">Wait handle. Wait previos thread</param>
        /// <param name="WaitStartStream">Wait handle. Stop main thread while current not get all params</param>
        /// <param name="CountAllReads">Max reads. Need for display progress</param>
        /// <param name="IterationRead">Current read. Need for display progress</param>
        /// <param name="CountCurrentThreads">Dont get create threads more then CountThread</param>
        private void PartialDecompress(FileStream SourceFile, FileStream DestFile, ManualResetEvent CurEvent
            , AutoResetEvent WaitStartStream, int CountAllReads, ref int IterationRead)
        {
            ManualResetEvent WaitEventRead = new ManualResetEvent(false);
            ManualResetEvent CurEventRead = new ManualResetEvent(false);
            byte[] ArchiveBMass;

            //info main stream about getting all params
            WaitStartStream.Set();

            try
            {

                while (SourceFile.MoveToNextGZip(out ArchiveBMass, ReadLocker,ref CurEventRead, ref PrevEvent, ref WaitEventRead))
                {
                    int bytesRead = 0;
                    int AllBytesRead = 0;
                    using (MemoryStream ArchiveBuffer = new MemoryStream(ArchiveBMass))
                    {
                        //decompress current ArchiveBMass
                        using (GZipStream Decompress = new GZipStream(ArchiveBuffer, CompressionMode.Decompress, true))
                        {
                            byte[] DecompressedArchiveBMass = new byte[CountBytesOneTimeRead];
                            //read compressed part while it not ends
                            bool firstread = true;
                            while ((bytesRead = Decompress.Read(DecompressedArchiveBMass, 0, CountBytesOneTimeRead)) > 0)
                            {
                                AllBytesRead += bytesRead;
                                if (firstread)
                                    ArchiveBMass = (byte[])DecompressedArchiveBMass.Clone();
                                else
                                    ArchiveBMass = ArchiveBMass.Concat(DecompressedArchiveBMass).ToArray();
                                firstread = false;
                            }
                        }

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

                        //write decompressed part into file
                        lock (WriteLocker)
                            if (AllBytesRead > 0)
                                DestFile.Write(ArchiveBMass, 0, AllBytesRead);
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
                ShowErrorMessage(ex, "Decompress thread process");
            }
        }

    }
}
