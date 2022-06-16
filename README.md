# ZHash
File hashing command-line tool with many features:
- Computes and verifies file hashes
- SHA1, SHA256 and MD5 support
- Accepts STDIN piped input
- Multiple file/folders/masks and exclusion masks
- Recursive subfolder processing
- Single hash DB file or one file per subfolder
- Extension shell handler to verify files on double-click

# Usage

Run ZHash -? to get the following help text:

```
  USAGE: ZHash [-options] [<file>|<folder>|<mask>]

  MODE:
    -c, -compute    : compute hashes of all input files (default)
    -u, -update     : Update hashes in zhash.zh, same as -c -f zhash.zh
    -v, -verify     : verify hashes of files already in zhash.zh
    -i, -stdin      : compute hash for stdin data; input files are ignored
        -reg [.ext] : register shell extension to verify .zh files

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
    -n, -new        : process only new files (files not in zhash.zh)
    -r, -refresh    : process only files already in zhash.zh

  OUTPUT:
    -f <zhash.zh>   : hashes filename. Outputs to console if not provided
    -l, -local      : output hashes file on same folder as source file(s)
    -p, -purge      : remove non-existant files from the hashes file
    -h, -hide       : set the Hidden attribute on the hashes file
    -hs, -syshide   : set the Hidden + System attributes on the hashes file
    -a, -abs        : output absolute instead of relative (default) paths
    -q, -quiet      : quiet mode, suppresses console output
    -d, -debug      : print some debug info
    -w, -wait       : wait for keypress before exiting

  NOTES:
  > Multiple files, folders, masks and -x exclusions can be provided
  > Compute mode outputs to console unless -f is given
  > Update mode outputs to file given with -f (default: zhash.zh)
  > Verify mode reads from file given with -f (default: zhash.zh)
  > Options -r and -n are ignored in Verify mode
  > Stdin mode computes the hash for STDIN data and outputs to console.
    Input paths and Output options are ignored. Input can be piped.
  > Hashes file is created in the current folder unless -local is used
    or a full path is given with -f option
  > The -reg option registers the an extension to run a ZHash verify.
    An alternate extension can be registered with '-reg .ext'
    Additional options can be included on the registration. Example:
      zhash -reg .chk -w -s 
```
