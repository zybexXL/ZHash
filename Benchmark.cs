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

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        internal const uint GENERIC_READ  = 0x80000000;
        internal const uint GENERIC_WRITE = 0x40000000;
        internal const uint GENERIC_ALL   = 0x10000000;

        internal const uint CREATE_NEW = 1;
        internal const uint CREATE_ALWAYS = 2;
        internal const uint OPEN_EXISTING = 3;
        internal const uint OPEN_ALWAYS = 4;
        internal const uint TRUNCATE_EXISTING = 5;

        internal const int FILE_ATTRIBUTE_NORMAL = 0x80;
        internal const int FILE_SHARE_READ = 1;
        internal const int FILE_SHARE_WRITE = 2;

        const uint FILE_FLAG_NO_BUFFERING  = 0x20000000;
        const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;
        const uint FILE_FLAG_OVERLAPPED    = 0x40000000;


        public static int Bench()
        {
            int hashduration = 2000;
            int diskduration = 3000;
            int queues = 4;
            long maxSize = (long)1 << 32;     // 4 GB

            int result = BenchmarkCPU(hashduration);
            result += BenchmarkStorage(diskduration, queues, maxSize);
            return result;
        }

        static int BenchmarkCPU(int duration)
        {
            long bufSize = 1 << 20;
            byte[] buffer = new byte[bufSize];
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
                    Program.PrintLine($"  {algo,6} : {speed:f2} MiB/sec");
                }
            }
            catch (Exception ex)
            {
                Program.PrintLine($"Benchmark failed! {ex.Message}");
                return 1;
            }
            return 0;
        }

        static int BenchmarkStorage(int duration, int queues, long maxFileSize)
        {
            int bufSize = 1 << 20;
            string testFile = "$$zhashTest$$.tmp";
            
            Program.PrintLine($"\nStorage sequential speed:");

            try
            {
                string path = ".";
                if (CmdLine.Paths.Count > 0)
                    path = CmdLine.Paths[0];
                path = Path.GetFullPath(path);

                if (!Util.isWritable(path))
                {
                    if (CmdLine.Paths.Count > 0)
                    {
                        Program.PrintLine($"Folder is not writable: {path}");
                        return 1;
                    }
                    else
                    {
                        path = Path.GetTempPath();
                        Program.PrintLine($"Current folder is not writable, using TEMP folder: {path}");
                    }
                }

                testFile = Path.Combine(path, testFile);

                // write test
                double MBps = BenchWriteAsync(testFile, bufSize, duration, queues, maxFileSize);
                Program.PrintLine($"   Write : {MBps:f2} MiB/sec");

                // let disk flush its internal cache
                Thread.Sleep(2000);

                // read test
                MBps = BenchReadAsync(testFile, bufSize, duration, queues);
                Program.PrintLine($"    Read : {MBps:f2} MiB/sec");

                // read test raw
                //Thread.Sleep(2000);
                //MBps = BenchReadRawAsync(testFile, bufSize, duration, queues);
                //Program.PrintLine($"Raw Read : {MBps:f2} MiB/sec");
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

        // write test, WRITE_THROUGH mode (no caching/buffering)
        static double BenchWriteAsync(string testFile, int bufSize, int duration, int queues, long maxSize)
        {
            byte[] buffer = new byte[bufSize * queues];
            FileOptions flags = 0;
            
            unchecked { flags = (FileOptions)(FILE_FLAG_WRITE_THROUGH + FILE_FLAG_NO_BUFFERING); }
            using (var stream = File.Create(testFile, bufSize, flags))
            {
                Stopwatch sw = Stopwatch.StartNew();

                long bytes = 0;
                Task[] writers = new Task[queues];
                for (int t = 0; t < queues; t++)
                    writers[t] = stream.WriteAsync(buffer, t * bufSize, bufSize);

                int running = queues;
                while (running > 0)
                {
                    int t = Task.WaitAny(writers);
                    bytes += bufSize;

                    if (stream.Position >= maxSize)
                        stream.Position = 0;

                    if (sw.ElapsedMilliseconds < duration)
                        writers[t] = stream.WriteAsync(buffer, t * bufSize, bufSize);
                    else
                        running--;
                }

                stream.Flush(true);
                sw.Stop();
                stream.Close();

                double speed = Speed(bytes, sw.ElapsedMilliseconds);
                return speed;
            }
        }

        // read test, cache enabled
        // this test can only read the test file once, or the caching will distort results
        static double BenchReadAsync(string testFile, int bufSize, int duration, int queues)
        {
            byte[] buffer = new byte[bufSize * queues];

            using (Stream stream = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.None, bufSize, true))
            {
                Stopwatch sw = Stopwatch.StartNew();

                long bytes = 0;
                Task<int>[] readers = new Task<int>[queues];
                for (int t = 0; t < queues; t++)
                    readers[t] = stream.ReadAsync(buffer, t * bufSize, bufSize);

                int running = queues;
                while (running > 0)
                {
                    int t = Task.WaitAny(readers);
                    bytes += readers[t].Result;

                    //if (stream.Position >= (1 << 30))   // max 1GB
                    //    stream.Position = 0;

                    if (readers[t].Result == bufSize && sw.ElapsedMilliseconds < duration)
                        readers[t] = stream.ReadAsync(buffer, t * bufSize, bufSize);
                    else
                        running--;
                }

                sw.Stop();
                stream.Close();

                double speed = Speed(bytes, sw.ElapsedMilliseconds);
                return speed;
            }
        }

        // read test, cache disabled
        // test file can be read multiple times as the data is not buffered/cached
        static double BenchReadRawAsync(string testFile, int bufSize, int duration, int queues)
        {
            byte[] buffer = new byte[bufSize * queues];

            uint flags = FILE_FLAG_OVERLAPPED + FILE_FLAG_WRITE_THROUGH + FILE_FLAG_NO_BUFFERING;
            SafeFileHandle h = CreateFile(testFile, GENERIC_READ, 0, IntPtr.Zero, OPEN_EXISTING, flags, IntPtr.Zero);

            using (Stream stream = new FileStream(h, FileAccess.Read, bufSize, true))
            {
                Stopwatch sw = Stopwatch.StartNew();

                long bytes = 0;
                long sizeLimit = stream.Length - bufSize;

                Task<int>[] readers = new Task<int>[queues];
                for (int t = 0; t < queues; t++)
                    readers[t] = stream.ReadAsync(buffer, t * bufSize, bufSize);

                int running = queues;
                while (running > 0)
                {
                    int t = Task.WaitAny(readers);
                    bytes += readers[t].Result;

                    if (stream.Position > sizeLimit)
                        stream.Position = 0;

                    if (readers[t].Result == bufSize && sw.ElapsedMilliseconds < duration)
                        readers[t] = stream.ReadAsync(buffer, t * bufSize, bufSize);
                    else
                        running--;
                }

                sw.Stop();
                stream.Close();
                h.Close();

                double speed = Speed(bytes, sw.ElapsedMilliseconds);
                return speed;
            }
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
