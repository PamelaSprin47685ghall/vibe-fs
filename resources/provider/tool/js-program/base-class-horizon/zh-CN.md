  async horizon() {
    // 返回 { ok, agents, ptys }: 地平线名册，纯数据——每个参与者的状态（returned | still-away | did-not-return | voided；已作废的已无法交付，因此以作废结清，而非完成）。它不推送，也不轮询。
    // 例如: const atHorizon = await this.horizon();
  }
