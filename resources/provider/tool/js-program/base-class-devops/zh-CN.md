  async devops(intent, extra = []) {
    // 返回 { ok, kind, status, returned: [{ text }] }，等待本任务绑定的 DevOps 交回调查记录。席位尚未绑定或已关闭时以 assessment-pending-for-devops 拒绝。
    // intent 写调查问题。extra 只补充所需历史：整数引用锚点，字符串是字面文本。本回合不传。
    // 例如: await this.devops("找出那个不稳定的测试", [1]);
  }
