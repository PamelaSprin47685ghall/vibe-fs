  async park(payload) {
    // Suspends this program at this await point and hands control back to the orchestrator; the tool call returns { handle, payload }, and the caller resumes with { unpark: { handle, value } } or gives up with { abandon: { handle } }.
    // Example: const decision = await this.park({ stage: "review" });
  }

  async deliver(reportMapping) {
    // Ends this program with the delivered view. First state the outcome as prose, then pass the reportMapping — a Record<string, null|string> mapping anchor numbers to summaries (null leaves an anchor unmapped) — pointing at the full material of this work; the host reads it as { prefixUntil: 0, thenMap: reportMapping }, and a { prefixUntil, thenMap } wrapper is not accepted. A missing or invalid reportMapping fails with VIEW_DECLARATION_INVALID, so the program is not delivered.
    // Example: await this.deliver({ 1: "setup summary", 2: null });
  }

  async run() {
    // The program entry: your class implements it, and its return value is this program's result (it must be JSON-compatible).
    // Example: async run() { return { ok: true }; }
  }
