## Compile a large project

```js
class Js extends JsProgram {
  async run() {
    return await js.run(class extends js.Runnable {
      maxDuration = 900000; // 15 min engineering bound
      interval = 10000;
      failureReason = null;

      next(ctx) {
        if (this.failureReason) return `sleep ${this.interval / 1000 + 2}`;
        return ctx.step === 0 ? "cmake --build build --parallel" : null;
      }

      // Only check() can return { abort }; after a non-zero exit, one hold slice
      // lets check() raise the stable abort reason.
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

## npm server + frontend tests

```js
class Js extends JsProgram {
  async run() {
    let testExitCode = null;
    const outcome = await js.run(class extends js.Runnable {
      maxDuration = 300000; // 5 min bound for server startup + test suite
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

Interactive form (`{ send: { name, input } }`) is also available for REPL/terminal-like children: spawn the process with `{ spawn, name }`, inject lines via `{ send: { name, input } }`, and read the incremental `ctx.stdout` / `ctx.stderr` deltas in `check(ctx)` — no `pty` tool exists any more.
