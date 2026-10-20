  async devops(intent, reportMapping) {
    // Returns the DevOps work record: hands one investigation question to this task's bound DevOps and waits — a synchronous investigation, the call returns only after DevOps hands the result back. intent is the question to ask; reportMapping is required, a record of anchor number to null (keep) or summary string (replace) with no prefixUntil, and a missing or malformed mapping is refused with VIEW_DECLARATION_INVALID.
    // Example: await this.devops("find the flaky test", { 1: null });
  }
