  async dag(spec) {
    // 返回 { ok, results, skipped }。按依赖波次 fork，每波 join。
    // 失败节点以 { dag, node, code, deps, done, pending } 挂起。注入 "retry"、"skip"、替代结果或 { abort: reason }。
    // 例如: await this.dag({ nodes: [{ id: "spec", charge: "写出接口规格" }] });
  }
