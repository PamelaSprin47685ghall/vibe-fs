  async park(payload) {
    // 挂起本程序，把控制权交回编排层；工具调用返回 { handle, payload }，调用方用 { unpark: { handle, value } } 续接，或用 { abandon: { handle } } 放弃。
    // 例如: const decision = await this.park({ stage: "review" });
  }

  async deliver(value, view) {
    // 用显式终值结束本程序。value 是交出的数据；可选 view 用 { prefixUntil, thenMap } 选择回传材料，与其它入口采用同一视图规则。
    // 例如: await this.deliver({ done: true });
  }

  async run() {
    // 程序入口：你的类实现它，返回值即本程序的结果（须是 JSON 兼容值）。
    // 例如: async run() { return { ok: true }; }
  }
