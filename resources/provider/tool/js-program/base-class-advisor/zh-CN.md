  async advisor(intent, carry = []) {
    // 返回 { ok, advice }，请教本会话专属顾问。顾问席不可用时返回 { ok: false, code: "advisor-unavailable", reason }。
    // intent 写问题。carry 只补充所需历史：整数引用锚点，字符串是字面文本。本回合不传。
    // 例如: const advice = await this.advisor("重试路径是否可靠？", [2, "已验证首次失败会原样重试"]);
  }

