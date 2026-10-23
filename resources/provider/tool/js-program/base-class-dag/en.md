  async dag(spec) {
    // Returns { ok, results, skipped }. Forks in dependency waves and joins each wave.
    // A failed node suspends with { dag, node, code, deps, done, pending }. Inject "retry", "skip", a replacement result, or { abort: reason }.
    // Example: await this.dag({ nodes: [{ id: "spec", charge: "write the interface spec" }] });
  }
