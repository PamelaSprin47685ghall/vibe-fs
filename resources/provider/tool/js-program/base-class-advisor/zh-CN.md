  async advisor(intent, reportMapping) {
    // 返回 { ok, advice }: 请教本会话专属顾问席，答复以散文数据带回，不是渲染。reportMapping 必填，形态是 Record<string, null|string>，把锚号映射到摘要（值为 null 表示该锚不摘要）；宿主按 { prefixUntil: 0, thenMap: reportMapping } 解读。reportMapping 缺失或非法时以 { ok: false, code: "VIEW_DECLARATION_INVALID", reason } 拒绝，不会被静默当成空映射；顾问席不可用时以 { ok: false, code: "advisor-unavailable", reason } 拒绝。
    // 例如: const advice = await this.advisor("重试路径是否可靠？", { 1: "setup summary", 2: null });
  }
