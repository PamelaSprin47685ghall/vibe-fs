import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync } from 'node:fs'
import { join } from 'node:path'
import { sandbox, runProgram, caseName, render, nodeCommand } from './support/executor.mjs'

const { contextDecode, contextView } = await import('../../../dist/OpenCode/Codec/ToolHostSurface.js')

// Host-native `run` tool is retired; `js.run` supersedes it (repository-programming [050]-[052]).

test('WHAT[process-execution-011] RUN_host_context_codec_exposes_plain_snapshot', () => {
  const decoded = contextDecode({ sessionID: 'ses-exec', agent: 'devops' })
  assert.deepEqual(contextView(decoded), {
    sessionId: 'ses-exec',
    agent: 'devops',
    toolCallId: null,
    providerRunId: null,
    promptText: null,
  })
})

test('WHAT[process-execution-011] js.run rejects incomplete Runnable specs before spawning', async () => {
  const { dir, cleanup } = sandbox('wxs-proc-011a-')
  try {
    const marker = join(dir, 'spawned')
    const cmd = nodeCommand(`require('node:fs').writeFileSync(${JSON.stringify(marker)}, 'ran')`)
    const incompleteSpecs = [
      `class { next() { return ${JSON.stringify(cmd)}; } check() { return true; } }`,
      `class { maxDuration = 0; next() { return ${JSON.stringify(cmd)}; } check() { return true; } }`,
      `class { maxDuration = Infinity; next() { return ${JSON.stringify(cmd)}; } check() { return true; } }`,
      `class { maxDuration = 1000; check() { return true; } }`,
      `class { maxDuration = 1000; next() { return ${JSON.stringify(cmd)}; } }`,
    ]
    for (const spec of incompleteSpecs) {
      const outcome = await runProgram(dir, `    return await js.run(${spec});`)
      assert.equal(caseName(outcome), 'Failed')
      assert.match(render(outcome), /RUNNABLE_SPEC_INCOMPLETE/)
      assert.equal(existsSync(marker), false, 'incomplete Runnable spec must not spawn the command')
    }
  } finally {
    cleanup()
  }
})

test('WHAT[process-execution-011] js.run rejects unsupported command descriptors with RUN_COMMAND_UNSUPPORTED', async () => {
  const { dir, cleanup } = sandbox('wxs-proc-011b-')
  try {
    const outcome = await runProgram(
      dir,
      `    return await js.run(class extends js.Runnable {
      maxDuration = 2000;
      next() { return { unsupported: true }; }
      check() { return true; }
    });`,
    )
    assert.equal(caseName(outcome), 'Failed')
    assert.match(render(outcome), /RUN_COMMAND_UNSUPPORTED/)
  } finally {
    cleanup()
  }
})

test('WHAT[process-execution-011] js.run maxDuration overrun terminates the foreground command and reports timeout', async () => {
  const { dir, cleanup } = sandbox('wxs-proc-011c-')
  try {
    const marker = join(dir, 'late-marker')
    const outcome = await runProgram(
      dir,
      `    return await js.run(class extends js.Runnable {
      maxDuration = 150;
      interval = 30;
      next(ctx) { return ctx.step === 0 ? "sleep 5 && touch late-marker" : null; }
      check() { return true; }
    });`,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    assert.match(render(outcome), /status = "timeout"/)
    assert.equal(existsSync(marker), false)
  } finally {
    cleanup()
  }
})
