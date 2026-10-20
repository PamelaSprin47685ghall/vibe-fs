import assert from 'node:assert/strict'
import test from 'node:test'
import { sandbox, runProgram, caseName, parseData, nodeCommand } from './support/executor.mjs'
import { oneCommand } from './support/runnable.mjs'
import { runWithSpoolFault } from './support/spool-fault.mjs'

const executeOne = async (command, options = {}) => {
  const { dir, cleanup } = sandbox('wxs-physical-run-018-')
  try {
    const outcome = await runProgram(dir, `return await js.call(${oneCommand(command, options)});`)
    assert.equal(caseName(outcome), 'Succeeded')
    return parseData(outcome)
  } finally {
    cleanup()
  }
}

test('WHAT[process-execution-018] js.call keeps split UTF-8 bytes, BOM and trailing CRLF in both native streams', async () => {
  const { dir, cleanup } = sandbox('wxs-physical-bytes-018-')
  const stdout = '\uFEFF汉🔥\r\n'
  const stderr = '诊断😀\r\n'
  const command = nodeCommand(`(async () => {
    const fs = require('node:fs');
    for (const [fd, text] of [[1, ${JSON.stringify(stdout)}], [2, ${JSON.stringify(stderr)}]]) {
      for (const byte of Buffer.from(text)) {
        fs.writeSync(fd, Buffer.from([byte]));
        await new Promise(resolve => setTimeout(resolve, 10));
      }
    }
    process.exit(7);
  })();`)
  try {
    const runnable = oneCommand(command, { onExit: 'onExit(result) { observed = result; }' })
    const outcome = await runProgram(dir, `let observed;
      await js.call(${runnable});
      return { stdout: observed.stdout, stderr: observed.stderr, exitCode: observed.exitCode, bytes: observed.bytes };`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(parseData(outcome), { stdout, stderr, exitCode: 7, bytes: Buffer.byteLength(stdout) + Buffer.byteLength(stderr) })
  } finally {
    cleanup()
  }
})

test('WHAT[process-execution-018] native SIGTERM and an ordinary exit 143 remain different facts', async () => {
  const exited = await executeOne(`exec ${nodeCommand('process.exit(143)')}`)
  assert.equal(exited.exitCode, 143)
  assert.equal(exited.signal ?? null, null)
  const signaled = await executeOne(`exec ${nodeCommand("process.kill(process.pid, 'SIGTERM')")}`)
  assert.equal(signaled.exitCode ?? null, null)
  assert.equal(signaled.signal, 'SIGTERM')
})

test('WHAT[process-execution-018] a foreground command receives EOF after its input and completes normally', async () => {
  const result = await executeOne('exec cat', { maxDuration: 500 })
  assert.equal(result.status, 'completed')
  assert.equal(result.exitCode, 0)
  assert.equal(result.stdout, '')
})

test('WHAT[process-execution-018] a physical spool failure remains a fault with incomplete output and never becomes a successful empty transcript', async () => {
  const outcome = await runWithSpoolFault()
  assert.equal(caseName(outcome), 'Succeeded')
  const result = parseData(outcome)
  assert.equal(result.status, 'failed')
  assert.equal(result.notStarted, false)
  assert.equal(result.outputComplete, false)
  assert.match(result.fault, /ENOENT/)
  if (result.exitCode !== undefined) assert.equal(result.exitCode, 7)
  if (result.signal !== undefined) assert.equal(result.signal, 'SIGKILL')
})

test('WHAT[process-execution-018] killing the supervisor group preserves an unknown background end and the already known foreground exit', async () => {
  const { dir, cleanup } = sandbox('wxs-background-unknown-018-')
  try {
    const command = `exec ${nodeCommand('setInterval(() => {}, 1000)')}`
    const outcome = await runProgram(dir, `return await js.call(class extends js.Callable {
      maxDuration = 3000;
      interval = 25;
      next(ctx) {
        if (ctx.step === 0) return { spawn: ${JSON.stringify(command)}, name: 'unknown-background' };
        if (ctx.step === 1) return 'sleep 0.15';
        if (ctx.step === 2) return { signal: 'SIGKILL', name: 'unknown-background' };
        return null;
      }
      watch(ctx) { return ctx.remainingMs > 0; }
      sanity() { return { nextDone: () => [{ step: 3 }], watchVeto: () => [{ remainingMs: 0 }] }; }
    });`)
    assert.equal(caseName(outcome), 'Succeeded')
    const result = parseData(outcome)
    assert.equal(result.status, 'failed')
    assert.equal(result.exitCode, 0)
    assert.equal(result.scopeClosed, true)
    assert.equal(result.outputComplete, false)
    assert.match(result.fault, /unknown-background: .*without a target exit receipt/)
  } finally {
    cleanup()
  }
})
