import test from 'node:test'

for (const callback of ['sanity', 'next', 'watch']) {
  test(`WHAT[process-execution-004] a synchronous infinite ${callback} callback cannot block the run deadline or strand its physical group`, { timeout: 6000 }, async () => {
    const assert = (await import('node:assert/strict')).default
    const { spawnSync } = await import('node:child_process')
    const { mkdtempSync, existsSync, readFileSync, rmSync } = await import('node:fs')
    const { tmpdir } = await import('node:os')
    const { join } = await import('node:path')
    const directory = mkdtempSync(join(tmpdir(), 'wxs-callback-deadline-'))
    try {
      const result = spawnSync(process.execPath, [join(import.meta.dirname, 'support/callback-deadline.fixture.mjs'), callback, directory], {
        timeout: 3000, encoding: 'utf8',
      })
      assert.equal(result.error, undefined, `${callback} blocked outside the vm deadline: ${result.stderr}`)
      assert.equal(result.status, 0, result.stderr)
      const observed = JSON.parse(result.stdout)
      if (callback === 'sanity') {
        assert.equal(observed.kind, 'Failed')
        assert.match(observed.wire, /CALL_SANITY_TIMEOUT/)
      } else {
        assert.equal(observed.kind, 'Succeeded', observed.wire)
        assert.equal(observed.data.entered, true)
        assert.equal(observed.data.result.status, 'timeout')
        assert.equal(observed.data.result.stopRequested, 'deadline')
        if (callback === 'watch') {
          assert.equal(observed.data.result.exitCode ?? null, null)
          if (observed.data.result.signal !== undefined) {
            assert.ok(['SIGTERM', 'SIGKILL'].includes(observed.data.result.signal))
          } else {
            assert.equal(observed.data.result.endUnconfirmed, true)
          }
          assert.equal(observed.data.result.scopeClosed, true)
          assert.equal(observed.data.result.outputComplete, false)
        }
      }
    } finally {
      const witness = join(directory, 'target.json')
      if (existsSync(witness)) {
        const { group } = JSON.parse(readFileSync(witness, 'utf8'))
        assert.ok(Number.isInteger(group) && group > 0)
        try { process.kill(-group, 'SIGKILL') } catch (error) {
          if (error.code !== 'ESRCH') throw error
        }
      }
      rmSync(directory, { recursive: true, force: true })
    }
  })
}

test('WHAT[process-execution-004] the real registered loop applies the administrator ceiling to a longer Runnable estimate', async () => {
  const assert = (await import('node:assert/strict')).default
  const { withExecutablePlugin, acceptAuthorityRoot } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
  const { oneCommand } = await import('./support/runnable.mjs')
  const { program, nodeCommand } = await import('./support/executor.mjs')
  const { existsSync } = await import('node:fs')
  const { join } = await import('node:path')
  const { parse } = await import('smol-toml')
  const previous = process.env.WANXIANGSHU_PROCESS_HARD_LIMIT_SECS
  process.env.WANXIANGSHU_PROCESS_HARD_LIMIT_SECS = '0.5'
  try {
    await withExecutablePlugin(async (hooks, directory, created, runtime) => {
      const sessionID = 'registered-hard-limit'
      await acceptAuthorityRoot(runtime, sessionID, 'devops', 'hard-limit-user')
      await hooks['chat.message']({ sessionID, messageID: 'hard-limit-user', agent: 'devops' }, {
        message: { id: 'hard-limit-user', role: 'user', agent: 'devops' },
        parts: [{ type: 'text', text: 'Check the administrator process ceiling.' }],
      })
      const ready = join(directory, 'target-started')
      const command = nodeCommand(`require('node:fs').writeFileSync(${JSON.stringify(ready)}, 'ready'); setInterval(() => {}, 1000)`)
      const wire = await hooks.tool['todowrite-for-devops'].execute({
        program: program(`return await js.call(${oneCommand(command, { maxDuration: 60000 })});`),
      }, { sessionID, agent: 'devops', messageID: 'hard-limit-assistant', callID: 'hard-limit-run' })
      const result = parse(wire).data
      assert.equal(existsSync(ready), true)
      assert.equal(result.status, 'timeout')
      assert.equal(result.stopRequested, 'deadline')
      assert.equal(result.outputComplete, false)
      assert.ok(result.durationMs >= 500 && result.durationMs < 4000)
    })
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_PROCESS_HARD_LIMIT_SECS
    else process.env.WANXIANGSHU_PROCESS_HARD_LIMIT_SECS = previous
  }
})

