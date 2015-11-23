using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Security.AccessControl;
using System.Threading;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Configuration;

namespace GZipTest
{
    partial class Program
    {
        static void Main(string[] args)
        {
            ManualResetEvent MainEvent = new ManualResetEvent(false);
            bool Result = false;
            bool DeleteFileFlag = false;
            //workaround for remove event bug
            //bug: after the handler was removed for the first time, installing it again has no effect
            Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e) { e.Cancel = false; };

            if (CheckArgs(ref args))
            {
                switch (args[0])
                {
                    case "compress":
                        #region compress logic
                        DeleteFileFlag = true;
                        using (Compress comp = new Compress(MainEvent))
                        {
                            //Start compress and add new event for exit
                            Console.CancelKeyPress += new ConsoleCancelEventHandler(comp.ExitEvent);
                            try { comp.FullCompress(args[1], args[2]); }
                            catch (Exception ex) { comp.ShowErrorMessage(ex, "Try compress file"); }

                            //Start check result if not exit
                            if (!comp.ExitFlag)
                            {
                                Console.CancelKeyPress -= new ConsoleCancelEventHandler(comp.ExitEvent);
                                comp.Dispose();
                                DeleteFileFlag = false;
                                using (Comparer chck = new Comparer(MainEvent))
                                {
                                    Console.CancelKeyPress += new ConsoleCancelEventHandler(chck.ExitEvent);
                                    try { Result = chck.FullCheck(args[1], args[2]); }
                                    catch (Exception ex) { chck.ShowErrorMessage(ex, "Check compress result"); }
                                }
                            }
                        }
                        #endregion
                        break;
                    case "decompress":
                        #region decompress logic
                        DeleteFileFlag = true;
                        using (Decompress decom = new Decompress(MainEvent))
                        {
                            //Start decompress and add new event for exit
                            Console.CancelKeyPress += new ConsoleCancelEventHandler(decom.ExitEvent);
                            try { decom.FullDecompress(args[1], args[2]); }
                            catch (Exception ex) { decom.ShowErrorMessage(ex, "Try decompress file"); }
                            
                            //Start check result if not exit
                            if (!decom.ExitFlag)
                            {
                                Console.CancelKeyPress -= new ConsoleCancelEventHandler(decom.ExitEvent);
                                decom.Dispose();
                                DeleteFileFlag = false;
                                using (Comparer chck = new Comparer(MainEvent))
                                {
                                    Console.CancelKeyPress += new ConsoleCancelEventHandler(chck.ExitEvent);
                                    try { Result = chck.FullCheck(args[2], args[1]); }
                                    catch (Exception ex) { chck.ShowErrorMessage(ex, "Check decompress result"); }
                                }
                            }
                        }
                        #endregion
                        break;
                    case "perfomance":
                        #region perfomance logic
                        DeleteFileFlag = true;
                        using (PerfomanceBalance perf = new PerfomanceBalance(MainEvent))
                        {
                            //Start perfomance and add new event for exit
                            Console.CancelKeyPress += new ConsoleCancelEventHandler(perf.ExitEvent);
                            try { perf.CalculatePerfomance(args[1], args[2]); }
                            catch (Exception ex) { perf.ShowErrorMessage(ex, "Calculate perfomance"); }
                            finally { Result = true; }
                        }
                        #endregion
                        break;
                    default:
                        break;
                }
            }

            if (!Result)
            {
                Console.WriteLine("Error while processing");
                if (DeleteFileFlag) File.Delete(args[2]);
            }

            Console.WriteLine("Result: {0}", Result ? "1" : "0");

            MainEvent.Set();
        }
    }

}
