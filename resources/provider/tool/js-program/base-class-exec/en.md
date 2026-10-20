  // The extendable engineering-loop base class: the parent of this.call(class extends this.Callable { ... }).
  Callable = Callable;

  async call(callableClass) {
    // Returns { ok, status, reason, exitCode, stdout, stderr, truncated, bytes, durationMs, steps }: hands a class extending this.Callable to the host-owned engineering loop. The host runs the sanity self-test first, then calls next per round and watch per time slice, and closes the job at maxDuration.
    // Example: const outcome = await this.call(class extends this.Callable { ... });
  }
