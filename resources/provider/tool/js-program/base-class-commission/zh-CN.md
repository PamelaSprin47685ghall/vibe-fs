  async commission(payload) {
    // 返回 { ok, name, agentId, outcome }。只有目的独立时才另开 Manager 道路，不因工作量大或进入下一阶段而另起一条。
    // payload 是 { charge, name?, calling? }；charge 以散文承载消息，name、calling 是路由元数据。缺 name 时由 charge 派生；calling 默认 lead。继续已有道路时沿用其 name，省略 calling。
    // 例如: await this.commission({ charge: "单独审计解析器模块" });
  }
