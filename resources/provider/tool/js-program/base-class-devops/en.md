  async devops(intent, carry = []) {
    // Returns { ok, kind, status, returned: [{ text }] } after this task's bound DevOps returns the investigation record. An unbound or closed seat refuses with assessment-pending-for-devops.
    // intent states the question. carry adds needed history: integers reference anchors; strings supply literal text. The current turn is not sent.
    // Example: await this.devops("find the flaky test", [1]);
  }

