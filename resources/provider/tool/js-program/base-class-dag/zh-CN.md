  async dag(spec, carry = []) {
    // 返回 { ok, results, skipped }。按依赖波次 fork，每波 join。所有节点收到同一份 carry，不能自行覆盖；整数引用历史锚点，字符串是字面文本，本回合不传。
    // 失败节点以 { dag, node, code, deps, done, pending } 挂起。注入 "retry"、"skip"、替代结果或 { abort: reason }。
    // 例如: await this.dag({ nodes: [{ id: "spec", charge: "写出接口规格" }] }, [12]);
  }

