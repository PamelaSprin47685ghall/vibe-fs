  async fork(payload, reportMapping) {
    // Returns { ok, name, agentId, outcome }: delegates one charge to a child work. payload is a string (the charge) or { charge, name?, calling?, keywords? }. reportMapping is required and is a Record<string, null|string> of anchor number to summary or null, with no prefixUntil — the host reads it as { prefixUntil: 0, thenMap: reportMapping }; a missing or invalid mapping is refused with { ok: false, code: "VIEW_DECLARATION_INVALID" }.
    // Example: await this.fork("index the parser module", { 12: "parser inventory" });
  }
