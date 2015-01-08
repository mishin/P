﻿namespace CheckP
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class Checker
    {
        private const int BufferSize = 1024;

        private const string RunPcOption = "runPc";
        private const string RunZingerOption = "runZing";
        private const string RunPrtOption = "runPrt";
        private const string AcceptOption = "acc";
        private const string IncludePcOption = "incPc";
        private const string IncludeZingerOption = "incZing";
        private const string IncludePrtOption = "incPrt";
        private const string AddOption = "add";
        private const string IgnorePromptOption = "igp";
        private const string DescrOption = "dsc";
        private const string ArgsPcOption = "argPc";
        private const string ArgsZingerOption = "argZing";
        private const string ArgsPrtOption = "argPrt";
        private const string TestOption = "cfg";
        private const string DelOption = "del";

        private const string TmpStreamFile = "check-tmp.txt";
        private const string AccFiles = "acc*.txt";
        private const string AccPrefix = "acc";
        private const string AccExt = ".txt";
        private const string LogFile = "check-output.log";
        private const string RuntimeFile1 = "program.c";
        private const string RuntimeFile2 = "program.h";
        private const string RuntimeFile3 = "stubs.c";

        private static readonly string[] AllOptions = new string[]
        {
            RunPcOption,
            RunZingerOption,
            RunPrtOption,
            AcceptOption,
            IncludePcOption,
            IncludeZingerOption,
            IncludePrtOption,
            AddOption,
            IgnorePromptOption,
            DescrOption,
            ArgsPcOption,
            ArgsZingerOption,
            ArgsPrtOption,
            TestOption,
            DelOption
        };

        private string activeDirectory;
        private bool reset = false;

        public string Description
        {
            get;
            private set;
        }

        public Checker(string activeDirectory, bool reset)
        {
            this.activeDirectory = activeDirectory;
            this.reset = reset;
        }

        public static void PrintUsage()
        {
            Console.WriteLine(
                "USAGE: CheckP -{0}: pc.exe -{1}: zinger.exe -{2}: prtMain.exe -{3}: dir [-{10}: args] [-{11}: args] [-{12}: args] [-{4}: files] [-{5}: files] [-{6}: files] [-{14}: files] [-{13}: file] [-{7}] [-{8}] [-{9}: descriptors]",
                RunPcOption,
                RunZingerOption,
                RunPrtOption,
                AcceptOption,
                IncludePcOption,
                IncludeZingerOption,
                IncludePrtOption,
                AddOption,
                IgnorePromptOption,
                DescrOption,
                ArgsPcOption,
                ArgsZingerOption,
                ArgsPrtOption,
                TestOption,
                DelOption
            );

            Console.WriteLine();
            Console.WriteLine("-{0}\tpc.exe to run", RunPcOption);
            Console.WriteLine("-{0}\tzinger.exe to run", RunZingerOption);
            Console.WriteLine("-{0}\tprtMain.exe to run", RunPrtOption);
            Console.WriteLine("-{0}\tA directory containing acceptable outputs", AcceptOption);
            Console.WriteLine("-{0}\tA list of arguments to pc.exe", ArgsPcOption);
            Console.WriteLine("-{0}\tA list of arguments to zinger.exe", ArgsZingerOption);
            Console.WriteLine("-{0}\tA list of arguments to tester.exe", ArgsPrtOption);
            Console.WriteLine("-{0}\tA list of files that should be included as output for pc.exe", IncludePcOption);
            Console.WriteLine("-{0}\tA list of files that should be included as output for zinger.exe", IncludeZingerOption);
            Console.WriteLine("-{0}\tA list of files that should be included as output for prt.exe", IncludePrtOption);
            Console.WriteLine("-{0}\tA list of files that should be deleted before running", DelOption);
            Console.WriteLine("-{0}\tA test file with additional configuration", TestOption);
            Console.WriteLine("-{0}\tAdds the output of this run to set of acceptable outputs", AddOption);
            Console.WriteLine("-{0}\tIgnore output sent to the prompt by commands", IgnorePromptOption);
            Console.WriteLine("-{0}\tDescriptions of this test", DescrOption);
        }

        /// <summary>
        /// Runs check using command line arguments
        /// </summary>
        /// <returns></returns>
        public bool Check()
        {
            Options opts;
            int errPos;
            string cmdStr;
            if (!OptionParser.Parse(out opts, out errPos, out cmdStr))
            {
                Console.WriteLine("ERROR: Could not parse command line");
                Console.WriteLine("INPUT: {0}", cmdStr);
                Console.WriteLine("POS  : {0}^", errPos == 0 ? string.Empty : new string(' ', errPos));
                Console.WriteLine();
                PrintUsage();
                return false;
            }

            bool isTestSet, result = true;
            Tuple<OptValueKind, object>[] testFile;
            result = ValidateOption(opts, TestOption, true, 1, 1, out isTestSet, out testFile) && result;
            if (isTestSet)
            {
                result = opts.LoadMore(activeDirectory, (string)testFile[0].Item2) && result;
                if (result)
                {
                    activeDirectory = new FileInfo(Path.Combine(activeDirectory, (string)testFile[0].Item2)).DirectoryName;
                }
            }

            return result && Check(opts);
        }

        public bool Check(string testfile)
        {
            var opts = new Options();
            if (!opts.LoadMore(activeDirectory, testfile))
            {
                return false;
            }

            return Check(opts);
        }

        private bool Check(Options opts)
        {
            bool isSet, result = true;
            bool isSetExePc;
            Tuple<OptValueKind, object>[] exePc;
            result = ValidateOption(opts, RunPcOption, true, 1, 1, out isSetExePc, out exePc) && result;

            //TODO: Do I need a separate isSetXX for each of pc, zinger, prt? 
            //Why was the same isSet used for for "exe" and "acc" options?
            bool isSetExeZinger;
            Tuple<OptValueKind, object>[] exeZinger;
            result = ValidateOption(opts, RunZingerOption, true, 1, 1, out isSetExeZinger, out exeZinger) && result;

            bool isSetExePrt;
            Tuple<OptValueKind, object>[] exePrt;
            result = ValidateOption(opts, RunPrtOption, true, 1, 1, out isSetExePrt, out exePrt) && result;

            Tuple<OptValueKind, object>[] accDir;
            result = ValidateOption(opts, AcceptOption, false, 1, 1, out isSet, out accDir) && result;

            bool isInclPc;
            Tuple<OptValueKind, object>[] includesPc;
            result = ValidateOption(opts, IncludePcOption, true, 1, int.MaxValue, out isInclPc, out includesPc) &&
                     result;

            bool isInclZinger;
            Tuple<OptValueKind, object>[] includesZinger;
            result =
                ValidateOption(opts, IncludeZingerOption, true, 1, int.MaxValue, out isInclZinger, out includesZinger) &&
                result;

            bool isInclPrt;
            Tuple<OptValueKind, object>[] includesPrt;
            result = ValidateOption(opts, IncludePrtOption, true, 1, int.MaxValue, out isInclPrt, out includesPrt) &&
                     result;

            bool isArgsPc;
            Tuple<OptValueKind, object>[] pcArgs;
            result = ValidateOption(opts, ArgsPcOption, true, 1, int.MaxValue, out isArgsPc, out pcArgs) && result;

            bool isArgsZinger;
            Tuple<OptValueKind, object>[] zingerArgs;
            result = ValidateOption(opts, ArgsZingerOption, true, 1, int.MaxValue, out isArgsZinger, out zingerArgs) &&
                     result;

            bool isArgsPrt;
            Tuple<OptValueKind, object>[] prtArgs;
            result = ValidateOption(opts, ArgsPrtOption, true, 1, int.MaxValue, out isArgsPrt, out prtArgs) && result;

            bool isAdd;
            Tuple<OptValueKind, object>[] values;
            result = ValidateOption(opts, AddOption, true, 0, 0, out isAdd, out values) && result;
            //If CheckP is called from Test with "reset" option, override isAdd:
            if (!isAdd && this.reset) isAdd = true;

            bool isIgnPrmpt;
            result = ValidateOption(opts, IgnorePromptOption, true, 0, 0, out isIgnPrmpt, out values) && result;

            bool isDel;
            Tuple<OptValueKind, object>[] delFiles;
            result = ValidateOption(opts, DelOption, true, 1, int.MaxValue, out isDel, out delFiles) && result;

            bool isDescr;
            Tuple<OptValueKind, object>[] descrs;
            result = ValidateOption(opts, DescrOption, true, 1, int.MaxValue, out isDescr, out descrs) && result;

            string[] unknownOpts;
            if (opts.TryGetOptionsBesides(AllOptions, out unknownOpts))
            {
                foreach (var uo in unknownOpts)
                {
                    Console.WriteLine("ERROR: -{0} is not a legal option", uo);
                }

                result = false;
            }

            if (!result)
            {
                Console.WriteLine();
                PrintUsage();
                return false;
            }

            if (isDescr)
            {
                var fullDescr = "";
                foreach (var v in descrs)
                {
                    fullDescr += v.Item2.ToString() + " ";
                }

                Description = fullDescr;
                Console.WriteLine("*********** Checking {0}***********", fullDescr);
            }

            if (isDel)
            {
                foreach (var df in delFiles)
                {
                    try
                    {
                        var dfi = new FileInfo(Path.Combine(activeDirectory, df.Item2.ToString()));
                        if (dfi.Exists)
                        {
                            Console.WriteLine("DEL: Deleted file {0}", dfi.FullName);
                            dfi.Attributes = FileAttributes.Normal;
                            dfi.Delete();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(
                            "Error deleting file {0} - {1}",
                            df.Item2,
                            e.Message);
                    }
                }
            }

            StreamWriter tmpWriter;
            if (!OpenTmpStream(out tmpWriter))
            {
                return false;
            }

            //debudding only?
            Console.WriteLine("Running test under {0}...", activeDirectory);

            //If isAdd is true, remove old acceptor files
            //Note: this will break the logic of multiple acceptors;
            //if in the future multiple acceptors are needed, re-implement this feature, for example:
            //in adition to the "add" option, add option "reset" for CheckP;
            //testP.bat will also have two alternative options: "reset" and "add";
            //only delete acceptors for "reset" option, but not for "add" option
            
            const string acceptorFilePattern = "acc_*.txt";
            DirectoryInfo di = new DirectoryInfo(activeDirectory);
            if (isAdd)
            {
                foreach (var acci in di.EnumerateFiles(acceptorFilePattern))
                {
                    File.Delete(Path.Combine(activeDirectory, acci.FullName));                 
                }
            }       

            try
            {
                //Run components of the P tool chain specified in options:
                if (isSetExePc)
                {
                    bool pcResult = Run(tmpWriter, isIgnPrmpt, exePc[0].Item2.ToString(), pcArgs);
                    if (!pcResult)
                    {
                        result = false;
                    }
                    else if (isInclPc && !AppendIncludes(tmpWriter, includesPc))
                    {
                        result = false;
                    }
                }
                //Run Zinger if isSetExeZinger and: (a) pc.exe run and no errors from pc.exe; or (b) pc.exe was not set to run
                //if (isSetExeZinger && result || !isSetExePc)
                if (isSetExeZinger)
                {
                    //TODO: since Zinger returns "true" when *.dll file is missing, catch this case by explicitly 
                    //checking if files specified as  zinger arguments are present (unless Zinger is fixed
                    //and returns "false" for such errors).
                    //The error message should give a tip: "Make sure pc.exe was called and run successfully"
                    bool zingerResult = Run(tmpWriter, isIgnPrmpt, exeZinger[0].Item2.ToString(), zingerArgs);

                    //debug:
                    //Console.WriteLine("Zinger returned: {0}", zingerResult);

                    if (!zingerResult)
                    {
                        result = false;
                    }
                    else if (isInclZinger && !AppendIncludes(tmpWriter, includesZinger))
                    {
                        result = false;
                    }
                }

                //Run runtime:
                //Console.WriteLine("isSetExePrt is {0}", isSetExePrt);
                if (isSetExePrt)
                {
                    //Console.WriteLine("activeDirectory is: {0}", activeDirectory);
                    //activeDirectory is "...\Prt", but runtime files are under
                    //"..\\Pc" (pcDirectory):
                    var temp = new DirectoryInfo(activeDirectory);
                    string parentFolder = temp.Parent.FullName;
                    var pcDirectory = String.Concat(parentFolder, "\\");
                    pcDirectory = String.Concat(pcDirectory, "Pc");
                    //Console.WriteLine("pcDirectory is: {0}", pcDirectory);

                    //Compute "TesterDirectory" (Tst\PrtTester):
                    //path to ...PrtTester\Debug\tester.exe:
                    var testerExePath = exePrt[0].Item2.ToString();
                    //Console.WriteLine("testerExePath is: {0}", testerExePath);
                    var testerExeAbsPath = Path.GetFullPath(testerExePath);
                    //This is the correct full path if the relative path to tester.exe in testconfigPrt.exe is:
                    //runPrt: ..\..\..\..\..\..\..\PLanguage\Plang\Tst\PrtTester\Debug\Tester.exe
                    //Console.WriteLine("abs path name for tester.exe: {0}", testerExeAbsPath);
                    var testerExeDirectoryInfo = (new FileInfo(testerExeAbsPath)).Directory;
                    //Console.WriteLine("testerExe directory is: {0}", testerExeDirectoryInfo.FullName);
                    var testerDirectory = testerExeDirectoryInfo.Parent.FullName;
                    //Console.WriteLine("testerDirectory is: {0}", testerDirectory);

                    //Remove previous runtime files from Tst\PrtTester:
                    File.Delete(Path.Combine(testerDirectory, RuntimeFile1));
                    File.Delete(Path.Combine(testerDirectory, RuntimeFile2));
                    File.Delete(Path.Combine(testerDirectory, RuntimeFile3));

                    //Copy current runtime files generated by Pc.exe to Tst\PrtTester:
                    File.Copy(
                        Path.Combine(pcDirectory, RuntimeFile1),
                        Path.Combine(testerDirectory, RuntimeFile1)
                        );
                    File.Copy(
                        Path.Combine(pcDirectory, RuntimeFile2),
                        Path.Combine(testerDirectory, RuntimeFile2)
                        );
                    File.Copy(
                        Path.Combine(pcDirectory, RuntimeFile3),
                        Path.Combine(testerDirectory, RuntimeFile3)
                        );

                    bool prtResult = Run(tmpWriter, isIgnPrmpt, exePrt[0].Item2.ToString(), prtArgs);
                    if (!prtResult)
                    {
                        result = false;
                    }
                    else if (isInclPrt && !AppendIncludes(tmpWriter, includesPrt))
                    {
                        result = false;
                    }
                    
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(
                    "ERROR running P tool: {0}", e.Message);
                return false;
            }

            if (!CloseTmpStream(tmpWriter))
            {
                result = false;
            }

            if (result && !CompareAcceptors(accDir[0].Item2.ToString(), isAdd))
            {
                File.Delete(Path.Combine(activeDirectory, LogFile));
                File.Copy(
                    Path.Combine(activeDirectory, TmpStreamFile),
                    Path.Combine(activeDirectory, LogFile));
                Console.WriteLine("LOGGED: Saved bad output to {0}",
                    Path.Combine(activeDirectory, LogFile));

                result = false;
            }

            if (!DeleteTmpFile())
            {
                result = false;
            }

            if (result)
            {
                Console.WriteLine("SUCCESS: Output matched");
            }

            return result;
        }

        private static bool ValidateOption(
            Options opts,
            string opt,
            bool isOptional,
            int nArgsMin,
            int nArgsMax,
            out bool isSet,
            out Tuple<OptValueKind, object>[] values)
        {
            isSet = opts.TryGetOption(opt, out values);
            if (!isSet && !isOptional)
            {
                Console.WriteLine("ERROR: -{0} option not provided", opt);
                return false;
            }
            else if (isSet && (values.Length < nArgsMin || values.Length > nArgsMax))
            {
                Console.WriteLine("ERROR: -{0} option has wrong number of arguments", opt);
                return false;
            }

            return true;
        }

        private bool OpenTmpStream(out StreamWriter wr)
        {
            wr = null;
            try
            {
                wr = new StreamWriter(Path.Combine(activeDirectory, TmpStreamFile));
            }
            catch (Exception e)
            {
                Console.WriteLine(
                    "ERROR: Could not open temporary file {0} - {1}",
                    TmpStreamFile,
                    e.Message);
                return false;
            }

            return true;
        }

        private static bool CloseTmpStream(StreamWriter wr)
        {
            try
            {
                wr.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(
                    "ERROR: Could not close temporary file {0} - {1}",
                    TmpStreamFile,
                    e.Message);
                return false;
            }

            return true;
        }

        private bool DeleteTmpFile()
        {
            try
            {
                var fi = new FileInfo(Path.Combine(activeDirectory, TmpStreamFile));
                if (fi.Exists)
                {
                    fi.Delete();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(
                    "ERROR: Could not delete temporary file {0} - {1}",
                    TmpStreamFile,
                    e.Message);
                return false;
            }

            return true;
        }

        private bool AppendIncludes(StreamWriter outStream,
                                           Tuple<OptValueKind, object>[] includes)
        {
            foreach (var inc in includes)
            {
                outStream.WriteLine();
                outStream.WriteLine("=================================");
                outStream.WriteLine("{0}", inc.Item2.ToString());
                outStream.WriteLine("=================================");

                try
                {
                    using (var sr = new StreamReader(Path.Combine(activeDirectory, inc.Item2.ToString())))
                    {
                        while (!sr.EndOfStream)
                        {
                            outStream.WriteLine(sr.ReadLine());
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("ERROR: Could not include {0} - {1}", inc.Item2.ToString(), e.Message);
                    return false;
                }
            }

            return true;
        }

        private bool CompareAcceptors(string accDir, bool add)
        {
            var tmpFile = Path.Combine(activeDirectory, TmpStreamFile);
            try
            {
                var di = new DirectoryInfo(Path.Combine(activeDirectory, accDir));
                if (!di.Exists)
                {
                    Console.WriteLine("ERROR: Acceptor directory {0} does not exist", accDir);
                    return false;
                }

                var hashSet = new HashSet<string>();
                foreach (var fi in di.EnumerateFiles(AccFiles))
                {
                    hashSet.Add(fi.Name);
                    if (!IsDifferent(fi.FullName, tmpFile))
                    {
                        return true;
                    }
                }

                if (add)
                {
                    var nextId = 0;
                    string name = "";
                    while (hashSet.Contains(name = string.Format("{0}_{1}{2}", AccPrefix, nextId, AccExt)))
                    {
                        ++nextId;
                    }

                    File.Copy(
                        Path.Combine(activeDirectory, TmpStreamFile),
                        Path.Combine(Path.Combine(activeDirectory, accDir), name));
                    return true;
                }
                else
                {
                    Console.WriteLine("ERROR: Output is not accepted");
                    return false;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: Could not compare acceptors - {0}", e.Message);
                return false;
            }
        }

        private bool Run(
            StreamWriter outStream,
            bool ignorePrompt,
            string exe,
            Tuple<OptValueKind, object>[] values)
        {
            var args = "";
            if (values != null)
            {
                foreach (var v in values)
                {
                    args += v.Item2.ToString() + " ";
                }
            }

            if (!ignorePrompt)
            {
                outStream.WriteLine("=================================");
                outStream.WriteLine("         Console output          ");
                outStream.WriteLine("=================================");
            }

            try
            {
                var psi = new ProcessStartInfo();
                psi.UseShellExecute = false;
                psi.RedirectStandardError = true;
                psi.RedirectStandardOutput = true;
                psi.WorkingDirectory = activeDirectory;
                psi.FileName = Path.Combine(activeDirectory, exe);
                psi.Arguments = args.Trim();
                psi.CreateNoWindow = true;

                var process = new Process();
                process.StartInfo = psi;
                process.OutputDataReceived += (s, e) => OutputReceived(outStream, ignorePrompt, s, e);
                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                process.WaitForExit();

                Console.WriteLine("EXIT: {0}", process.ExitCode);
                outStream.WriteLine("EXIT: {0}", process.ExitCode);
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: Failed to run command: {0}", e.Message);
                return false;
            }

            return true;
        }

        private static void OutputReceived(
            StreamWriter outStream,
            bool ignorePrompt,
            object sender,
            DataReceivedEventArgs e)
        {
            if (ignorePrompt)
            {
                Console.WriteLine("OUT: {0}", e.Data);
            }
            else
            {
                outStream.WriteLine("OUT: {0}", e.Data);
            }
        }

        private static bool IsDifferent(string file1, string file2)
        {
            int read1, read2;
            var buf1 = new char[BufferSize];
            var buf2 = new char[BufferSize];
            try
            {
                using (var sr1 = new StreamReader(file1))
                {
                    using (var sr2 = new StreamReader(file2))
                    {
                        while (true)
                        {
                            read1 = sr1.ReadBlock(buf1, 0, BufferSize);
                            read2 = sr2.ReadBlock(buf2, 0, BufferSize);
                            if (read1 != read2)
                            {
                                return true;
                            }

                            for (int i = 0; i < read1; ++i)
                            {
                                if (buf1[i] != buf2[i])
                                {
                                    return true;
                                }
                            }

                            if (read1 == 0)
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            catch
            {
                return true;
            }
        }
    }
}
