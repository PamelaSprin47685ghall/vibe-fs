  async resume(payload, reportMapping) {
    // 返回 { ok, name, agentId, outcome }: 把一条差事续给已经带着这个名字的既有工作——不另选职位，不新建身份，这里也不接受 calling。payload 是 { charge, name }：name 指明要续的那条道路，必填，缺失时以 `name-required` 拒绝；charge 承载这一轮的差事。reportMapping 必填，形状是 Record<string, null|string>，把锚点编号映到摘要或 null，不带 prefixUntil——宿主按 { prefixUntil: 0, thenMap: reportMapping } 解读；缺省或非法一律拒绝并返回 { ok: false, code: "VIEW_DECLARATION_INVALID" }。
    // 例如: await this.resume({ name: "audit-road", charge: "继续那项审计" }, { 12: "审计清单" });
  }
