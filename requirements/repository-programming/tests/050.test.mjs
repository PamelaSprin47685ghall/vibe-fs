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

test('WHAT[repository-programming-050] a Runnable missing check is refused with RUNNABLE_SPEC_INCOMPLETE', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const doc = await runText(
      dir,
      `    return await js.run(class { maxDuration = 1000; next() { return null; } });`,
    )
    assert.match(doc, /# failed/)
    assert.match(doc, /RUNNABLE_SPEC_INCOMPLETE/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] the host loop runs one foreground command and reports the structured outcome', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const doc = await runText(
      dir,
      `    return await js.run(class extends js.Runnable {
      maxDuration = 5000;
      interval = 100;
      next(ctx) { return ctx.step === 0 ? "echo hello" : null; }
      check() { return true; }
    });`,
    )
    assert.match(doc, /# ok/)
    assert.match(doc, /status = "completed"/)
    assert.match(doc, /hello/)
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
    const outcome = await js.run(class extends js.Runnable {
      maxDuration = 5000;
      interval = 100;
      next(ctx) { return ctx.step === 0 ? "exit 4" : null; }
      check() { return true; }
      onExit(result) { dispatched.push(result.exitCode); }
    });
    return { outcome, dispatched };`,
    )
    assert.match(doc, /# ok/)
    assert.match(doc, /dispatched = \[4\]/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] a check abort kills the foreground command and reports aborted', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const doc = await runText(
      dir,
      `    const outcome = await js.run(class extends js.Runnable {
      maxDuration = 8000;
      interval = 200;
      next(ctx) { return ctx.step === 0 ? "sleep 5 && touch late.marker" : null; }
      check() { return { abort: "stalled" }; }
    });
    return outcome;`,
    )
    assert.match(doc, /status = "aborted"/)
    assert.equal(existsSync(join(dir, 'late.marker')), false, 'the killed command must not finish')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] a check park suspends the run and resumes after unpark', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const parked = await runProgram(
      dir,
      `    const outcome = await js.run(class extends js.Runnable {
      maxDuration = 5000;
      interval = 50;
      next(ctx) { return ctx.step === 0 ? "sleep 0.3" : null; }
      check() { if (!this.parked) { this.parked = true; return { park: { reason: "review" } }; } return true; }
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

test('WHAT[repository-programming-050] maxDuration expiry kills the command and reports timeout', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const started = Date.now()
    const doc = await runText(
      dir,
      `    return await js.run(class extends js.Runnable {
      maxDuration = 400;
      interval = 100;
      next(ctx) { return ctx.step === 0 ? "sleep 5 && touch too-late.marker" : null; }
      check() { return true; }
    });`,
    )
    assert.match(doc, /status = "timeout"/)
    assert.ok(Date.now() - started < 4000, 'the run must end when maxDuration expires')
    assert.equal(existsSync(join(dir, 'too-late.marker')), false)
  } finally {
    cleanup()
  }
})
