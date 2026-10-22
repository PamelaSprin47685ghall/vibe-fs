  async dag(spec, reportMapping) {
    // Returns { ok, results, skipped }: declarative dependency sugar compiled into fork waves plus one join per wave; one reportMapping goes to every node and a node cannot replace its own copy — reportMapping is required and is a Record<string, null|string> of anchor number to summary or null, with no prefixUntil — the host reads it as { prefixUntil: 0, thenMap: reportMapping }; a failed node suspends with { dag, node, code, deps, done, pending }, and the injected value is "retry", "skip", a replacement result or { abort: reason }.
    // Example: await this.dag({ nodes: [{ id: "spec", charge: "write the interface spec" }] }, { 12: "parser inventory" });
  }
