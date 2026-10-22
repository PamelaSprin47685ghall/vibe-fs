  async fork(payload, reportMapping) {
    // 返回 { ok, name, agentId, outcome }: 把一份差事委派给子工作。payload 是字符串（差事）或 { charge, name?, calling?, keywords? }。reportMapping 必填，形状是 Record<string, null|string>，把锚点编号映到摘要或 null，不带 prefixUntil——宿主按 { prefixUntil: 0, thenMap: reportMapping } 解释；缺省或非法一律拒绝并返回 { ok: false, code: "VIEW_DECLARATION_INVALID" }。
    // 例如: await this.fork("为解析器模块建索引", { 12: "解析器清单" });
  }
