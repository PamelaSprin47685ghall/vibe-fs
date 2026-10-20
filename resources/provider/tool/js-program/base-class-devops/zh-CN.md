  async devops(intent, reportMapping) {
    // 返回 DevOps 的完整工作记录：把一句调查问题交给本任务绑定的固定 DevOps 并等待结果——同步调查，DevOps 交回结果调用才结束。intent 是要问的那句话；reportMapping 必填，形态是「锚点编号 → null（保留）或摘要字符串（替换）」的记录，不带 prefixUntil，缺失或非法以 VIEW_DECLARATION_INVALID 拒绝。
    // 例如: await this.devops("找出那个不稳定的测试", { 1: null });
  }
