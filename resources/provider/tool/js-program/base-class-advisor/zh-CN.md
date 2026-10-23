  async advisor(intent) {
    // 返回 { ok, context }，请教本会话专属顾问，格式为 TOML。顾问席不可用时返回 { ok: false, code: "advisor-unavailable", reason }。
    // 单次请求发送 intent 与调用方当前 scope 上下文；顾问自然停止后返回其完整 scope session。
    // 例如: const { ok, context } = await this.advisor("重试路径是否可靠？");
  }
