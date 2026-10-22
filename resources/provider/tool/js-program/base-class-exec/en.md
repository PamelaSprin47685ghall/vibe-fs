  async call(command, timeoutOrOptions) {
    // Returns { ok, status, exitCode, stdout, stderr, truncated, bytes, durationMs }. command is one shell string. Optional timeoutOrOptions specifies an explicit timeout in milliseconds (a positive number or { timeoutMs } object); omitted uses the stable default timeout (10000ms). Text notes do not price waiting time. stdout and stderr are the increment since the previous automatic park.
    // Example: const outcome = await this.call("printf ok");
  }

  async signal(signal) {
    // Signals the call that parked this program. CALL_CHILD_UNKNOWN when that command has already ended.
  }

  async send(input) {
    // Writes keyboard input to the call that parked this program, appending a newline when missing. CALL_CHILD_UNKNOWN when that command has already ended.
  }
