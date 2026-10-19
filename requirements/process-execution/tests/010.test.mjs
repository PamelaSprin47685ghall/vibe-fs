import test from 'node:test'
import assert from 'node:assert/strict'
import { verifyPtyScope } from './support/pty-scope.mjs'

test('WHAT[process-execution-010] native signal exits remain distinct from normal high exit codes', t =>
  verifyPtyScope(t, 'native-status', evidence => {
    assert.equal(evidence.nativeSignals, 7)
    assert.equal(evidence.distinctExitCodes, 5)
  }))

test('WHAT[process-execution-010] the supervised PTY preserves a real terminal, input, resize and target exit code', t =>
  verifyPtyScope(t, 'basic', evidence => {
    assert.equal(evidence.tty, true)
    assert.equal(evidence.input, true)
    assert.equal(evidence.resize, true)
    assert.deepEqual(evidence.exit, { code: 37, signal: null })
  }))

{
// Host-native `run` tool is retired; `js.run` supersedes it (repository-programming [050]-[052]).
const { default: assert } = await import('node:assert/strict')
const { default: test } = await import('node:test')
const { sandbox, runProgram, caseName, parseData } = await import('./support/executor.mjs')

test('WHAT[process-execution-010] js.run completed command reports exit code and streams via onExit and outcome', async () => {
  const { dir, cleanup } = sandbox('wxs-proc-010a-')
  try {
    const outcome = await runProgram(
      dir,
      `    let exitedWith = null;
    const result = await js.run(class extends js.Runnable {
      maxDuration = 5000;
      interval = 50;
      next(ctx) { return ctx.step === 0 ? "printf 'hello-stdout'; printf 'hello-stderr' >&2" : null; }
      watch(ctx) { return ctx.remainingMs > 0; }
      sanity() { return { nextDone: () => this.next({ step: 1 }), watchVeto: () => this.watch({ remainingMs: 0 }) }; }
      onExit(r) { exitedWith = { exitCode: r.exitCode, stdout: r.stdout, stderr: r.stderr }; }
    });
    return { exitCode: result.exitCode, onExitStdout: exitedWith.stdout, onExitStderr: exitedWith.stderr };`,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    const parsed = parseData(outcome)
    assert.equal(parsed.exitCode, 0)
    assert.equal(parsed.onExitStdout, 'hello-stdout')
    assert.equal(parsed.onExitStderr, 'hello-stderr')
  } finally {
    cleanup()
  }
})

test('WHAT[process-execution-010] js.run nonzero exit is reported as data rather than thrown', async () => {
  const { dir, cleanup } = sandbox('wxs-proc-010b-')
  try {
    const outcome = await runProgram(
      dir,
      `    return await js.run(class extends js.Runnable {
      maxDuration = 5000;
      interval = 50;
      next(ctx) { return ctx.step === 0 ? "exit 3" : null; }
      watch(ctx) { return ctx.remainingMs > 0; }
      sanity() { return { nextDone: () => this.next({ step: 1 }), watchVeto: () => this.watch({ remainingMs: 0 }) }; }
    });`,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    const parsed = parseData(outcome)
    assert.equal(parsed.status, 'completed')
    assert.equal(parsed.exitCode, 3)
  } finally {
    cleanup()
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const { renderPtyCompletion } = await import('../../../dist/Process/Surface.js')

test('WHAT[process-execution-010] EXEC_004_pty_completion_is_natural_language_plus_exit_code', () => {
  const wire = renderPtyCompletion('shell', 'pty-9', 'ended', 0)
  assert.match(wire, /# shell has ended\./)
  assert.match(wire, /exit_code = 0/)
  assert.ok(!wire.includes('pty_id'))
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const {
  command,
  context,
  createCancellationToken,
  estimate,
  runWithLauncher,
  runWithHostLauncher,
} = await import('../../../dist/Process/Surface.js')
const CTX = context(undefined, 3_600_000)
const cmd = command('sh', ['-c', 'echo hi'], undefined, undefined)
const makeEstimate = (runtimeSeconds = 10, outputBytes = 1024, memory = 'medium') =>
  estimate(runtimeSeconds, outputBytes, memory)
const live = () => createCancellationToken(false)
const cancelled = () => createCancellationToken(true)
const okLauncher = (exitCode = 0, out = 'hello', err = '') => async (_command, _token) => [
  { kind: 'Exited', exitCode },
  new TextEncoder().encode(out),
  new TextEncoder().encode(err),
]

test('WHAT[process-execution-010] EXEC_011_successful_run_collects_stdout_and_exit_code', async () => {
  const result = await runWithLauncher(okLauncher(0, 'the output', ''), cmd, makeEstimate(), CTX, live())
  assert.equal(result.kind, 'Observed')
  assert.deepEqual(result.ending, { kind: 'Exited', exitCode: 0 })
  assert.deepEqual(result.output, {
    kind: 'InMemory',
    stdout: 'the output',
    stderr: '',
  })
})
test('WHAT[process-execution-010] EXEC_011_nonzero_exit_is_still_an_ok_outcome', async () => {
  const result = await runWithLauncher(okLauncher(3, '', 'boom'), cmd, makeEstimate(), CTX, live())
  assert.equal(result.kind, 'Observed')
  assert.equal(result.output.kind, 'InMemory')
  assert.deepEqual(result.ending, { kind: 'Exited', exitCode: 3 })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { dirname, join } = await import("node:path");
const { default: test } = await import("node:test");
const { fileURLToPath } = await import("node:url");
const {
  command,
  context,
  createCancellationToken,
  estimate,
  run,
} = await import('../../../dist/Process/Surface.js')

test('WHAT[process-execution-010] CUT_02_non_pty_host_uses_one_supervised_process_owner_and_retires_direct_child_process_spawn', async () => {
  const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
  const hostSrc = readFileSync(join(root, 'src/Wanxiangshu/Process/NodeProcessHost.fs'), 'utf8')
  assert.match(hostSrc, /Import\("SupervisedProcess",\s*"wanxiangli\/supervision"\)/)
  assert.match(hostSrc, /new SupervisedProcess\(/)
  assert.match(hostSrc, /session\.takeOutput\(\)/)
  assert.doesNotMatch(hostSrc, /Import\("ProcessSession",\s*"wanxiangli\/process"\)/)
  assert.doesNotMatch(hostSrc, /(?:node:)?child_process/)
  assert.doesNotMatch(hostSrc, /process\.kill\s*\(/)

  const outcome = await run(
    command(process.execPath, ['-e', 'process.stdout.write("via-supervised-process"); process.stderr.write("err-stream"); process.exit(5)'], process.cwd(), undefined),
    estimate(10, 4096, 'medium'),
    context(process.cwd(), 60_000),
    createCancellationToken(false),
  )
  assert.equal(outcome.kind, 'Observed')
  assert.deepEqual(outcome.ending, { kind: 'Exited', exitCode: 5 })
  assert.deepEqual(outcome.output, {
    kind: 'InMemory',
    stdout: 'via-supervised-process',
    stderr: 'err-stream',
  })
})
}
