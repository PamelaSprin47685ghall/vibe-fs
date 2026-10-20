  async fork(payload) {
    // 返回 { ok, name, agentId, outcome }: 把一份差事委派给子工作。payload 是字符串（差事）或 { charge, name?, calling?, keywords? }。
    // 例如: await this.fork("为解析器模块建索引");
  }

  async dag(spec) {
    // 返回 { ok, results, skipped }: 声明式依赖糖，编译成若干 fork 波次加每波一次 join；失败节点带着 { dag, node, code, deps, done, pending } 挂起，注入 "retry"、"skip"、替代结果或 { abort: reason }。
    // 例如: await this.dag({ nodes: [{ id: "spec", charge: "写出接口规格" }] });
  }
