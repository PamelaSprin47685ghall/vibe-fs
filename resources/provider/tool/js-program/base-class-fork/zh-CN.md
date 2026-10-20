  async fork(payload, reportMapping) {
    // 返回 { ok, name, agentId, outcome }: 把一份差事委派给子工作。payload 是字符串（差事）或 { charge, name?, calling?, keywords? }。reportMapping 必填，形状是 Record<string, null|string>，把锚点编号映到摘要或 null，不带 prefixUntil——宿主按 { prefixUntil: 0, thenMap: reportMapping } 解释；缺省或非法一律拒绝并返回 { ok: false, code: "VIEW_DECLARATION_INVALID" }。
    // 例如: await this.fork("为解析器模块建索引", { 12: "解析器清单" });
  }

  async dag(spec, reportMapping) {
    // 返回 { ok, results, skipped }: 声明式依赖糖，编译成若干 fork 波次加每波一次 join；同一份 reportMapping 发给所有节点，节点不得替换自己那一份——reportMapping 必填，形状是 Record<string, null|string>，把锚点编号映到摘要或 null，不带 prefixUntil——宿主按 { prefixUntil: 0, thenMap: reportMapping } 解释；失败节点带着 { dag, node, code, deps, done, pending } 挂起，注入 "retry"、"skip"、替代结果或 { abort: reason }。
    // 例如: await this.dag({ nodes: [{ id: "spec", charge: "写出接口规格" }] }, { 12: "解析器清单" });
  }
