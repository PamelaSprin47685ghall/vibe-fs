  async call(command, planB) {
    // 返回 { ok, status, exitCode, stdout, stderr, truncated, bytes, durationMs }。command 是一条 shell 字符串。planB 必填，是不跑这条命令时的替代方案；它的 UTF-8 字节数 × 50ms 是自动 yield 的时限。缺 planB 以 CALL_COMMAND_UNSUPPORTED 失败。stdout/stderr 是上一次自动 park 所带增量之后的新输出。
    // 例如: const outcome = await this.call("printf ok", "直接读文件");
    // 传入继承 this.Callable 的类时，执行带 sanity、next、watch 和 maxDuration 的宿主工程循环；该形态不传 planB。
  }

  async signal(signal) {
    // 向让本次程序 park 的那条 call 发信号。那条命令已经结束时是 CALL_CHILD_UNKNOWN。
  }

  async send(input) {
    // 把键盘输入写进让本次程序 park 的那条 call。缺行尾换行时补一个。那条命令已经结束时是 CALL_CHILD_UNKNOWN。
  }
  Callable = Callable;
