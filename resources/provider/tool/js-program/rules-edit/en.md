rewrite(path, newText) stages replacement of an existing UTF-8 file. newText is
the complete resulting file, not a patch. The target must exist in the
transaction snapshot or the call fails FILE_NOT_FOUND. newText must be a string.
The call does not write immediately; it adds a StagedRewrite to this program's
WriteSet. You do not have to file(path) first.
