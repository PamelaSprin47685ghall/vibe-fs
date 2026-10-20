  async park(payload) {
    // 挂起本程序，把控制权交回编排层；工具调用返回 { handle, payload }，调用方用 { unpark: { handle, value } } 续接，或用 { abandon: { handle } } 放弃。
    // 例如: const decision = await this.park({ stage: "review" });
  }

  async deliver(reportMapping) {
    // 以交付视图结束本程序。先在助手正文把结果讲成散文——不写进映射——再传入 reportMapping —— Record<string, null|string>，把锚号映射到摘要（值为 null 表示该锚不摘要）—— 指向本次工作的全部材料，包括那份散文和它依赖的上下文；宿主按 { prefixUntil: 0, thenMap: reportMapping } 解读，不再接受 { prefixUntil, thenMap } 包装对象。不要把散文复制进 deliver 参数，也不要留到调用之后再写：调用之后没有以后。reportMapping 缺失或非法时被拒绝：调用返回 { ok: false, code: "VIEW_DECLARATION_INVALID", reason }，这份拒绝不构成交付，程序继续运行。
    // 例如: await this.deliver({ 1: "setup summary", 2: null });
  }

  async run() {
    // 程序入口：你的类实现它，返回值即本程序的结果（须是 JSON 兼容值）。
    // 例如: async run() { return { ok: true }; }
  }
