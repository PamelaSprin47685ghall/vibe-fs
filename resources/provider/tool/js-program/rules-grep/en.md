grep(needle, pattern = "**/*") searches UTF-8 files selected by the same
gitignore-style glob. needle is a non-empty string (literal) or a RegExp
(caller g/y/lastIndex ignored). Unreadable or non-UTF-8 files are skipped.
Returns { matches: [{ path, line, column, text }], truncated }. line and
column are 1-based. text is the matched substring. The bound is on match
count. grep does not grant file().
