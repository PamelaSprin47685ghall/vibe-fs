import test from 'node:test'

{
const assert = (await import('node:assert/strict')).default
const { execFile, spawnSync } = await import('node:child_process')
const { promisify } = await import('node:util')
const { fileURLToPath } = await import('node:url')
const { withFinitePhysicalWait } = await import('../../verification-system/tests/support/finite-physical-wait.mjs')
for (const mode of ['scope', 'recovery']) {
  for (const kind of ['valid', 'missing', 'file', 'relative', 'empty', 'null']) {
    test(`WHAT[process-execution-005] PTY ${mode} validates ${kind} cwd before physical preparation`, { timeout: 15000 }, async t => {
      if (spawnSync('bun', ['--version'], { encoding: 'utf8' }).status !== 0) return t.skip('Bun is required for the real PTY proof')
      const helper = fileURLToPath(new URL('./support/pty-cwd.mjs', import.meta.url))
      const { stdout } = await withFinitePhysicalWait(t, { operation: `PTY cwd ${mode} ${kind}`, limitMs: 14000 },
        () => promisify(execFile)('bun', [helper, mode, kind], { timeout: 12000 }))
      const evidence = JSON.parse(stdout.trim())
      assert.equal(evidence.mode, mode)
      assert.equal(evidence.kind, kind)
    })
  }
}
}

{
// Host-native `run` tool is retired; `js.call` supersedes it (repository-programming [050]-[052]).
const { default: assert } = await import('node:assert/strict')
const { default: test } = await import('node:test')
const { sandbox, runProgram, caseName, render } = await import('./support/executor.mjs')

test('WHAT[process-execution-005] js.call rejects non-positive or non-finite maxDuration before spawning', async () => {
  const { dir, cleanup } = sandbox('wxs-proc-005-')
  try {
    for (const rawExpr of ['0', '-5', 'Number.NaN', 'Number.POSITIVE_INFINITY']) {
      const outcome = await runProgram(
        dir,
        `    return await this.call(class extends this.Callable {
      maxDuration = ${rawExpr};
      next(ctx) { return ctx.step === 0 ? "true" : null; }
      watch(ctx) { return ctx.remainingMs > 0; }
      sanity() { return { nextDone: () => [{ step: 1 }], watchVeto: () => [{ remainingMs: 0 }] }; }
    });`,
      )
      assert.equal(caseName(outcome), 'Failed', `maxDuration=${rawExpr} must fail`)
      assert.match(render(outcome), /CALLABLE_SPEC_INCOMPLETE/, `maxDuration=${rawExpr}`)
    }
  } finally {
    cleanup()
  }
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
  commandView,
  estimate,
  estimateView,
} = await import('../../../dist/Process/Surface.js')

test('WHAT[process-execution-005] EXEC_010_process_request_carries_all_fields', () => {
  const environment = Object.fromEntries([['__proto__', 'literal'], ['EMPTY', '']])
  const cmd = command('sh', ['-lc', 'echo hi'], '/tmp/wx', 'input', environment)
  const cmdView = commandView(cmd)
  const estView = estimateView(estimate(42, 65536, 'large'))

  assert.equal(cmdView.fileName, 'sh')
  assert.deepEqual(cmdView.arguments, ['-lc', 'echo hi'])
  assert.equal(cmdView.workingDirectory, '/tmp/wx')
  assert.equal(cmdView.stdin, 'input')
  assert.deepEqual(cmdView.environment, environment)
  assert.equal(estView.runtimeSeconds, 42)
  assert.equal(estView.outputBytes, 65536)
  assert.equal(estView.memory, 'large')
})
}

{
  const assert = (await import('node:assert/strict')).default
  const runtime = await import('../../../dist/Process/Surface.js')
  test('WHAT[process-execution-005] estimate validation rejects nonfinite or nonpositive time and negative output', () => {
    assert.equal(runtime.validateEstimate(10, 0).ok, true)
    for (const invalid of [NaN, Infinity, -Infinity, 0, -5]) {
      const result = runtime.validateEstimate(invalid, 1024)
      assert.equal(result.ok, false)
      assert.match(result.error, /finite positive number/)
    }
    const result = runtime.validateEstimate(10, -1)
    assert.equal(result.ok, false)
    assert.match(result.error, /non-negative/)
  })
}
