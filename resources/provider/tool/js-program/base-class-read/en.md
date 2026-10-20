  async cat(path) {
    // Returns string: reads the file content and registers that read snapshot (the write-before FILE_CHANGED check compares against it); a missing path is FILE_NOT_FOUND, non-UTF-8 content is INVALID_UTF8.
    // Example: const text = await this.cat("README.md");
  }

  async stat(path) {
    // Returns { exists, size, mtime, isDir }: looks one path up; a missing path is a normal observation (exists = false), not a failure.
    // Example: const info = await this.stat("package.json");
  }

  whereAll(content, needle, from = 0) {
    // Returns [[begin, end], ...]: normalized exact matching in the text (whitespace runs collapse to one space), reported as UTF-16 offsets into the original string; no match returns [].
    // Example: const spans = this.whereAll(text, "const value");
  }

  async where(content, needle, from = 0) {
    // Returns [begin, end]: exactly one match returns its span; zero or many matches suspend the program through the park channel with { needle, candidates, nearest }, and unpark injects the chosen span, so the program always receives one span.
    // Example: const [begin, end] = await this.where(text, "const value");
  }
