  async rg(argv) {
    // Returns { exitCode, messages } or { exitCode, paths }: one ripgrep call; a content search equals rg --json plus that argv (summary modes such as --files are passed through because rg refuses them together with --json). argv is a string array — do not shell-quote it and do not pass --json yourself; a search without a path does not search the workspace. exitCode 0 means a hit, 1 means a successful search with nothing, and is not a failure.
    // Example: const found = await this.rg(["RetryPolicy", "src", "-g", "*.fs"]);
  }
