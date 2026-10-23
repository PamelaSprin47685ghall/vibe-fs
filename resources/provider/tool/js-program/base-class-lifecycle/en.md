  async park(payload) {
    // Suspends this program at this await point and hands control back to the orchestrator; the tool call returns { handle, payload }, and the caller resumes with { unpark: { handle, value } } or gives up with { abandon: { handle } }.
    // Example: const decision = await this.park({ stage: "review" });
  }

  async deliver(carry = []) {
    // Close the work in assistant text in your own judgment, then call this.deliver() immediately. The current turn returns automatically; do not copy the text or assign it an anchor. No mandated sections.
    // carry adds older context only: integers reference anchors; strings supply literal text. Delivery ends the program. An invalid array returns VIEW_DECLARATION_INVALID; correct it and retry.
    // Example: await this.deliver();
  }

  async run() {
    // The program entry: your class implements it, and its return value is this program's result (it must be JSON-compatible).
    // Example: async run() { return { ok: true }; }
  }

