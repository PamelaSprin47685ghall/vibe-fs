  async advisor(intent) {
    // Returns { ok, context } from this session's advisor as TOML. An unavailable seat returns { ok: false, code: "advisor-unavailable", reason }.
    // Sends a single request containing intent and caller post-scope context; the advisor's natural stop returns its full post-scope session.
    // Example: const { ok, context } = await this.advisor("is the retry path sound?");
  }
