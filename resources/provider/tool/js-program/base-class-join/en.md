  async join() {
    // Returns { ok, records, byName }: waits for this session's delivered children (completed | failed | abandoned | voided); a child that has not delivered but can still deliver keeps the join open, while a voided one is reported once and then retires.
    // Example: const joined = await this.join();
  }
