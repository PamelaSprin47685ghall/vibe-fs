  async call(command, planB) {
    // Returns { ok, status, exitCode, stdout, stderr, truncated, bytes, durationMs }. command is one shell string. planB is required: the alternative if this command is not run. Its UTF-8 byte length × 50ms is the automatic yield budget. A missing planB is CALL_COMMAND_UNSUPPORTED. stdout and stderr are the increment since the previous automatic park.
    // Example: const outcome = await this.call("printf ok", "read the file directly");
  }

  async signal(signal) {
    // Signals the call that parked this program. CALL_CHILD_UNKNOWN when that command has already ended.
  }

  async send(input) {
    // Writes keyboard input to the call that parked this program, appending a newline when missing. CALL_CHILD_UNKNOWN when that command has already ended.
  }
