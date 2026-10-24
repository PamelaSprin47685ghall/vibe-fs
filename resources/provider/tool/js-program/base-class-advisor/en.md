  async advisor(intent) {
    // Returns { ok, advice } from this session's advisor. An unavailable seat returns { ok: false, code: "advisor-unavailable", reason }.
    // Sends a single request containing intent and caller post-scope context; the advisor's natural stop returns only its final assistant prose.
    // Example: const { ok, advice } = await this.advisor("is the retry path sound?");
  }