for (const exhaustAfterRun of [false, true]) {
  test(`WHAT[process-execution-004] concurrent explicit runs borrow the script deadline and ${exhaustAfterRun ? 'return its original remaining budget' : 'keep later watch and result reads alive'}`, async () => {
    const assert = (await import('node:assert/strict')).default
    const { sandbox, program, nodeCommand, caseName, parseData, render } = await import('./support/executor.mjs')
    const { execute } = await import('../../../dist/Repository/Programming/Js/WorkflowSurface.js')
    const { writeFileSync, readFileSync } = await import('node:fs')
    const { join } = await import('node:path')
    const { dir, cleanup } = sandbox('wxs-run-deadline-lease-')
    try {
      writeFileSync(join(dir, 'probe'), 'data')
      const command = nodeCommand('setTimeout(() => {}, 350)')
      const outcome = await execute(dir, 'DevOps', 'en', 'deadline-lease', { program: program(`
        const host = this;
        let observedLate = false;
        function runnable(command) {
          return class extends js.Callable {
            maxDuration = 1500;
            interval = 25;
            next(ctx) { return ctx.step === 0 ? command : null; }
            async watch(ctx) {
              if (ctx.remainingMs === 0) return false;
              const observed = await host.stat('probe');
              if (ctx.elapsedMs > 200 && observed.exists) observedLate = true;
              return true;
            }
            sanity() { return { nextDone: () => [{ step: 1 }], watchVeto: () => [{ remainingMs: 0 }] }; }
          };
        }
        const results = await Promise.all([js.call(runnable('sleep 0.1')), js.call(runnable(${JSON.stringify(command)}))]);
        await host.write('runs-finished', JSON.stringify({ observedLate, statuses: results.map(result => result.status) }));
        ${exhaustAfterRun ? 'const until = Date.now() + 250; while (Date.now() < until) {}' : ''}
        const after = await host.stat('probe');
        return { observedLate, afterExists: after.exists, statuses: results.map(result => result.status) };
      `) }, 1000, Date.now() + 150, 4096, null)
      assert.deepEqual(JSON.parse(readFileSync(join(dir, 'runs-finished'), 'utf8')), { observedLate: true, statuses: ['completed', 'completed'] })
      if (exhaustAfterRun) {
        assert.equal(caseName(outcome), 'Failed')
        assert.match(render(outcome), /PROGRAM_TIMEOUT/)
      } else {
        assert.equal(caseName(outcome), 'Succeeded', render(outcome))
        assert.deepEqual(parseData(outcome), { observedLate: true, afterExists: true, statuses: ['completed', 'completed'] })
      }
    } finally {
      cleanup()
    }
  })
}

test('WHAT[process-execution-004] normal high exit codes stay normal target exits', async () => {
  const assert = (await import('node:assert/strict')).default
  const runtime = await import('../../../dist/Process/Surface.js')
  for (const code of [0, 1, 7, 137, 143, 255]) {
    const result = await runtime.run(
      runtime.command(process.execPath, ['-e', `process.exit(${code})`], null, null),
      runtime.estimate(10, 1024, 'medium'), runtime.context(null, 5000), runtime.createCancellationToken(false))
    assert.equal(result.kind, 'Observed')
    assert.deepEqual(result.ending, { kind: 'Exited', exitCode: code })
    assert.deepEqual(result.output, { kind: 'InMemory', stdout: '', stderr: '' })
    assert.equal(result.stopRequested, null)
    assert.equal(result.fault, null)
  }
})

