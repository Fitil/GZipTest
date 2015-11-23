using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;

namespace GZipTest
{
    partial class Program
    {
        /// <summary>
        /// Check and correct agruments from console
        /// </summary>
        /// <param name="args">Arguments to check. From main args</param>
        /// <returns>return true if all arguments correct</returns>
        public static bool CheckArgs(ref string[] args)
        {
            switch (args.Count())
            {
                case 2:
                    Console.WriteLine("Destination file not set. Save result operation in source file folder?\npress Y for accept and N for decline");
                    string YorN = Console.ReadLine().ToUpper();
                    if (YorN == "Y")
                    {
                        string[] bufferArgs = new string[3];
                        bufferArgs[0] = args[0];
                        bufferArgs[1] = args[1];
                        if (Path.GetExtension(args[1]) == ".gz")
                        {
                            bufferArgs[2] = bufferArgs[1].Substring(0, bufferArgs[1].Length - 3);
                        }
                        else
                        {
                            bufferArgs[2] = bufferArgs[1] + ".gz";
                        }
                        args = bufferArgs;
                    }
                    else
                        return false;
                    break;
                case 3:
                    //number of arguments is correct
                    break;
                default:
                    //number of arguments is not correct
                    Console.WriteLine("Error. Please check arguments");
                    return false;
            }
            if (args.Count() != 3)
            {
                Console.WriteLine("Count arguments must be equal 3");
                return false;
            }
            switch (args[0])
            {
                case "compress":
                    #region Compress check logic
                    if (!File.Exists(args[1]))
                    {
                        Console.WriteLine("File path not correct or file does not exist");
                        return false;
                    }
                    if (Path.GetExtension(args[1]) == ".gz")
                    {
                        Console.WriteLine("File already compressed");
                        return false;
                    }
                    if (Path.GetExtension(args[2]) != ".gz")
                    {
                        Console.WriteLine("Archive file not in .gz format\nSet format arhive file as .gz?\npress Y for accept and N for decline");
                        string YorN = Console.ReadLine().ToUpper();
                        if (YorN == "Y")
                            args[2] = args[2] + ".gz";
                        else
                            return false;
                    }
                    if (File.Exists(args[2]))
                    {
                        Console.WriteLine("Archive file already exist");
                        return false;
                    }
                    if (!CheckWritePermissionOnDir(Path.GetDirectoryName(args[2])))
                    {
                        Console.WriteLine("Dont have write permission for archive file directory");
                        return false;
                    }
                    #endregion
                    break;
                case "decompress":
                    #region Decompress check logic
                    if (!File.Exists(args[1]))
                    {
                        Console.WriteLine("File path not correct or file does not exist");
                        return false;
                    }
                    if (Path.GetExtension(args[1]) != ".gz")
                    {
                        Console.WriteLine("File not compressed");
                        return false;
                    }
                    if (File.Exists(args[2]))
                    {
                        Console.WriteLine("Decompressed file already exist");
                        return false;
                    }
                    if (!CheckWritePermissionOnDir(Path.GetDirectoryName(args[2])))
                    {
                        Console.WriteLine("Dont have write permission for decompressed file directory");
                        return false;
                    }
                    #endregion
                    break;
                case "perfomance":
                    #region Perfomance check logic
                    if (!File.Exists(args[1]))
                    {
                        Console.WriteLine("File path not correct or file does not exist");
                        return false;
                    }
                    if (Path.GetExtension(args[1]) == ".gz")
                    {
                        Console.WriteLine("File already compressed");
                        return false;
                    }
                    if (Path.GetExtension(args[2]) != ".gz")
                    {
                        Console.WriteLine("Archive file not in .gz format\nSet format arhive file as .gz?\npress Y for accept and N for decline");
                        string YorN = Console.ReadLine().ToUpper();
                        if (YorN == "Y")
                            args[2] = args[2] + ".gz";
                        else
                            return false;
                    }
                    if (File.Exists(args[2]))
                    {
                        Console.WriteLine("Archive file already exist");
                        return false;
                    }
                    if (!CheckWritePermissionOnDir(Path.GetDirectoryName(args[2])))
                    {
                        Console.WriteLine("Dont have write permission for archive file directory");
                        return false;
                    }
                    #endregion
                    break;
                default:
                    Console.WriteLine("Please choose type operation in first argument\nSupport types only - compress, decompress, perfomance");
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Check permissions for dirictory by path
        /// </summary>
        /// <param name="path">Path to directory</param>
        /// <returns>retrun true is user have write permission</returns>
        private static bool CheckWritePermissionOnDir(string path)
        {
            bool writeAllow = false;
            bool writeDeny = false;

            if (!Directory.Exists(path))
                return false;

            DirectorySecurity accessControlList = Directory.GetAccessControl(path);
            if (accessControlList == null)
                return false;

            AuthorizationRuleCollection accessRules = accessControlList.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
            if (accessRules == null)
                return false;

            //search in every rule write access or deny 
            foreach (FileSystemAccessRule rule in accessRules)
            {
                //if rule not connected with write permission go to next rule
                if ((FileSystemRights.Write & rule.FileSystemRights) != FileSystemRights.Write)
                    continue;

                //check writeAllow and writeDeny flags
                if (rule.AccessControlType == AccessControlType.Allow)
                    writeAllow = true;
                else if (rule.AccessControlType == AccessControlType.Deny)
                    writeDeny = true;
            }

            return writeAllow && !writeDeny;
        }
    }
}
