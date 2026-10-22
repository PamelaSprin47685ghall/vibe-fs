  async park(payload) {
    // Suspends this program at this await point and hands control back to the orchestrator; the tool call returns { handle, payload }, and the caller resumes with { unpark: { handle, value } } or gives up with { abandon: { handle } }.
    // Example: const decision = await this.park({ stage: "review" });
  }

  async deliver(reportMapping) {
    // Ends this program with the delivered view. First state the outcome as prose in the assistant text — not in the mapping — then pass the reportMapping — a Record<string, null|string> mapping anchor numbers to summaries (null leaves an anchor unmapped) — pointing at the full material of this work, including that prose and the context it rests on; the host reads it as { prefixUntil: 0, thenMap: reportMapping }, and a { prefixUntil, thenMap } wrapper is not accepted. Do not copy the prose into the deliver argument, and do not leave it for after the call: there is no after. A missing or invalid reportMapping is refused: the call returns { ok: false, code: "VIEW_DECLARATION_INVALID", reason }, that refusal is not a delivery, and the program keeps running.
    // Example: await this.deliver({ 1: "setup summary", 2: null });
  }

  async run() {
    // The program entry: your class implements it, and its return value is this program's result (it must be JSON-compatible).
    // Example: async run() { return { ok: true }; }
  }
