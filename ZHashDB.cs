using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ZHash
{
    public class ZHashDB
    {
        public string dbPath;
        public bool isDirty;
        public Dictionary<string, ZHashItem> hashes = new Dictionary<string, ZHashItem>();

        public ZHashDB(string path, bool load=true)
        {
            dbPath = Path.GetFullPath(path);
            isDirty = !load || !Read();
        }

        private bool Read(bool keepInvalid = true)
        {
            try
            {
                if (!File.Exists(dbPath))
                    return false;

                hashes.Clear();
                var lines = File.ReadAllLines(dbPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    var item = ZHashItem.Parse(lines[i]);
                    if (item.isInvalid)
                    {
                        if (keepInvalid)
                            hashes[$"line{i}"] = item;
                    }
                    else
                        hashes[item.itemId] = item;
                }
                
                Program.PrintDebug($":: Loaded {hashes.Count} entried from {dbPath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARNING: Failed to read hashfile {dbPath}\n          {ex.Message}");
            }
            return false;
        }

        public ZHashItem getHash(string path)
        {
            if (hashes.TryGetValue(path.ToLower(), out var item)) return item;
            if (hashes.TryGetValue(Util.CleanPath(path).ToLower(), out item)) return item;
            return null;
        }

        public ZHashItem Add(string path, string algo, byte[] hash)
        {
            //Remove(path);
            var item = new ZHashItem(path, algo, hash);
            item.updated = true;
            hashes[item.itemId] = item;
            isDirty = true;
            return item;
        }

        public void Remove(string path)
        {
            path = path.ToLower();
            bool removed = hashes.Remove(path);
            removed |= hashes.Remove(Util.CleanPath(path));
            if (removed)
                isDirty = true;
        }

        public void Purge(bool keepInvalid = true)
        {
            int count = hashes.Count;
            hashes = hashes.Where(h => (keepInvalid && h.Value.isInvalid) || h.Value.updated).ToDictionary(h=>h.Key, h=>h.Value);
            if (hashes.Count != count)
                isDirty = true;
        }

        public void Save(bool hide = false) { Save(dbPath, hide); }

        public bool Save(string hashfile, bool hide = false)
        {
            try
            {
                dbPath = Path.GetFullPath(hashfile);
                var data = hashes.Values.OrderBy(h => h.itemId).ToList();
                StringBuilder sb = new StringBuilder();
                foreach (var hash in data)
                    sb.AppendLine(hash.ToString());

                if (File.Exists(dbPath))
                {
                    File.SetAttributes(dbPath, FileAttributes.Normal);
                    File.Delete(dbPath);
                }
                File.WriteAllText(dbPath, sb.ToString());

                if (hide)
                    File.SetAttributes(dbPath, FileAttributes.Hidden | FileAttributes.Archive);

                Program.PrintDebug($":: Wrote {hashes.Count} entried to {dbPath}");
                isDirty = false;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARNING: Failed to write hashfile {hashfile}\n          {ex.Message}");
            }

            return false;
        }
    }
}
