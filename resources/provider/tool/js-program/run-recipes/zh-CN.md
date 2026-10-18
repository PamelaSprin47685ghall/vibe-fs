## 编译大型工程

```js
class Js extends JsProgram {
  async run() {
    return await js.run(class extends js.Runnable {
      maxDuration = 900000; // 15 分钟工程总上界
      interval = 10000;
      failureReason = null;

      next(ctx) {
        if (this.failureReason) return `sleep ${this.interval / 1000 + 2}`;
        return ctx.step === 0 ? "cmake --build build --parallel" : null;
      }

      // 只有 check() 能返回 { abort }；非零退出后借一个保持时间片让 check() 报出稳定
      // 中止原因。
      check(ctx) {
        if (this.failureReason) return { abort: this.failureReason };
        if (ctx.elapsedMs > 600000 && ctx.remainingMs < 30000) {
          return { abort: "build_time_loss: under 30s left after 10m elapsed" };
        }
        return true;
      }

      onExit(result) {
        if (result.exitCode === 0) return;
        this.failureReason = `build_failed:exit_${result.exitCode}`;
      }
    });
  }
}
```

## npm 服务 + 前端测试

```js
class Js extends JsProgram {
  async run() {
    let testExitCode = null;
    const outcome = await js.run(class extends js.Runnable {
      maxDuration = 300000; // 服务启动 + 测试套件的 5 分钟上界
      interval = 2000;

      next(ctx) {
        if (ctx.step === 0) return { spawn: "npm run dev", name: "srv" };
        if (ctx.step === 1) return { waitFor: { port: 5173, timeoutMs: 30000 } };
        if (ctx.step === 2) return "npm run test:e2e";
        if (ctx.step === 3) return { signal: "SIGTERM", name: "srv" };
        return null;
      }

      check(ctx) {
        const srv = ctx.backgrounds.find((b) => b.name === "srv");
        if (srv && srv.exited && srv.exitCode !== 0) {
          return { abort: `server_crashed:exit_${srv.exitCode}` };
        }
        return true;
      }

      onExit(result) {
        testExitCode = result.exitCode;
      }
    });
    return { ...outcome, testExitCode };
  }
}
```

交互形态（`{ send: { name, input } }`）同样可用于 REPL / 类终端子进程：先用 `{ spawn, name }` 启动子进程，再用 `{ send: { name, input } }` 向其 stdin 写入行，并在 `check(ctx)` 中读取本时间片的 `ctx.stdout` / `ctx.stderr` 增量——系统中已不再有 `pty` 工具。
