using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace GZipTest
{
    class Comparer : ArchiveMain
    {
        public Comparer(ManualResetEvent MainProgrammEvent)
            : base(MainProgrammEvent) { }

        /// <summary>
        /// Compare source and destination after compressing or decompressing, 
        /// </summary>
        /// <param name="SourceFileFullName">Path to uncopressed file</param>
        /// <param name="ArchiveFileFullName">Path to compressed file</param>
        /// <returns>Return true if both files matched</returns>
        public bool FullCheck(string SourceFileFullName, string ArchiveFileFullName)
        {
            bool result = true;
            Console.WriteLine("Start check result");

            using (FileStream SourceFile = new FileStream(SourceFileFullName, FileMode.Open))
            using (FileStream ArchiveFile = new FileStream(ArchiveFileFullName, FileMode.Open))
            {
                //initialize events, by default all events - close
                for (int j = 0; j < CountThreads; j++)
                    events[j] = new ManualResetEvent(false);
                ManualResetEvent WaitStart = new ManualResetEvent(false);
                //Calculate parametsr for loading indicator
                int CountAllReads = (int)Math.Ceiling((double)SourceFile.Length / CountBytesOneTimeRead);
                int IterationRead = 0;
                int bytesRead = 0;
                byte[] SourceBMass = new byte[CountBytesOneTimeRead];
                byte[] ArchiveBMass = new byte[CountBytesOneTimeRead];

                SourceFile.Seek(0, SeekOrigin.Begin);

                //read both files by parts while nothing read
                ArchiveFile.MoveToNextGZip(out ArchiveBMass);
                if (ArchiveBMass == null)
                {
                    #region check all file in 1 thread
                    int i = 0;
                    using (GZipStream Decompress = new GZipStream(ArchiveFile, CompressionMode.Decompress, true))
                    {
                        byte[] DecompressedArchiveBMass = new byte[CountBytesOneTimeRead];
                        //read compressed file
                        while ((bytesRead = Decompress.Read(DecompressedArchiveBMass, 0, CountBytesOneTimeRead)) > 0)
                        {
                            //stop if error
                            if (ExitFlag)
                            {
                                result = false;
                                break;
                            }
                            SourceBMass = new byte[bytesRead];
                            SourceFile.Read(SourceBMass, 0, bytesRead);
                            //display comprering progress
                            LoadIndication(CountAllReads, i, ExitFlag);
                            //if find not equal block then exit with false
                            if (BitConverter.ToInt32(SourceBMass, 0) != BitConverter.ToInt32(DecompressedArchiveBMass, 0))
                            {
                                //display 100% comprering
                                LoadIndication(1, 1, ExitFlag);
                                return false;
                            }
                            i++;
                        }
                    }
                    #endregion
                }
                else
                {
                    #region check file in multiple threads
                    ArchiveFile.Seek(0, SeekOrigin.Begin);
                    for (int CountCurrentThreads = 0; CountCurrentThreads < CountThreads; CountCurrentThreads++)
                    {
                        //stop if error
                        if (ExitFlag) break;

                        WaitStart.Reset();

                        Thread thr = new Thread(delegate()
                        {
                            PartialCheck(ArchiveFile, SourceFile, events[CountCurrentThreads]
                                , WaitStart, CountAllReads, ref IterationRead, ref result);
                        });
                        thr.IsBackground = true;
                        try { thr.Start(); }
                        catch (ThreadStartException ex) { events[CountCurrentThreads].Set(); ShowErrorMessage(ex, "start comparing thread"); }

                        //Wait while thread gets all params
                        if (!ExitFlag)
                            WaitStart.WaitOne();
                    }
                    #endregion
                } 

                WaitHandle.WaitAll(events);
                LoadIndication(1, 1, ExitFlag);
            }
            return result;
        }

        /// <summary>
        /// Check compressed bytes with spicific FileStream
        /// </summary>
        /// <param name="ArchiveBMass">Compressed bytes</param>
        /// <param name="SourceFile">Source file for comparison</param>
        /// <param name="CurEvent">Wait handle. Current thread. Need to send it next thread</param>
        /// <param name="WaitEvent">Wait handle. Wait previous thread</param>
        /// <param name="WaitStartStream">Wait handle. Stop main thread while current not get all params</param>
        /// <param name="CountAllReads">Max reads. Need for display progress</param>
        /// <param name="IterationRead">Current read. Need for display progress</param>
        /// <param name="CountCurrentThreads">Dont get create threads more then CountThread</param>
        /// <param name="result">Output set in false if compression failed</param>
        private void PartialCheck(FileStream ArchiveFile, FileStream SourceFile, ManualResetEvent CurEvent
            , ManualResetEvent WaitStartStream, int CountAllReads, ref int IterationRead, ref bool result)
        {
            ManualResetEvent WaitEventRead = new ManualResetEvent(false);
            ManualResetEvent CurEventRead = new ManualResetEvent(false);
            byte[] ArchiveBMass;

            //informate main stream about getting all params
            WaitStartStream.Set();

            try
            {
                while (ArchiveFile.MoveToNextGZip(out ArchiveBMass, ReadLocker, ref CurEventRead, ref PrevEvent, ref WaitEventRead))
                {
                    byte[] SourceBMass;
                    int bytesRead = 0;
                    int AllBytesRead = 0;
                    using (MemoryStream ArchiveBuffer = new MemoryStream(ArchiveBMass))
                    {
                        using (GZipStream Decompress = new GZipStream(ArchiveBuffer, CompressionMode.Decompress, true))
                        {
                            byte[] DecompressedArchiveBMass = new byte[CountBytesOneTimeRead];
                            //read compressed part while it not ends, concatinate all read result if reads more then once
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
                            result = false;
                            CurEventRead.Set();
                            break;
                        }

                        //compare decompressed thread and original file
                        lock (WriteLocker)
                        {
                            SourceBMass = new byte[ArchiveBMass.Length < 4 ? 4 : ArchiveBMass.Length];
                            SourceFile.Read(SourceBMass, 0, AllBytesRead);
                            //if find not equal block then exit with false
                            if (BitConverter.ToInt32(SourceBMass, 0) != BitConverter.ToInt32(ArchiveBMass, 0))
                            {
                                result = false;
                                LoadIndication(1, 1, ExitFlag);
                                ExitFlag = true;
                                CurEventRead.Set();
                                break;
                            }
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
                result = false;
                ShowErrorMessage(ex, "Check compress or decompress files identity");
            }
        }
    }
}
