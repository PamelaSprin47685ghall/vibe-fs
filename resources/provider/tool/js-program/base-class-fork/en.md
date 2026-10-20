  async fork(payload) {
    // Returns { ok, name, agentId, outcome }: delegates one charge to a child work. payload is a string (the charge) or { charge, name?, calling?, keywords? }.
    // Example: await this.fork("index the parser module");
  }

  async dag(spec) {
    // Returns { ok, results, skipped }: declarative dependency sugar compiled into fork waves plus one join per wave; a failed node suspends with { dag, node, code, deps, done, pending }, and the injected value is "retry", "skip", a replacement result or { abort: reason }.
    // Example: await this.dag({ nodes: [{ id: "spec", charge: "write the interface spec" }] });
  }
