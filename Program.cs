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
using System.Threading.Tasks;

namespace ZHash
{
    class Program
    {
        static Version version = new Version(1, 0, 2);
        static ConsoleColor DefaultColor = Console.ForegroundColor;

        static bool quiet;
        static CmdOption algo;
        static ZHashManager manager;

        internal static bool DEBUG = false;

        static int Main(string[] args)
        {
            Console.WriteLine($"ZHash v{version} - Checksum tool (c) 2022 Pedro Fonseca\n");
            if (!CmdLine.Parse(args, out int code))
                return code;

            algo = CmdLine.FirstOrDefault(CmdOption.SHA1, CmdOption.SHA256, CmdOption.MD5);
            quiet = CmdLine.hasOption(CmdOption.Quiet) || Console.IsOutputRedirected;
            DEBUG = CmdLine.hasOption(CmdOption.Debug);

            manager = new ZHashManager();

            switch (CmdLine.RunMode)
            {
                case CmdOption.Compute:
                case CmdOption.Update:
                    Compute();
                    break;

                case CmdOption.Verify:
                    Verify();
                    break;
                
                case CmdOption.Stdin:
                    HashStdin();
                    break;

                case CmdOption.Register:
                    Register();
                    break;

            }

            manager.Close();
            Console.ResetColor();

            if (CmdLine.hasOption(CmdOption.Wait) && CmdLine.RunMode != CmdOption.Register)
            {
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
            return 0;
        }

        static void Compute()
        {
            int count = 0;
            string dbName = Path.GetFileName(manager.dbPath ?? "").ToLower();
            bool onlyNew = CmdLine.hasOption(CmdOption.New);
            bool refresh = CmdLine.hasOption(CmdOption.Refresh);

            foreach (var path in CmdLine.Paths)
                foreach (var file in manager.EnumerateFiles(path, CmdLine.Excludes))
                {
                    if (file.Name.ToLower() == dbName && path.ToLower() != dbName)
                        continue;

                    if (onlyNew && manager.GetHash(file.FullName) != null)
                    {
                        PrintDebug($"** [-n] Skpping {file.FullName}");
                        continue;
                    }

                    if (refresh && manager.GetHash(file.FullName) == null)
                    {
                        PrintDebug($"** [-r] Skpping {file.FullName}");
                        continue;
                    }

                    count++;
                    if (!quiet) Print($"  ...  {file.Name}\r");
                    byte[] hashdata = ComputeHash(file.FullName);
                    ZHashItem item = manager.Update(file.FullName, algo.ToString(), hashdata);
                    if (item == null || hashdata == null)
                        item = new ZHashItem($"Hashing failed: {file.FullName}");
                    if (!quiet || Console.IsOutputRedirected)
                        PrintLine(item.ToString(), item.isInvalid ? ConsoleColor.Red : ConsoleColor.Blue);
                }

            if (!quiet && count == 0)
                PrintLine("No matching files found.");
        }

        static void Verify()
        {
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
                        PrintLine($"(new)  {rel}", ConsoleColor.DarkGray);
                    else
                    {
                        if (!quiet) Print($"  ...  {file.Name}\r", ConsoleColor.DarkGray);
                        byte[] hashdata = ComputeHash(file.FullName);
                        if (hashdata == null)
                            PrintLine($"ERROR  {rel}", ConsoleColor.Magenta);
                        else
                        {
                            if (hashdata.SequenceEqual(hashItem.hashData))
                                PrintLine($"  OK!  {rel}", ConsoleColor.Green);
                            else
                                PrintLine($"FAIL!  {rel}", ConsoleColor.Red);
                        }
                    }
                }

            if (!quiet && count == 0)
                PrintLine("No matching files found.");
        }

        static void Print(string text, ConsoleColor color = ConsoleColor.Black)
        {
            if (color == ConsoleColor.Black) color = DefaultColor;
            Console.ForegroundColor = color;
            Console.Write(text);
        }

        internal static void PrintDebug(string text)
        {
            if (DEBUG) Print(text + Environment.NewLine, ConsoleColor.DarkGray);
        }

        static void PrintLine(string text, ConsoleColor color = ConsoleColor.Black)
        {
            Print(text + Environment.NewLine, color);
        }

        static HashAlgorithm GetHasher(CmdOption hash)
        {
            switch (hash)
            {
                case CmdOption.MD5: return MD5.Create();
                case CmdOption.SHA1: return SHA1.Create();
                case CmdOption.SHA256: return SHA256.Create();
            }
            return null;
        }

        static void HashStdin()
        {
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
            catch { }
        }

        static byte[] ComputeHash(string path)
        {
            try
            {
                FileInfo fi = new FileInfo(path);
                Stopwatch sw = DEBUG ? Stopwatch.StartNew() : null;
                HashAlgorithm hasher = GetHasher(algo);
                using (Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20, true))
                    hasher.ComputeHash(stream);

                sw?.Stop();
                if (DEBUG)
                {
                    long KB = fi.Length / 1024;
                    double secs = sw.ElapsedMilliseconds/1000.0;
                    if (secs == 0) secs = 1;
                    PrintDebug($"\n** Hashed {KB} KB in {secs:f2} seconds = {KB/secs:f2} KB/sec");
                }
                return hasher.Hash;
            }
            catch { }
            return null;
        }

        static void Register()
        {
            string ext = CmdLine.Paths.Count > 0 ? CmdLine.Paths[0] : Constants.ZHEXT;

            if (CmdLine.Paths.Count > 1)
                Console.WriteLine("Please register only one extension at a time");
            else if (!Regex.IsMatch(ext, @"^\.?\w+$"))
                Console.WriteLine($"Cannot register invalid extension: {ext}");
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
                }
            }
        }
    }
}
