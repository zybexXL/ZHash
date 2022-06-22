using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ZHash
{
    class Program
    {
        static Version version = new Version(1, 0, 8);
        static ConsoleColor DefaultColor = Console.ForegroundColor;

        static bool quiet;
        static CmdOption algo;
        static ZHashManager manager;

        internal static bool DEBUG = false;

        static int Main(string[] args)
        {
            int result = 0;
            Console.WriteLine($"ZHash v{version} - Checksum tool (c) 2022 Pedro Fonseca\n");
            if (CmdLine.Parse(args, out result))
            {

                algo = CmdLine.FirstOrDefault(CmdOption.SHA1, CmdOption.SHA256, CmdOption.MD5);
                quiet = CmdLine.hasOption(CmdOption.Quiet) || Console.IsOutputRedirected;
                DEBUG = CmdLine.hasOption(CmdOption.Debug);

                manager = new ZHashManager();

                switch (CmdLine.RunMode)
                {
                    case CmdOption.Compute:
                    case CmdOption.Update:
                        result = Compute();
                        break;

                    case CmdOption.Verify:
                        result = Verify();
                        break;

                    case CmdOption.Stdin:
                        result = HashStdin();
                        break;

                    case CmdOption.Register:
                        result = Register();
                        break;

                    case CmdOption.Bench:
                        result = Benchmark.Bench();
                        break;

                }
                manager.Close();
                Console.ResetColor();
            }

            if (CmdLine.hasOption(CmdOption.WaitIf) && result == 0)
            {
                //Console.WriteLine("\nAll done, no errors");
                Thread.Sleep(2000);
            }

            if (CmdLine.hasOption(CmdOption.Wait) || (CmdLine.hasOption(CmdOption.WaitIf) && result != 0))
            {
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
            return result;
        }

        static int Compute()
        {
            int result = 0;
            int count = 0;
            string dbName = Path.GetFileName(manager.dbPath ?? "").ToLower();
            bool onlyNew = CmdLine.hasOption(CmdOption.New);
            bool refresh = CmdLine.hasOption(CmdOption.Refresh);

            bool cancel = false;
            Console.CancelKeyPress += (o, args) => { args.Cancel = true; cancel = true; };

            foreach (var path in CmdLine.Paths)
            {
                foreach (var file in manager.EnumerateFiles(path, CmdLine.Excludes))
                {
                    if (file.Name.ToLower() == dbName && path.ToLower() != dbName)
                        continue;

                    if (onlyNew && manager.GetHash(file.FullName) != null)
                    {
                        PrintDebug($"** [-n] Skipping {file.FullName}");
                        continue;
                    }

                    if (refresh && manager.GetHash(file.FullName) == null)
                    {
                        PrintDebug($"** [-r] Skipping {file.FullName}");
                        continue;
                    }

                    count++;
                    if (!quiet) Print($"  ...  {file.Name}\r");
                    byte[] hashdata = ComputeHash(file.FullName, algo);
                    ZHashItem item = manager.Update(file.FullName, algo.ToString(), hashdata);
                    if (item == null || hashdata == null)
                    {
                        item = new ZHashItem($"Hashing failed: {file.FullName}");
                        result = 1;
                    }
                    if (!quiet || Console.IsOutputRedirected)
                        PrintLine(item.ToString(), item.isInvalid ? ConsoleColor.Red : ConsoleColor.Cyan);

                    if (cancel) break;
                }
                if (cancel)
                {
                    PrintLine("\nCTRL+C pressed", ConsoleColor.Red); 
                    break;
                }
            }
            if (!quiet && count == 0)
            {
                PrintLine("No matching files found, nothing done.");
                result = 8;
            }

            return result;
        }

        static int Verify()
        {
            int result = 0;
            int count = 0;
            string dbName = Path.GetFileName(manager.dbPath ?? "").ToLower();
            foreach (var path in CmdLine.Paths)
                foreach (var file in manager.EnumerateFiles(path, CmdLine.Excludes))
                {
                    if (file.Name.ToLower() == dbName && path.ToLower() != dbName)
                        continue;
                    count++;
                    string rel = Util.GetRelativePath(Environment.CurrentDirectory, file.FullName);
                    ZHashItem hashItem = manager.GetHash(file.FullName);
                    if (hashItem?.hashData == null)
                        PrintLine($"(new)  {rel}", ConsoleColor.Gray);
                    else if (!Enum.TryParse(hashItem.algorithm, true, out CmdOption vAlgo) || GetHasher(vAlgo) == null)
                    {
                        PrintLine($"ERROR: Unsupported hash '{hashItem.algorithm}': {rel}", ConsoleColor.Magenta);
                        result = 6;
                    }
                    else
                    {
                        if (!quiet) Print($"  ...  {file.Name}\r", ConsoleColor.DarkGray);
                        byte[] hashdata = ComputeHash(file.FullName, vAlgo);
                        if (hashdata == null)
                        {
                            PrintLine($"ERROR: Could not hash: {rel}", ConsoleColor.Magenta);
                            result = 1;
                        }
                        else
                        {
                            if (hashdata.SequenceEqual(hashItem.hashData))
                                PrintLine($"  OK!  {rel}", ConsoleColor.Green);
                            else
                            {
                                PrintLine($"FAIL!  {rel}", ConsoleColor.Red);
                                result = 2;
                            }
                        }
                    }
                }

            if (!quiet && count == 0)
            {
                PrintLine("No matching files found, nothing done.");
                result = 8;
            }

            return result;
        }

        internal static void Print(string text, ConsoleColor color = ConsoleColor.Black)
        {
            if (color == ConsoleColor.Black) color = DefaultColor;
            Console.ForegroundColor = color;
            Console.Write(text);
        }

        internal static void PrintDebug(string text)
        {
            if (DEBUG) Print(text + Environment.NewLine, ConsoleColor.DarkGray);
        }

        internal static void PrintLine(string text, ConsoleColor color = ConsoleColor.Black)
        {
            Print(text + Environment.NewLine, color);
        }

        internal static HashAlgorithm GetHasher(CmdOption hash)
        {
            switch (hash)
            {
                case CmdOption.MD5: return MD5.Create();
                case CmdOption.SHA1: return SHA1.Create();
                case CmdOption.SHA256: return SHA256.Create();
            }
            return null;
        }

        static int HashStdin()
        {
            int result = 0;
            try
            {
                if (!Console.IsInputRedirected && !quiet)
                    Console.WriteLine("Enter data to hash then press CTRL+C:");

                Console.CancelKeyPress += (o, args) => { args.Cancel = true; };

                HashAlgorithm hasher = GetHasher(algo);
                Stream stdin = Console.OpenStandardInput();
                hasher.ComputeHash(stdin);

                if (Console.CursorLeft > 0) Console.WriteLine();
                PrintLine($"{algo}:{Util.Hexify(hasher.Hash)}");
            }
            catch (Exception ex)
            {
                PrintDebug($"Exception processing STDIN:\n{ex.Message}");
                result = 99;
            }
            return result;
        }

        static byte[] ComputeHash(string path, CmdOption algo)
        {
            try
            {
                FileInfo fi = new FileInfo(path);
                Stopwatch sw = DEBUG ? Stopwatch.StartNew() : null;
                HashAlgorithm hasher = GetHasher(algo);

                int bufSize = 1 << 20;
                int queues = 4;
                byte[] bufferA = new byte[bufSize * queues];
                byte[] bufferB = new byte[bufSize * queues];
                Task<int>[] readers = new Task<int>[4];

                using (Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufSize, true))
                {
                    // read ahead into bufferA
                    for (int i = 0; i < queues; i++)
                        readers[i] = stream.ReadAsync(bufferA, i * bufSize, bufSize);
                    
                    int running = queues;
                    while (running > 0)
                    {
                        // process bufferA, read ahead into bufferB
                        for (int i = 0; i < queues; i++)
                        {
                            if (readers[i] == null) continue;
                            readers[i].Wait();
                            int bytes = readers[i].Result;
                            if (bytes == bufSize)
                                readers[i] = stream.ReadAsync(bufferB, i * bufSize, bufSize);
                            else
                            {
                                running--;
                                readers[i] = null;
                            }
                            if (bytes > 0)
                                hasher.TransformBlock(bufferA, i * bufSize, bytes, bufferA, i * bufSize);
                        }

                        // process bufferB, read ahead into bufferA
                        for (int i = 0; i < queues; i++)
                        {
                            if (readers[i] == null) continue;
                            readers[i].Wait();
                            int bytes = readers[i].Result;
                            if (bytes == bufSize)
                                readers[i] = stream.ReadAsync(bufferA, i * bufSize, bufSize);
                            else
                            {
                                running--;
                                readers[i] = null;
                            }
                            if (bytes > 0)
                                hasher.TransformBlock(bufferB, i * bufSize, bytes, bufferB, i * bufSize);
                        }
                    }
                }
                
                hasher.TransformFinalBlock(bufferA, 0, 0);
                sw.Stop();
                if (DEBUG)
                {
                    long KB = fi.Length / 1024;
                    if (KB == 0) KB = 1;
                    double secs = sw.ElapsedMilliseconds/1000.0;
                    if (secs == 0) secs = 1;
                    PrintDebug($"\n** Hashed {KB} KiB in {secs:f2} seconds = {KB/secs:f2} KiB/sec");
                }
                return hasher.Hash;
            }
            catch { }
            return null;
        }

        static int Register()
        {
            int result = 0;
            string ext = CmdLine.Paths.Count > 0 ? CmdLine.Paths[0] : Constants.ZHEXT;

            if (CmdLine.Paths.Count > 1)
            {
                Console.WriteLine("Please register only one extension at a time");
                result = 7;
            }
            else if (!Regex.IsMatch(ext, @"^\.?\w+$"))
            {
                Console.WriteLine($"Cannot register invalid extension: {ext}");
                result = 7;
            }
            else
            {
                try
                {
                    if (!ext.StartsWith(".")) ext = "." + ext;
                    string zhash = Assembly.GetExecutingAssembly().Location;
                    string args = "-v -f \"%1\"";
                    string progID = "ZHashFile";

                    foreach (CmdOption opt in Enum.GetValues(typeof(CmdOption)))
                        if (CmdLine.hasOption(opt) && opt != CmdOption.File && opt != CmdOption.Register)
                            args = $"{args} -{opt.ToString().ToLower()}";

                    foreach (var exclude in CmdLine.Excludes)
                    {
                        string value = exclude.Contains(' ') ? $"\"{exclude}\"" : exclude;
                        args = $"{args} -x {value}";
                    }

                    using (RegistryKey root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
                    {
                        using (var key = root.CreateSubKey($"Software\\Classes\\{ext}\\OpenWithProgIDs", true))
                            key.SetValue($"{progID}", new byte[0], RegistryValueKind.None);
                        using (var key = root.CreateSubKey($"Software\\Classes\\{progID}", true))
                            key.SetValue("", "ZHash store");
                        using (var key = root.CreateSubKey($"Software\\Classes\\{progID}\\DefaultIcon", true))
                            key.SetValue("", $"{zhash},0");
                        using (var key = root.CreateSubKey($"Software\\Classes\\{progID}\\Shell\\Open\\Command", true))
                            key.SetValue("", $"\"{zhash}\" {args}");
                    }

                    // notify windows
                    Util.SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
                    Console.WriteLine($"Extension registered:\n{ext} = \"{zhash}\" {args}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to register extension '{ext}':\n{ex.Message}");
                    result = 99;
                }
            }
            return result;
        }
    }
}
