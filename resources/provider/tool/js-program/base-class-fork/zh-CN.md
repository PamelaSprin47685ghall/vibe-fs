  async fork(payload) {
    // 返回 { ok, name, agentId, outcome }。payload 是任务字符串或 { charge, name?, calling? }；charge 以散文承载消息，name、calling 是路由元数据。
    // 例如: await this.fork("为解析器模块建索引");
  }
