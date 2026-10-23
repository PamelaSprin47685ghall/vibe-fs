  async commission(payload, carry) {
    // Returns { ok, name, agentId, outcome }. Open a separate Manager road only for an independent purpose, not for more work or a later stage.
    // payload is { charge, name?, calling? }; charge carries the message as prose, name and calling are routing metadata. A missing name comes from charge; calling defaults to lead. To continue a known road, use its name and omit calling.
    // carry adds needed history: integers reference anchors; strings supply literal text. The current turn is not sent. State the task once in charge.
    // Example: await this.commission({ charge: "audit the parser module on its own" }, [12]);
  }

