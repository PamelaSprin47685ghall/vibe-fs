  async cat(path) {
    // Returns string: reads the file content and registers that read snapshot (the write-before FILE_CHANGED check compares against it); a missing path is FILE_NOT_FOUND, non-UTF-8 content is INVALID_UTF8.
    // Example: const text = await this.cat("README.md");
  }

  async stat(path) {
    // Returns { exists, size, mtime, isDir }: looks one path up; a missing path is a normal observation (exists = false), not a failure.
    // Example: const info = await this.stat("package.json");
  }
