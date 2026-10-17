write(path, content) overwrites an existing UTF-8 file or creates a missing
one: one atomic write, effective immediately (as-is). If the target was read
through cat or file and changed on disk since that snapshot: FILE_CHANGED.
Edit and Write remain distinct capabilities.

mkdir(path) creates exactly one directory (POSIX, non-recursive): an existing
target is DIRECTORY_EXISTS, a missing parent is PARENT_DIRECTORY_MISSING.

Find and replace is a composition, not a primitive:
  const text = await this.cat(path);
  const [begin, end] = await this.where(text, "old");
  this.write(path, text.slice(0, begin) + "new" + text.slice(end));
Batch replacement is one whereAll plus a reverse loop. Append is cat + write.
