using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ZHash
{
    public class ZHashItem
    {
        public bool isInvalid;
        public string line;

        public string algorithm;
        public string path;
        public byte[] hashData;
        public DateTime timestamp;

        public string itemId => Util.CleanPath(path).ToLower();
        public string hashStr => Util.Hexify(hashData);

        public bool updated;

        public ZHashItem(string text, bool invalid = true)
        {
            line = text;
            isInvalid = invalid;
        }

        public ZHashItem(string path, string algo, byte[] hash)
            : this(path, algo, hash, DateTime.Now)
        {
        }

        public ZHashItem(string path, string algo, byte[] hash, DateTime date)
        {
            this.path = path;
            algorithm = algo;
            hashData = hash;
            timestamp = date;
        }

        public static ZHashItem Parse(string text)
        {
            Match m = Regex.Match(text ?? "", @"^(\w+:)?([a-zA-Z0-9]+)\s+(\d\d\d\d-\d\d-\d\dT\d\d:\d\d:\d\d)?\s+(.*)");
            if (m.Success)
            {
                if (!DateTime.TryParseExact(m.Groups[3].Value, "s", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                    date = DateTime.Now;

                return new ZHashItem(
                    m.Groups[4].Value,
                    m.Groups[1].Value.Trim(':'),
                    Util.StringToByteArray(m.Groups[2].Value),
                    date);
            }
            return new ZHashItem(text, true);
        }

        public string ToString(bool withTimestamp)
        {
            string date = withTimestamp ? $"{timestamp:s}  " : "";
            string hash = Util.Hexify(hashData);
            string file = Util.CleanPath(path);
            return isInvalid ? line : $"{algorithm}:{hash}  {date}{file}";
        }

        public override string ToString()
        {
            return ToString(true);
        }
    }
}
