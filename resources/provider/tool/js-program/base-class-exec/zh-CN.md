  // 可继承的工程循环基类：this.call(class extends this.Callable { ... }) 的父类。
  Callable = Callable;

  async call(callableClass) {
    // 返回 { ok, status, reason, exitCode, stdout, stderr, truncated, bytes, durationMs, steps }: 把一个继承 this.Callable 的类交给宿主的工程循环执行。宿主先跑 sanity 自证，再逐轮调 next，每个时间片调 watch，最后按 maxDuration 收口。
    // 例如: const outcome = await this.call(class extends this.Callable { ... });
  }
