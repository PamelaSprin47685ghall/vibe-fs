  async fork(payload, reportMapping) {
    // Returns { ok, name, agentId, outcome }: delegates one charge to a child work. payload is a string (the charge) or { charge, name?, calling?, keywords? }. reportMapping is required and is a Record<string, null|string> of anchor number to summary or null, with no prefixUntil — the host reads it as { prefixUntil: 0, thenMap: reportMapping }; a missing or invalid mapping is refused with { ok: false, code: "VIEW_DECLARATION_INVALID" }.
    // Example: await this.fork("index the parser module", { 12: "parser inventory" });
  }

  async dag(spec, reportMapping) {
    // Returns { ok, results, skipped }: declarative dependency sugar compiled into fork waves plus one join per wave; one reportMapping goes to every node and a node cannot replace its own copy — reportMapping is required and is a Record<string, null|string> of anchor number to summary or null, with no prefixUntil — the host reads it as { prefixUntil: 0, thenMap: reportMapping }; a failed node suspends with { dag, node, code, deps, done, pending }, and the injected value is "retry", "skip", a replacement result or { abort: reason }.
    // Example: await this.dag({ nodes: [{ id: "spec", charge: "write the interface spec" }] }, { 12: "parser inventory" });
  }
