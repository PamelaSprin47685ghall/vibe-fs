  async horizon() {
    // Returns { ok, agents, ptys }: the horizon roster as data — each participant's status (returned | still-away | did-not-return | voided; a voided one can no longer return, so it is settled rather than completed). It does not push and does not poll.
    // Example: const atHorizon = await this.horizon();
  }
