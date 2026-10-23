  async join() {
    // Returns { ok, records }: each record has name, agentId, role, status, and returned: [{ text }]. Waits for this session's children (completed | failed | abandoned | voided); an active child keeps join open, while a voided one is reported once and then retires.
    // Example: const joined = await this.join();
  }
