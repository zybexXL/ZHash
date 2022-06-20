using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZHash
{
    public class Benchmark
    {
        const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
        const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;

        public static int Bench()
        {
            int result = BenchmarkCPU();
            result += BenchmarkStorage();
            return result;
        }

        static int BenchmarkCPU()
        {
            long bufSize = 1 << 20;
            byte[] buffer = new byte[bufSize];
            int duration = 3000;
            var hashes = new CmdOption[] { CmdOption.SHA256, CmdOption.SHA1, CmdOption.MD5 };

            try
            {
                Program.PrintLine("CPU hashing speed:");
                foreach (var algo in hashes)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    HashAlgorithm hasher = Program.GetHasher(algo);
                    int count = 0;
                    while (sw.ElapsedMilliseconds < duration)
                    {
                        var hash = hasher.ComputeHash(buffer);
                        Array.Copy(hash, buffer, hash.Length);
                        count++;
                    }
                    sw?.Stop();
                    double speed = Speed(bufSize * count, sw.ElapsedMilliseconds);
                    Program.PrintLine($"  {algo,6} : {speed:f2} MB/sec");
                }
            }
            catch (Exception ex)
            {
                Program.PrintLine($"Benchmark failed! {ex.Message}");
                return 1;
            }
            return 0;
        }

        static int BenchmarkStorage()
        {
            long bufSize = 1 << 20;
            byte[] buffer = new byte[bufSize];
            int duration = 3000;
            string testFile = "$$zhashTest$$.tmp";

            try
            {
                string path = ".";
                if (CmdLine.Paths.Count > 0)
                    path = CmdLine.Paths[0];
                if (File.Exists(path)) path = Path.GetDirectoryName(path);
                path = Path.GetFullPath(path);
                testFile = Path.Combine(path, testFile);

                Program.PrintLine($"\nStorage speed, sequential:");


                FileOptions fo = 0;
                unchecked { fo = (FileOptions)(FILE_FLAG_WRITE_THROUGH + FILE_FLAG_NO_BUFFERING); }
                using (var stream = File.Create(testFile, 1 << 20, fo))
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    int count = 0;
                    while (sw.ElapsedMilliseconds < duration)
                    {
                        stream.Write(buffer, 0, buffer.Length);
                        count++;
                    }
                    stream.Flush();
                    stream.Close();
                    sw.Stop();
                    double speed = Speed(bufSize * count, sw.ElapsedMilliseconds);
                    Program.PrintLine($"  Write : {speed:f2} MB/sec");
                }

                using (Stream stream = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.None, 1 << 20, true))
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    long bytes = 0;
                    while (sw.ElapsedMilliseconds < duration)
                    {
                        int count = stream.Read(buffer, 0, buffer.Length);
                        bytes += count;
                        if (count < buffer.Length) break;
                    }
                    stream.Flush();
                    stream.Close();
                    sw.Stop();
                    double speed = Speed(bytes, sw.ElapsedMilliseconds);
                    Program.PrintLine($"   Read : {speed:f2} MB/sec");
                }
            }
            catch (Exception ex)
            {
                Program.PrintLine($"Benchmark failed! {ex.Message}");
                return 1;
            }
            finally
            {
                try { File.Delete(testFile); } catch { }
            }
            return 0;
        }

        static double Speed(long bytes, long millis, bool KBps = false)
        {
            if (!KBps) bytes = bytes >> 10;
            double secs = millis / 1000.0;
            if (secs == 0) secs = 1;
            return (bytes / 1024.0) / secs;
        }
    }
}
