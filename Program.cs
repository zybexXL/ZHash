using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ZHash
{
    class Program
    {
        static Version version = new Version(1, 0, 0);
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

            DEBUG = CmdLine.hasOption(CmdOption.Debug);
            algo = CmdLine.FirstOrDefault(CmdOption.SHA1, CmdOption.SHA256, CmdOption.MD5);
            quiet = CmdLine.hasOption(CmdOption.Quiet) || Console.IsOutputRedirected;
            manager = new ZHashManager();

            CmdOption mode = CmdLine.FirstOrDefault(CmdOption.Compute, CmdOption.Update, CmdOption.Verify, CmdOption.Stdin);
            switch (mode)
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
            }

            manager.Close();
            Console.ResetColor();
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
                        PrintLine(item.ToString(), item.isInvalid ? ConsoleColor.Red : DefaultColor);
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
                HashAlgorithm hasher = GetHasher(algo);
                using (Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20, true))
                    hasher.ComputeHash(stream);

                return hasher.Hash;
            }
            catch { }
            return null;
        }
    }
}
