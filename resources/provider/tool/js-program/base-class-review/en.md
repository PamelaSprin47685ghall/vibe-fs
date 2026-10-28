  async review(findings, note) {
    // Submits review findings as an array of { key, value: string | null }: each key names one obligation, value is its remaining work, null deletes the key, and [] changes nothing.
    // When the obligations are all cleared the tenure relays: a successor's first review with no obligations advances the milestone, and an originator with no obligations makes the system ask the user. Returns data result.
    // Reviewed is recorded now; AwaitReview means your next review is welcome, not a verdict to wait for with join().
    // note (optional string): when this review launches a Manager, the note is its title and full draft; without it the saved draft is sent.
    // Example: const result = await this.review([{ key: "gap-1", value: "add tests" }]);
  }
