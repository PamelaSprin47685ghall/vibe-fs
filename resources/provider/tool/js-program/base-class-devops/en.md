  async devops(intent) {
    // Returns { ok, kind, status, returned: [{ text }] } after this task's bound DevOps returns the investigation record. An unbound or closed seat refuses with assessment-pending-for-devops.
    // intent states the question.
    // Example: await this.devops("find the flaky test");
  }
