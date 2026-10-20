import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  execute,
  caseName,
  parkedHandle,
  parkedPayload,
  render,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-run-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
  }
}`

const runProgram = (dir, body, sessionId = 'sess-050') =>
  execute(dir, 'DevOps', 'en', sessionId, { program: program(body) }, 5000, Date.now() + 60_000, 1 << 20, null)

const runText = async (dir, body) => render(await runProgram(dir, body))

test('WHAT[repository-programming-050] call proofs use argument arrays, preserve this and ignore a forged method apply', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(dir, `
      let calls = 0;
      const result = await js.call(class extends js.Callable {
        maxDuration = 1000;
        value = 7;
        next(value) { if (value === 7) { calls++; return null; } return null; }
        watch(value) { return value === this.value ? false : true; }
        sanity() {
          this.next.apply = () => { throw new Error('forged apply used'); };
          return { nextDone: () => [7], watchVeto: () => [7] };
        }
      });
      return { result, calls, oldEntry: typeof js.run, oldBase: typeof js.Runnable };
    `)
    assert.equal(caseName(outcome), 'Succeeded', render(outcome))
    assert.match(render(outcome), /calls = 1/)
    assert.match(render(outcome), /oldEntry = "undefined"/)
    assert.match(render(outcome), /oldBase = "undefined"/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] a no-op legacy proof cannot bypass the actual nonterminating next', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(dir, `return await js.call(class extends js.Callable {
      maxDuration = 1000;
      next() { return "touch bypass.marker"; }
      watch() { return true; }
      sanity() { return { nextDone: () => null, watchVeto: () => false }; }
    });`)
    assert.equal(caseName(outcome), 'Parked', render(outcome))
    assert.equal(existsSync(join(dir, 'bypass.marker')), false)
    assert.match(JSON.stringify(parkedPayload(outcome)), /argument array/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] actual proof callbacks and unresolved argument promises consume the same finite deadline', async () => {
  const { dir, cleanup } = sandbox()
  try {
    for (const proof of ['next', 'watch', 'promise']) {
      const outcome = await runProgram(dir, `return await js.call(class extends js.Callable {
        maxDuration = 100;
        next() { ${proof === 'next' ? 'while (true) {}' : 'return null;'} }
        watch() { ${proof === 'watch' ? 'while (true) {}' : 'return false;'} }
        sanity() { return {
          nextDone: () => ${proof === 'promise' ? 'new Promise(() => {})' : '[]'},
          watchVeto: () => []
        }; }
      });`)
      assert.equal(caseName(outcome), 'Failed', render(outcome))
      assert.match(render(outcome), /CALL_SANITY_TIMEOUT/)
    }
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] a Callable missing watch is refused with CALLABLE_SPEC_INCOMPLETE', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const doc = await runText(
      dir,
      `    return await js.call(class { maxDuration = 1000; next() { return null; } });`,
    )
    assert.match(doc, /# failed/)
    assert.match(doc, /CALLABLE_SPEC_INCOMPLETE/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] a Callable missing sanity is refused with CALLABLE_SPEC_INCOMPLETE', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const doc = await runText(
      dir,
      `    return await js.call(class extends js.Callable {
      maxDuration = 1000;
      next(ctx) { return ctx.step === 0 ? "echo hello" : null; }
      watch() { return true; }
    });`,
    )
    assert.match(doc, /# failed/)
    assert.match(doc, /CALLABLE_SPEC_INCOMPLETE/)
    assert.match(doc, /sanity/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] the host loop runs one foreground command and reports the structured outcome', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const doc = await runText(
      dir,
      `    return await js.call(class extends js.Callable {
      maxDuration = 5000;
      interval = 100;
      next(ctx) { return ctx.step === 0 ? "echo hello" : null; }
      watch(ctx) {
        this.lastExit = ctx.exitCode;
        if (ctx.exited && ctx.exitCode !== 0) return { abort: "command failed" };
        return true;
      }
      sanity() {
        return {
          nextDone: () => [{ root: "/", step: 1 }],
          watchVeto: () => [{ root: "/", step: 0, stdout: "", stderr: "", exited: true, exitCode: 2, backgrounds: [], elapsedMs: 0, remainingMs: 60000 }],
        };
      }
    });`,
    )
    assert.match(doc, /# ok/)
    assert.match(doc, /status = "completed"/)
    assert.match(doc, /hello/)
    assert.match(doc, /steps = 2/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] onExit dispatches every exited command with its exit code', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const doc = await runText(
      dir,
      `    const dispatched = [];
    const outcome = await js.call(class extends js.Callable {
      maxDuration = 5000;
      interval = 100;
      next(ctx) { return ctx.step === 0 ? "exit 4" : null; }
      watch(ctx) {
        if (ctx.exited && ctx.exitCode !== 0) return { abort: "command failed" };
        return true;
      }
      onExit(result) { dispatched.push(result.exitCode); }
      sanity() {
        return {
          nextDone: () => [{ root: "/", step: 1 }],
          watchVeto: () => [{ root: "/", step: 0, stdout: "", stderr: "", exited: true, exitCode: 2, backgrounds: [], elapsedMs: 0, remainingMs: 60000 }],
        };
      }
    });
    return { outcome, dispatched };`,
    )
    assert.match(doc, /# ok/)
    assert.match(doc, /dispatched = \[4\]/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] a watch abort kills the foreground command and reports aborted', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const doc = await runText(
      dir,
      `    const outcome = await js.call(class extends js.Callable {
      maxDuration = 8000;
      interval = 200;
      next(ctx) { return ctx.step === 0 ? "sleep 5 && touch late.marker" : null; }
      watch() { return { abort: "stalled" }; }
      sanity() {
        return {
          nextDone: () => [{ root: "/", step: 1 }],
          watchVeto: () => [{ root: "/", step: 0, stdout: "", stderr: "", exited: true, exitCode: 2, backgrounds: [], elapsedMs: 0, remainingMs: 60000 }],
        };
      }
    });
    return outcome;`,
    )
    assert.match(doc, /status = "aborted"/)
    assert.equal(existsSync(join(dir, 'late.marker')), false, 'the killed command must not finish')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] watch returning false is a real veto, not a synonym for continue', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const doc = await runText(
      dir,
      `    const outcome = await js.call(class extends js.Callable {
      maxDuration = 8000;
      interval = 100;
      next(ctx) { return ctx.step === 0 ? "sleep 5 && touch veto.marker" : null; }
      watch() { return false; }
      sanity() {
        return { nextDone: () => [{ root: "/", step: 1 }], watchVeto: () => [{}] };
      }
    });
    return outcome;`,
    )
    assert.match(doc, /status = "aborted"/)
    assert.equal(existsSync(join(dir, 'veto.marker')), false)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] a watch park suspends the run and resumes after unpark', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const parked = await runProgram(
      dir,
      `    const outcome = await js.call(class extends js.Callable {
      maxDuration = 5000;
      interval = 50;
      next(ctx) { return ctx.step === 0 ? "sleep 0.3" : null; }
      watch(ctx) {
        if (!this.parked) { this.parked = true; return { park: { reason: "review" } }; }
        if (ctx.exited && ctx.exitCode !== 0) return { abort: "failed" };
        return true;
      }
      sanity() {
        return {
          nextDone: () => [{ root: "/", step: 1 }],
          watchVeto: () => {
            this.parked = true;
            return [{ root: "/", step: 0, stdout: "", stderr: "", exited: true, exitCode: 2, backgrounds: [], elapsedMs: 0, remainingMs: 60000 }];
          },
        };
      }
    });
    return outcome;`,
    )
    assert.equal(caseName(parked), 'Parked')
    assert.deepEqual(parkedPayload(parked), { reason: 'review' })

    const resumed = await execute(
      dir,
      'DevOps',
      'en',
      'sess-050',
      { unpark: { handle: parkedHandle(parked), value: 'continue' } },
      5000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(resumed), 'Succeeded')
    assert.match(render(resumed), /status = "completed"/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] a sanity thunk that fails to show its exit parks with the diagnosis', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const parked = await runProgram(
      dir,
      `    const outcome = await js.call(class extends js.Callable {
      maxDuration = 3000;
      next(ctx) { return "echo never-terminates"; }
      watch() { return true; }
      sanity() {
        return {
          nextDone: () => [{ root: "/", step: 1 }],
          watchVeto: () => [{ root: "/", step: 0, stdout: "", stderr: "", exited: true, exitCode: 2, backgrounds: [], elapsedMs: 0, remainingMs: 60000 }],
        };
      }
    });
    return outcome;`,
    )
    assert.equal(caseName(parked), 'Parked')
    const payload = parkedPayload(parked)
    assert.ok(payload.sanity)
    assert.match(payload.sanity.failure, /next has no null path/)
    // Nothing ran: the proof gates the whole run.
    assert.equal(existsSync(join(dir, 'never-terminates')), false)

    const resumed = await execute(
      dir,
      'DevOps',
      'en',
      'sess-050',
      { unpark: { handle: parkedHandle(parked), value: 'sanity failed: next has no null path' } },
      5000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(resumed), 'Succeeded')
    const doc = render(resumed)
    assert.match(doc, /sanity failed: next has no null path/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] a sanity that returns non-thunks is refused with CALLABLE_SPEC_INCOMPLETE', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const doc = await runText(
      dir,
      `    return await js.call(class extends js.Callable {
      maxDuration = 1000;
      next(ctx) { return ctx.step === 0 ? "echo hello" : null; }
      watch() { return true; }
      sanity() { return { nope: true }; }
    });`,
    )
    assert.match(doc, /# failed/)
    assert.match(doc, /CALLABLE_SPEC_INCOMPLETE/)
    assert.match(doc, /nextDone/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] maxDuration expiry kills the command and reports timeout', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const started = Date.now()
    const doc = await runText(
      dir,
      `    return await js.call(class extends js.Callable {
      maxDuration = 400;
      interval = 100;
      next(ctx) { return ctx.step === 0 ? "sleep 5 && touch too-late.marker" : null; }
      watch(ctx) {
        if (ctx.exited && ctx.exitCode !== 0) return { abort: "command failed" };
        return true;
      }
      sanity() {
        return {
          nextDone: () => [{ root: "/", step: 1 }],
          watchVeto: () => [{ root: "/", step: 0, stdout: "", stderr: "", exited: true, exitCode: 2, backgrounds: [], elapsedMs: 0, remainingMs: 60000 }],
        };
      }
    });`,
    )
    assert.match(doc, /status = "timeout"/)
    assert.ok(Date.now() - started < 4000, 'the run must end when maxDuration expires')
    assert.equal(existsSync(join(dir, 'too-late.marker')), false)
  } finally {
    cleanup()
  }
})
