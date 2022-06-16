# ZHash
File hashing command-line tool with many features:
- Computes and verifies file hashes
- SHA1, SHA256 and MD5 support
- Accepts STDIN piped input
- Multiple file/folders/masks and exclusion masks
- Recursive subfolder processing
- Single hash DB file or one file per subfolder


# Usage
```
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
```
