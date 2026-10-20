  async resume(payload, reportMapping) {
    // 返回 { ok, name, agentId, outcome }: 把一条差事续给已经带着这个名字的既有工作——不另选职位，不新建身份，这里也不接受 calling。payload 是字符串（差事）或 { charge, name? }；缺 name 时由差事派生。reportMapping 必填，形状是 Record<string, null|string>，把锚点编号映到摘要或 null，不带 prefixUntil——宿主按 { prefixUntil: 0, thenMap: reportMapping } 解读；缺省或非法一律拒绝并返回 { ok: false, code: "VIEW_DECLARATION_INVALID" }。
    // 例如: await this.resume("继续那项审计", { 12: "审计清单" });
  }
