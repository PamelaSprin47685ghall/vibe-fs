  async park(payload) {
    // 在此 await 点挂起并交回 { handle, payload }。公开工具入参始终只有 { program: string }；恢复／放弃走专用生命周期接口，不是 Host 工具参数。
    // 例如: const decision = await this.park({ stage: "review" });
  }

  async run() {
    // 程序入口：你的类实现它，返回值即本程序的结果（须是 JSON 兼容值）。
    // 例如: async run() { return { ok: true }; }
  }

