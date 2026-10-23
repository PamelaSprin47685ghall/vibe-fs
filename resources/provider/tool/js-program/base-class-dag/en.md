  async dag(spec, extra = []) {
    // Returns { ok, results, skipped }. Forks in dependency waves and joins each wave. All nodes receive the same extra and cannot override it. Integers reference history anchors; strings supply literal text. The current turn is not sent.
    // A failed node suspends with { dag, node, code, deps, done, pending }. Inject "retry", "skip", a replacement result, or { abort: reason }.
    // Example: await this.dag({ nodes: [{ id: "spec", charge: "write the interface spec" }] }, [12]);
  }
