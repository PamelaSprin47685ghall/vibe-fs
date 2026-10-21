  async join() {
    // 返回 { ok, records, byName }: 等本会话已交付的子工作回来（completed | failed | abandoned | voided）；未交付且仍可能交付的子工作让 join 保持不关闭，已作废的则报告一次后退役。
    // 例如: const joined = await this.join();
  }
