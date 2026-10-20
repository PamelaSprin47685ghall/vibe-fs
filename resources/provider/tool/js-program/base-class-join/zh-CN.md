  async join() {
    // 返回 { ok, records, byName }: 等本会话已交付的子工作回来（completed | failed | abandoned）；未交付的子工作让 join 保持不关闭。
    // 例如: const joined = await this.join();
  }