test('WHAT[process-execution-004] a parked run still stops at its deadline and releases the large-output permit only after physical closure', async () => {
  const assert = (await import('node:assert/strict')).default
  const { sandbox, runProgram, caseName, parseData, nodeCommand } = await import('./support/executor.mjs')
  const { execute, parkedHandle } = await import('../../../dist/Repository/Programming/Js/WorkflowSurface.js')
  const gate = await import('../../../dist/Process/LargeGateSurface.js')
  const { readFileSync } = await import('node:fs')
  const { join } = await import('node:path')
  const { setTimeout: delay } = await import('node:timers/promises')
  const { dir, cleanup } = sandbox('wxs-park-deadline-')
  let handle
  try {
    assert.equal(gate.getCount(), 1)
    const witness = join(dir, 'target')
    const command = `exec ${nodeCommand(`require('node:fs').writeFileSync(${JSON.stringify(witness)}, String(process.pid)); process.stdout.write('READY'); setInterval(() => {}, 1000)`)}`
    const parked = await runProgram(dir, `return await js.call(class extends js.Callable {
      maxDuration = 500;
      interval = 25;
      worldLock = true;
      next(ctx) { return ctx.step === 0 ? ${JSON.stringify(command)} : null; }
      watch(ctx) { if (ctx.stdout) return { park: { ready: true } }; return ctx.remainingMs > 0; }
      sanity() { return { nextDone: () => [{ step: 1 }], watchVeto: () => [{ remainingMs: 0 }] }; }
    });`, 'park-deadline')
    assert.equal(caseName(parked), 'Parked')
    handle = parkedHandle(parked)
    const pid = Number(readFileSync(witness, 'utf8'))
    assert.equal(gate.getCount(), 0)
    const alive = () => {
      try { process.kill(pid, 0); return true } catch (error) {
        if (error.code === 'ESRCH') return false
        throw error
      }
    }
    const deadline = performance.now() + 2500
    while ((alive() || gate.getCount() === 0) && performance.now() < deadline) await delay(10)
    assert.equal(alive(), false, 'park must not suspend the physical deadline')
    assert.equal(gate.getCount(), 1)
    const resumed = await execute(dir, 'DevOps', 'en', 'park-deadline', { unpark: { handle, value: 'continue' } }, 2000, Date.now() + 2000, 4096, null)
    handle = null
    assert.equal(caseName(resumed), 'Succeeded')
    const result = parseData(resumed)
    assert.equal(result.status, 'timeout')
    assert.equal(result.stopRequested, 'deadline')
    assert.equal(result.scopeClosed, true)
    assert.equal(result.outputComplete, false)
  } finally {
    if (handle) await execute(dir, 'DevOps', 'en', 'park-deadline', { abandon: { handle } }, 2000, Date.now() + 2000, 4096, null)
    cleanup()
  }
})

test('WHAT[process-execution-004] a physical spawn rejection proves no target execution', async () => {
  const assert = (await import('node:assert/strict')).default
  const runtime = await import('../../../dist/Process/Surface.js')
  const result = await runtime.run(
    runtime.command('/nonexistent-wanxiang-test-program', [], null, null),
    runtime.estimate(10, 1024, 'medium'), runtime.context(null, 5000), runtime.createCancellationToken(false))
  assert.equal(result.kind, 'NotStarted')
  assert.equal(result.reason.kind, 'StartRejected')
  assert.match(result.reason.reason, /ENOENT/)
})

