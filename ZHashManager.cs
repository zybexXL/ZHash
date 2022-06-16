using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ZHash
{
    public class ZHashManager
    {
        bool dbLocal;
        bool dbPurge;
        bool dbHide;
        bool absolutePaths;
        bool recurse;
        public string dbPath;
        bool singleDb;
        bool dbless;

        ZHashDB currentDb;

        public ZHashManager()
        {
            dbLocal = CmdLine.hasOption(CmdOption.Local);
            dbPurge = CmdLine.hasOption(CmdOption.Purge);
            absolutePaths = CmdLine.hasOption(CmdOption.Absolute);
            dbHide = CmdLine.hasOption(CmdOption.Hide);
            recurse = CmdLine.hasOption(CmdOption.Subs);

            dbPath = CmdLine.getOption(CmdOption.File);
            dbless = string.IsNullOrEmpty(dbPath);
            if(dbless) dbPath = Constants.ZHFILE;

            singleDb = dbless || Regex.IsMatch(dbPath, @"[/\\:]");
            dbPath = Path.GetFullPath(dbPath);
        }

        ~ZHashManager() { Close(); }

        public ZHashDB getDB(string targetpath)
        {
            string zPath = dbPath;
            if (!singleDb)
            {
                if (dbLocal)
                    zPath = Path.Combine(Path.GetDirectoryName(targetpath), Path.GetFileName(dbPath));
                else
                    zPath = Path.Combine(Environment.CurrentDirectory, Path.GetFileName(dbPath));
            }

            zPath = Path.GetFullPath(zPath);
            if (currentDb?.dbPath != zPath)
            {
                Close();
                currentDb = new ZHashDB(zPath, !dbless);
            }
            return currentDb;
        }

        public ZHashItem Update(string path, string algo, byte[] hashdata)
        {
            if (hashdata == null) return null;

            var db = getDB(path);
            db.Remove(path);
            if (!absolutePaths)
            {
                path = Util.GetRelativePath(Path.GetDirectoryName(db.dbPath), path);
                db.Remove(path);
            }
            var hash = db.Add(path, algo, hashdata);

            return hash;
        }

        public ZHashItem GetHash(string path)
        {
            var db = getDB(path);
            var item = db.getHash(path);
            if (item == null)
            {
                path = Util.GetRelativePath(Path.GetDirectoryName(db.dbPath ?? ""), path);
                item = db.getHash(path);
            }
            return item;
        }

        public void Close()
        {
            if (dbless || currentDb == null) 
                return;

            if (dbPurge)
                currentDb.Purge();
            if (currentDb.isDirty)
                currentDb.Save(dbHide);

            currentDb = null;
        }

        public IEnumerable<FileInfo> EnumerateFiles(string path, List<string> excludes)
        {
            bool ignorecase = Environment.OSVersion.Platform != PlatformID.Unix;
            Regex reExclude = null;
            if (excludes != null && excludes.Count > 0)
                reExclude = Util.mask2Regex(excludes, ignorecase);

            foreach (var file in FindFiles(path, null, reExclude))
                yield return file;
        }

        private IEnumerable<FileInfo> FindFiles(string path, string mask, Regex reExclude)
        {
            if (!Directory.Exists(path))
            {
                mask = Path.GetFileName(path);
                path = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(path)) path = ".";
            }
            
            if (string.IsNullOrEmpty(mask))
                mask = "*.*";

            DirectoryInfo di = null;
            try
            {
                di = new DirectoryInfo(path);
            }
            catch { }
            if (di == null || !di.Exists)
                yield break;

            IEnumerator<FileInfo> fileenum = null;
            try
            {
                fileenum = di.EnumerateFiles(mask, SearchOption.TopDirectoryOnly).GetEnumerator();
            }
            catch { }
            while (true)
            {
                if (fileenum == null) break;
                FileInfo file = null;
                try 
                {
                    if (!fileenum.MoveNext()) break;
                    file = fileenum.Current;
                }
                catch { }
                if (file == null) break;
                if (reExclude == null || !reExclude.IsMatch(file.Name))
                    yield return file;
            }

            if (recurse)
            {
                IEnumerator<DirectoryInfo> direnum = null;
                try
                {
                    direnum = di.EnumerateDirectories("*.*", SearchOption.TopDirectoryOnly).GetEnumerator();
                }
                catch { }
                while (true)
                {
                    if (direnum == null) break;
                    DirectoryInfo dir = null;
                    try
                    {
                        if (!direnum.MoveNext()) break;
                        dir = direnum.Current;
                    }
                    catch { }
                    if (dir == null) break;
                    foreach (var file in FindFiles(dir.FullName, mask, reExclude))
                        yield return file;
                }
            }
            
            // simpler implementation, but cannot use try/catch with a yield inside
            //foreach (var file in di.EnumerateFiles(mask, SearchOption.TopDirectoryOnly))
            //    if (reExclude == null || !reExclude.IsMatch(file.Name))
            //        yield return file;

            //if (recurse)
            //{
            //    foreach (var sub in di.EnumerateDirectories("*.*", SearchOption.TopDirectoryOnly))
            //        foreach (var file in FindFiles(sub.FullName, mask, reExclude))
            //            yield return file;
            //}
        }
    }
}
