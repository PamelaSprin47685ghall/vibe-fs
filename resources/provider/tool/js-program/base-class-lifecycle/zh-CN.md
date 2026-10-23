  async park(payload) {
    // 挂起本程序，把控制权交回编排层；工具调用返回 { handle, payload }，调用方用 { unpark: { handle, value } } 续接，或用 { abandon: { handle } } 放弃。
    // 例如: const decision = await this.park({ stage: "review" });
  }

  async run() {
    // 程序入口：你的类实现它，返回值即本程序的结果（须是 JSON 兼容值）。
    // 例如: async run() { return { ok: true }; }
  }

