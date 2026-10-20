  async commission(payload, reportMapping) {
    // 返回 { ok, name, agentId, outcome }: 把一条差事托付给一位 Manager 独立推进、目的地自有的一条道路——只有目的是独立时才另开道路，不因工作量大或已进入下一阶段就另起一条。payload 是 { charge, name?, calling? }；charge 必填，缺 name 时由 charge 派生，calling 指明主导这条道路的职位（省略时推导为 lead）——继续已知道的道路时省略 calling，沿用它自己的名字。reportMapping 必填，形状是 Record<string, null|string>，把锚点编号映到摘要或 null，不带 prefixUntil——宿主按 { prefixUntil: 0, thenMap: reportMapping } 解读；缺省或非法一律拒绝并返回 { ok: false, code: "VIEW_DECLARATION_INVALID" }。
    // 例如: await this.commission({ charge: "单独审计解析器模块" }, { 12: "审计清单" });
  }
