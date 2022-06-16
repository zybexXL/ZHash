using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ZHash
{
    public enum CmdOption { Help, Compute, Update, Verify, Stdin, Debug, New, Refresh, Exclude, Subs, File, Local, Purge, Hide, Absolute, Quiet, MD5, SHA1, SHA256 }

    internal static class CmdLine
    {
        public static List<string> Paths { get; private set; } = new List<string>();
        public static List<string> Excludes { get; private set; } = new List<string>();
        public static bool hasOption(CmdOption option) => options.ContainsKey(option);
        public static string getOption(CmdOption option) => options.TryGetValue(option, out string value) ? value : null;
        public static int countOptions(params CmdOption[] options) => options.Count(o => hasOption(o));
        public static CmdOption FirstOrDefault(params CmdOption[] options) => options.FirstOrDefault(o => hasOption(o));

        static Dictionary<string, string> Aliases = new Dictionary<string, string>() {
            { "?", "help" }, { "v", "verify" }, { "u", "update" }, { "c", "compute" }, { "i", "stdin" },
            { "s", "subs" }, {"x", "Exclude" }, { "f", "File" }, { "l", "local" }, { "q", "quiet" }, 
            { "n", "new" }, { "r", "refresh" },
            { "p", "purge" }, { "h", "hide" }, { "a", "absolute" }, { "abs", "absolute" }, { "d", "debug" },
            { "1", "sha1" }, { "2", "sha256" }, { "m", "md5" }, { "5", "md5" },
        };

        static string getAlias(string text) => Aliases.TryGetValue(text?.ToLower(), out string alias) ? alias : text;

        static Dictionary<CmdOption, string> options = new Dictionary<CmdOption, string>();


        internal static bool Parse(string[] args, out int exitcode)
        {
            exitcode = 0;
            try
            {
                if (ParseArgs(args) && ValidateArgs())
                {
                    if (hasOption(CmdOption.Help))
                        Usage();
                    else
                        return true;
                }
                else
                {
                    Console.WriteLine("Invalid command line args, use -? for syntax");
                    exitcode = 100;
                }
            }
            catch (IndexOutOfRangeException) { Usage(); }
            catch (Exception ex) { 
                Console.WriteLine($"Failed to parse command line!\n{ex.Message}");
                exitcode = 99;
            }
            return false;
        }

        static void Usage()
        {
            Console.WriteLine(@"ZHash calculates or verifies checksum hashes for one or more files

  USAGE: ZHash [-options] [<file>|<folder>|<mask>]

  MODE:
    -c, -compute    : compute hashes of all input files (default)
    -u, -update     : Update hashes in zhash.chk, same as -c -f zhash.chk
    -v, -verify     : verify hashes of files already in zhash.chk
    -i, -stdin      : compute hash for stdin data; input files are ignored

  HASH FUNCTION:
    -1, -sha1       : use SHA1 hash function, 160 bits (default)
    -2, -sha256     : use SHA256 hash function, 256 bits
    -m, -md5        : use MD5 hash function, 128 bits

  INPUT:
    <file>          : file to hash or verify
    <folder>        : folder to hash or verify
    <mask>          : file mask to hash or verify
   
    -x <mask>       : exclude files matching given file mask
    -s, -subs       : process subfolders
    -n, -new        : process only new files (files not in zhash.chk)
    -r, -refresh    : process only files already in zhash.chk

  OUTPUT:
    -f <zhash.chk>  : hashes filename. Outputs to console if not provided
    -l, -local      : output hashes file on same folder as source file(s)
    -p, -purge      : remove non-existant files from the hashes file
    -h, -hide       : set the Hidden + System attributes on the hashes file
    -a, -abs        : output absolute instead of relative (default) paths
    -q, -quiet      : quiet mode, suppresses console output
    -d, -debug      : print some debug info

  NOTES:
  > Multiple files, folders, masks and -x exclusions can be provided
  > Compute mode outputs to console unless -f is given
  > Update mode outputs to file given with -f (default: zhash.chk)
  > Verify mode reads from file given with -f (default: zhash.chk)
  > Options -r and -n are ignored in Verify mode
  > Stdin mode computes the hash for STDIN data and outputs to console.
    Input paths and Output options are ignored. Input can be piped.
  > Hashes file is created in the current folder unless -local is used
    or a full path is given with -f option
");
        }

        static bool ParseArgs(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                Match m = Regex.Match(args[i], "^(--?|/)(.+)");
                if (!m.Success)
                    Paths.Add(getAlias(args[i].Trim('"')));
                else
                {
                    string opt = getAlias(m.Groups[2].Value);
                    if (Enum.TryParse(opt, true, out CmdOption option))
                    {
                        switch (option)
                        {
                            case CmdOption.Help:
                                options[option] = "true";
                                return true;
                            case CmdOption.Exclude:
                                Excludes.Add(args[++i].Trim('"'));
                                break;
                            case CmdOption.File:
                                options[option] = args[++i].Trim('"');
                                break;
                            default:
                                options[option] = "true";
                                break;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Unknown option -{opt}");
                        return false;
                    }
                }
            }
            return true;
        }

        static bool ValidateArgs()
        {
            int funcs = countOptions(CmdOption.MD5, CmdOption.SHA1, CmdOption.SHA256);
            int modes = countOptions(CmdOption.Verify, CmdOption.Update, CmdOption.Compute, CmdOption.Stdin);
            
            //default options
            if (modes == 0) options[CmdOption.Compute] = "true";
            if (funcs == 0) options[CmdOption.SHA1] = "true";

            // default input path
            if (Paths.Count == 0) Paths.Add(".");
            
            // default hash file
            if (!hasOption(CmdOption.File) && !hasOption(CmdOption.Compute))
                options[CmdOption.File] = "zhash.chk";
            
            // syntax errors
            if (funcs > 1)
                Console.WriteLine("Only one hash function supported per run");
            else if (modes > 1)
                Console.WriteLine("Please select only one mode");
            else
                return true;

            return false;
        }
    }
}
