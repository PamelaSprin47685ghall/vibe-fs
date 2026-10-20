  async resume(payload, reportMapping) {
    // Returns { ok, name, agentId, outcome }: continues one charge on the work that already carries this name — no other office is chosen, no new identity is created, and calling is not accepted here. payload is a string (the charge) or { charge, name? }; a missing name is derived from the charge. reportMapping is required and is a Record<string, null|string> of anchor number to summary or null, with no prefixUntil — the host reads it as { prefixUntil: 0, thenMap: reportMapping }; a missing or invalid mapping is refused with { ok: false, code: "VIEW_DECLARATION_INVALID" }.
    // Example: await this.resume("continue the audit", { 12: "audit inventory" });
  }
