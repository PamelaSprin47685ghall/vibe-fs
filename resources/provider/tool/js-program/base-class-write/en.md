  write(path, content) {
    // Writes a UTF-8 file (overwriting an existing one or creating a missing one): one atomic write, effective as-is; the write-before check compares the read snapshot and reports FILE_CHANGED.
    // Example: this.write("generated/version.txt", "1.2.3\n");
  }
