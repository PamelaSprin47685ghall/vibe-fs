  async rg(argv) {
    // 返回 { exitCode, messages } 或 { exitCode, paths }: 一次 ripgrep 调用，内容检索等价于 rg --json 加这个 argv（--files 等摘要模式因 rg 拒绝与 --json 同用而直接透传）。argv 是字符串数组，不要自己加引号，也不要自己传 --json；不带路径的检索不搜工作区。exitCode 0 表示命中，1 表示检索成功但无命中，不是失败。
    // 例如: const found = await this.rg(["RetryPolicy", "src", "-g", "*.fs"]);
  }
