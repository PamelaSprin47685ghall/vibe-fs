  async advisor(intent) {
    // 返回 { ok, advice }，请教本会话专属顾问。顾问席不可用时返回 { ok: false, code: "advisor-unavailable", reason }。
    // 单次请求发送 intent 与调用方当前 scope 上下文；顾问自然停止后只交回其末轮 assistant 散文。
    // 例如: const { ok, advice } = await this.advisor("重试路径是否可靠？");
  }
