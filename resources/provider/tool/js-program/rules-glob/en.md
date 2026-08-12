glob(pattern) enumerates files with gitignore/wildmatch semantics under the
current path boundary. * does not cross /. ** matches zero or more directories.
A pattern without a slash matches at any depth (*.md matches every .md file).
{a,b} expands to alternatives. Results omit .git, omit gitignored paths, do
not follow symlinks, and are sorted. The return value is { paths, truncated }.
The bound is on match count; truncated is true when matches were cut. glob
does not grant Read.
