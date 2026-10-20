  async park(payload) {
    // Suspends this program at this await point and hands control back to the orchestrator; the tool call returns { handle, payload }, and the caller resumes with { unpark: { handle, value } } or gives up with { abandon: { handle } }.
    // Example: const decision = await this.park({ stage: "review" });
  }

  async deliver(value, view) {
    // Ends this program with an explicit terminal value. value is the data; optional view selects handoff material with { prefixUntil, thenMap }, using the shared view rules.
    // Example: await this.deliver({ done: true });
  }

  async run() {
    // The program entry: your class implements it, and its return value is this program's result (it must be JSON-compatible).
    // Example: async run() { return { ok: true }; }
  }
