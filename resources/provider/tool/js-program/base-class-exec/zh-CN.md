  async call(command, timeoutOrOptions) {
    // 返回 { ok, status, exitCode, stdout, stderr, truncated, bytes, durationMs }。command 是一条 shell 字符串。可选的 timeoutOrOptions 显式指定超时时限（毫秒数值或 { timeoutMs } 对象），缺省时使用统一稳定默认时限（10000ms）。文本长度不计价时限。stdout/stderr 是上一次自动 park 所带增量之后的新输出。
    // 例如: const outcome = await this.call("printf ok");
  }

  async signal(signal) {
    // 向让本次程序 park 的那条 call 发信号。那条命令已经结束时是 CALL_CHILD_UNKNOWN。
  }

  async send(input) {
    // 把键盘输入写进让本次程序 park 的那条 call。缺行尾换行时补一个。那条命令已经结束时是 CALL_CHILD_UNKNOWN。
  }
