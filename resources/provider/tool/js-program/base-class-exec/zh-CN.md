  async call(command) {
    // 返回 { ok, status, exitCode, stdout, stderr, truncated, bytes, durationMs }。command 是一条 shell 字符串。stdout/stderr 是上一次自动 park 所带增量之后的新输出。函数或类参数是 CALL_COMMAND_UNSUPPORTED。
    // 例如: const outcome = await this.call("printf ok");
  }

  async signal(signal) {
    // 向让本次程序 park 的那条 call 发信号。那条命令已经结束时是 CALL_CHILD_UNKNOWN。
  }

  async send(input) {
    // 把键盘输入写进让本次程序 park 的那条 call。缺行尾换行时补一个。那条命令已经结束时是 CALL_CHILD_UNKNOWN。
  }
