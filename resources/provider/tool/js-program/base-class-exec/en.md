  async call(command) {
    // Returns { ok, status, exitCode, stdout, stderr, truncated, bytes, durationMs }. command is one shell string. stdout and stderr are the increment since the previous automatic park. A function or class is CALL_COMMAND_UNSUPPORTED.
    // Example: const outcome = await this.call("printf ok");
  }

  async signal(signal) {
    // Signals the call that parked this program. CALL_CHILD_UNKNOWN when that command has already ended.
  }

  async send(input) {
    // Writes keyboard input to the call that parked this program, appending a newline when missing. CALL_CHILD_UNKNOWN when that command has already ended.
  }
