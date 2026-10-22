  async join() {
    // 返回 { ok, records }：每条记录含 name、agentId、role、status 和 returned: [{ text }]。等本会话子工作回来（completed | failed | abandoned | voided）；仍可能交付的子工作让 join 继续等待，已作废的则报告一次后退役。
    // 例如: const joined = await this.join();
  }
