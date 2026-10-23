  async park(payload) {
    // 挂起本程序，把控制权交回编排层；工具调用返回 { handle, payload }，调用方用 { unpark: { handle, value } } 续接，或用 { abandon: { handle } } 放弃。
    // 例如: const decision = await this.park({ stage: "review" });
  }

  async deliver(extra = []) {
    // 惯用法：按自己的判断先用助手正文收束工作，再立即 this.deliver()。本回合自动回传，不用复制正文或指定锚点。不设固定章节。
    // extra 仅补充旧上下文：整数引用锚点，字符串是字面文本。交付结束程序；非法数组返回 VIEW_DECLARATION_INVALID，修正后重试。
    // 例如: await this.deliver();
  }

  async run() {
    // 程序入口：你的类实现它，返回值即本程序的结果（须是 JSON 兼容值）。
    // 例如: async run() { return { ok: true }; }
  }

