  write(path, content) {
    // Writes a UTF-8 file (overwriting an existing one or creating a missing one): one atomic write, effective as-is; the write-before check compares the read snapshot and reports FILE_CHANGED.
    // Example: this.write("generated/version.txt", "1.2.3\n");
  }

  mkdir(path) {
    // Creates one directory (POSIX, non-recursive); an existing target is DIRECTORY_EXISTS, a missing parent is PARENT_DIRECTORY_MISSING.
    // Example: this.mkdir("generated");
  }
