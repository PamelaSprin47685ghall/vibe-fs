  async advisor(intent, extra = []) {
    // Returns { ok, advice } from this session's advisor. An unavailable seat returns { ok: false, code: "advisor-unavailable", reason }.
    // intent states the question. extra adds needed history: integers reference anchors; strings supply literal text. The current turn is not sent.
    // Example: const advice = await this.advisor("is the retry path sound?", [2, "Verified: the first failure retries unchanged"]);
  }
