// 工程循环基类：this.call(class extends this.Callable { ... }) 的父类。
class Callable {
  // 整个工程的执行上限（毫秒，正数），必填。
  maxDuration = 0;

  // 每个时间片的毫秒数，默认 10000。
  interval = 10000;

  // next(ctx)：宿主每轮调用一次；返回命令字符串、null（做完了）、{ spawn, name }、{ signal, name }、{ send: { name, input } } 或 { waitFor: { port, timeoutMs } }。ctx = { root, step }。
  next(ctx) {}

  // watch(ctx)：每个时间片调用一次，先读 ctx 增量再裁决；返回 true 继续、false 或 { abort: reason } 否决、{ park: payload } 挂起。ctx = { root, step, stdout, stderr, exited, exitCode, backgrounds, elapsedMs, remainingMs }。
  watch(ctx) {}

  // sanity()：控制流自证，返回 { nextDone, watchVeto } 两个 thunk，各自只把实例拨到目标状态并返回参数数组。
  sanity() {}

  // onExit(result)：子进程退出时的处置；result = { root, exitCode, stdout, stderr, elapsedMs }。
  onExit(result) {}
}
