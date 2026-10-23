  async fork(payload, carry = []) {
    // 返回 { ok, name, agentId, outcome }。payload 是任务字符串或 { charge, name?, calling? }；charge 以散文承载消息，name、calling 是路由元数据。
    // carry 只补充所需历史：整数引用锚点，字符串是字面文本。本回合不传，任务只写在 charge 中。
    // 例如: await this.fork("为解析器模块建索引", [12, "只需列出公开入口"]);
  }

