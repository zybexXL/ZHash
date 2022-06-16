using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ZHash
{

    public static class Util
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        public static string CleanPath(string path) => Regex.Replace(path ?? "", @"^\.[\\/]", "");

        public static string Hexify(byte[] hash)
            => hash == null || hash.Length == 0 ? ""
            : BitConverter.ToString(hash).Replace("-", string.Empty);

        public static byte[] StringToByteArray(string hex)
        {
            if (hex == null || hex.Length == 0) return new byte[0];
            if (hex.Length % 2 == 1) hex = $"0{hex}";
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public static string GetRelativePath(string relativeTo, string path)
        {
            string absRoot = Path.GetFullPath(relativeTo);
            string absPath = Path.GetFullPath(path);

            if (absRoot == absPath) return "";

            if (!absRoot.EndsWith($"{Path.DirectorySeparatorChar}"))
                absRoot = $"{absRoot}{Path.DirectorySeparatorChar}";

            bool isLinux = Environment.OSVersion.Platform == PlatformID.Unix;
            StringComparison comparer = isLinux ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase;
            if (absPath.StartsWith(absRoot, comparer))
                return absPath.Substring(absRoot.Length);
            
            return absPath;
        }

        public static Regex mask2Regex(List<string> masks, bool ignoreCase = true)
        {
            return mask2Regex(string.Join("|", masks), ignoreCase);
        }

        public static Regex mask2Regex(string mask, bool ignoreCase = true)
        {
            const char uAsterisk = '\uEABA';
            const char uQuestion = '\uEBAB';
            const char uPipe = '\uEACA';

            //bool is3charExt = Regex.IsMatch(mask, @"\.\w\w\w$");
            mask = mask.Trim('|');
            mask = mask.Replace('*', uAsterisk);
            mask = mask.Replace('?', uQuestion);
            mask = mask.Replace('|', uPipe);
            mask = Regex.Escape(mask);
            
            //if (!is3charExt) mask = mask + "$";
            mask = mask.Replace($"{uAsterisk}", ".*");
            mask = mask.Replace(uQuestion, '.');
            mask = mask.Replace(uPipe, '|');
            mask = $"^({mask})$";

            return new Regex(mask, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
        }
    }
}
