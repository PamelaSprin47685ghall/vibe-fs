  async review(findings) {
    // Submits review findings as an array of { key, value: string | null }.
    // Value null deletes the key; [] does not clear existing ledger.
    // Only the first review whose resulting ledger is empty grants promotion eligibility; later calls only apply keyed updates without altering eligibility. Returns data result.
    // Example: const result = await this.review([{ key: "gap-1", value: "add tests" }]);
  }