test('WHAT[process-execution-004] target signal status does not use the supervisor KILL status', async () => {
  const assert = (await import('node:assert/strict')).default
  const runtime = await import('../../../dist/Process/Surface.js')
  for (const signal of ['SIGTERM', 'SIGINT', 'SIGKILL']) {
    const result = await runtime.run(
      runtime.command(process.execPath, ['-e', `process.kill(process.pid, '${signal}')`], undefined, undefined),
      runtime.estimate(10, 1024, 'medium'),
      runtime.context(undefined, 5000),
      runtime.createCancellationToken(false),
    )
    assert.equal(result.kind, 'Observed')
    assert.deepEqual(result.ending, { kind: 'Signaled', signal })
    assert.equal(result.fault, null)
  }
})

test('WHAT[process-execution-004] deadline terminates the group after the target exits while a descendant holds stdout', { timeout: 15000 }, async t => {
  const { verifyDrainingStop } = await import('./support/draining.mjs')
  await verifyDrainingStop(t, 'deadline')
})

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const deadline = await import("../../../dist/Process/DeadlineSurface.js");


test('WHAT[process-execution-004] Process_deadline_uses_explicit_offset_semantics', () => {
  const value = deadline.create('2026-01-01T00:00:00Z', 5000)

  assert.equal(deadline.remainingMs('2026-01-01T00:00:02Z', value), 3000)
  assert.equal(deadline.isExpired('2026-01-01T00:00:02Z', value), false)
  assert.equal(deadline.remainingMs('2026-01-01T00:00:05Z', value), 0)
  assert.equal(deadline.isExpired('2026-01-01T00:00:05Z', value), true)
  assert.equal(deadline.isExpired('2026-01-01T00:00:06Z', value), true)

  // Same instant expressed with a non-zero offset must produce the same answer.
  assert.equal(deadline.remainingMs('2026-01-01T08:00:02+08:00', value), 3000)
  assert.equal(deadline.isExpired('2026-01-01T08:00:02+08:00', value), false)
})
test('WHAT[process-execution-004] Process_deadline_is_independent_of_ambient_timezone', () => {
  const original = process.env.TZ
  const value = deadline.create('2026-01-01T00:00:00Z', 5000)

  try {
    for (const zone of ['UTC', 'Asia/Shanghai', 'America/Los_Angeles']) {
      process.env.TZ = zone
      assert.equal(deadline.isExpired('2026-01-01T00:00:02Z', value), false, `expired under TZ=${zone}`)
      assert.equal(deadline.remainingMs('2026-01-01T00:00:02Z', value), 3000, `remaining under TZ=${zone}`)
    }
  } finally {
    if (original === undefined) delete process.env.TZ
    else process.env.TZ = original
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { existsSync, readFileSync } = await import("node:fs");
const { dirname, join } = await import("node:path");
const { default: test } = await import("node:test");
const { fileURLToPath } = await import("node:url");

const {
  command,
  commandView,
  estimate,
  estimateView,
} = await import('../../../dist/Process/Surface.js')

test('WHAT[process-execution-004] EXEC_oneshot_completion_wait_is_bounded_by_management_deadline', () => {
  const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
  const oneshotPath = join(root, 'src/Wanxiangshu/Execution/Delegation/Handle/OpenCode/OneShotTool.fs')
  assert.equal(existsSync(oneshotPath), false, 'OneShotTool.fs must be physically removed')
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

test('WHAT[process-execution-004] EXEC_011_slow_process_is_killed_and_reports_timeout', async () => {
  const hangingLauncher = (_command, token) =>
    new Promise((resolve) => {
      token.register(() => resolve([{ kind: 'Unconfirmed' }, new Uint8Array(0), new Uint8Array(0)]))
    })

  const tightContext = context(undefined, 100)
  const result = await runWithLauncher(
    hangingLauncher,
    cmd,
    makeEstimate(1, 1024),
    tightContext,
    live(),
  )

  assert.equal(result.kind, 'Observed')
  assert.deepEqual(result.ending, { kind: 'Unconfirmed' })
  assert.deepEqual(result.stopRequested, { kind: 'DeadlineStopRequested', durationMs: 100 })
  assert.equal(result.output, null)
})
test('WHAT[process-execution-004] EXEC_011_untyped_host_rejection_does_not_prove_zero_execution', async () => {
  const failingHost = async () => ({ ok: false, error: 'ENOENT: no such binary' })
  const result = await runWithHostLauncher(failingHost, cmd, makeEstimate(), CTX, live())

  assert.equal(result.kind, 'Observed')
  assert.deepEqual(result.ending, { kind: 'Unconfirmed' })
  assert.match(result.fault, /invalid child/)
})
test('WHAT[process-execution-004] EXEC_011_throwing_host_maps_to_execution_failed', async () => {
  const explodingHost = async () => {
    throw new Error('host exploded')
  }
  const result = await runWithHostLauncher(explodingHost, cmd, makeEstimate(), CTX, live())

  assert.equal(result.kind, 'Observed')
  assert.deepEqual(result.ending, { kind: 'Unconfirmed' })
  assert.match(result.fault, /host exploded/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const { create: createDeadline } = await import('../../../dist/Process/DeadlineSurface.js')
const {
  killAckGraceMs,
  childCreate,
  childExit,
  childOnExit,
  childView,
  waitForExit,
  createCancellationToken,
  cancel,
} = await import('../../../dist/Process/Surface.js')
const nowIso = () => new Date().toISOString()
const within = (promise, ms, label) =>
  Promise.race([
    promise,
    new Promise((_, reject) => {
      const timer = setTimeout(() => reject(new Error(`${label}: did not settle within ${ms}ms`)), ms)
      timer.unref?.()
    }),
  ])
const expired = () => createDeadline('2000-01-01T00:00:00Z', 1)
const killCount = (child) => childView(child).killCount

test('WHAT[process-execution-004] EXEC_011_B_deadline_kills_once_then_real_exit_is_timed_out', async () => {
  let child
  child = childCreate(() => {
    setTimeout(() => childExit(child, { kind: 'Signaled', signal: 'SIGKILL' }), 15)
  })
  const wait = waitForExit(child, expired(), createCancellationToken(false))

  const outcome = await within(wait, 2_000, 'deadline + kill-ack')
  assert.deepEqual(outcome, { stopRequested: 'Deadline' })
  assert.equal(killCount(child), 1, 'deadline path Kill exactly once')
})
test(
  'WHAT[process-execution-004] EXEC_011_C_kill_never_acked_preserves_stop_without_fabricating_exit',
  { timeout: 15_000 },
  async () => {
    assert.equal(killAckGraceMs, 1_000)
    const child = childCreate(undefined)
    const wait = waitForExit(child, expired(), createCancellationToken(false))

    const outcome = await within(wait, 5_000, 'kill-ack timeout')
    assert.deepEqual(outcome, { stopRequested: 'Deadline' })
    assert.equal(killCount(child), 1, 'deadline still Kill once before grace')
  },
)
}

{
  const assert = (await import('node:assert/strict')).default
  const runtime = await import('../../../dist/Process/Surface.js')
  test('WHAT[process-execution-004] effective deadline takes the smaller estimate and hard limit', () => {
    for (const [estimate, expected] of [[10, 10], [2000, 2000], [5000, 3600]]) {
      assert.equal(runtime.effectiveDeadlineSeconds(estimate, 3600), expected)
    }
    for (const invalid of [NaN, Infinity, -Infinity, 0, -10]) {
      assert.equal(runtime.effectiveDeadlineSeconds(invalid, 60), 60)
    }
    assert.equal(runtime.defaultHardLimitSeconds, 3600, 'current default policy, not a universal time contract')
  })
  test('WHAT[process-execution-004] deadline result describes waiting ending without claiming a return', () => {
    assert.match(runtime.renderDeadlineExpired(), /No return reached you before your waiting ended/)
  })
}
