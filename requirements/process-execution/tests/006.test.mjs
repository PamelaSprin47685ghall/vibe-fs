import test from 'node:test'
import assert from 'node:assert/strict'
import { verifyPtyScope } from './support/pty-scope.mjs'
import { nodeCommand } from './support/executor.mjs'

test('WHAT[process-execution-006] a waiter fault without token cancellation keeps its original error', async () => {
  const runtime = await import('../../../dist/Process/Surface.js')
  const child = runtime.childCreate(() => { throw new Error('Controlled kill failure') })
  try {
    const result = await runtime.runWithHostLauncher(async () => child,
      runtime.command('unused', [], null, null), runtime.estimate(0.01, 1024, 'medium'),
      runtime.context(null, 10000), runtime.createCancellationToken(false))
    assert.equal(result.kind, 'Observed')
    assert.deepEqual(result.ending, { kind: 'Unconfirmed' })
    assert.match(result.fault, /Controlled kill failure/)
    assert.equal(runtime.childView(child).killCount, 1)
  } finally {
    runtime.childExit(child, { kind: 'Exited', exitCode: 0 })
  }
})

test('WHAT[process-execution-006] the registered DevOps tool observes real Host abort during execution without claiming physical completion', { timeout: 20000 }, async () => {
  const { withExecutablePlugin, acceptAuthorityRoot } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
  const { existsSync, readFileSync } = await import('node:fs')
  const { join } = await import('node:path')
  const { setTimeout: delay } = await import('node:timers/promises')
  const { refreshGlobalLanguage } = await import('../../../dist/Participant/Provider/LanguageSurface.js')
  const until = async observe => {
    const end = performance.now() + 5000
    while (performance.now() < end) {
      if (observe()) return
      await delay(10)
    }
    assert.fail('The real process did not reach its expected physical state')
  }
  const alive = pid => {
    try {
      process.kill(pid, 0)
      return true
    } catch (error) {
      if (error.code === 'ESRCH') return false
      throw error
    }
  }
  await withExecutablePlugin(async (hooks, directory, created, runtime) => {
    const sessionID = 'registered-abort-devops'
    const messageID = 'registered-abort-user'
    await acceptAuthorityRoot(runtime, sessionID, 'devops', messageID)
    await hooks['chat.message']({ sessionID, messageID, agent: 'devops' }, {
      message: { id: messageID, role: 'user', agent: 'devops' },
      parts: [{ type: 'text', text: 'Check command cancellation.' }],
    })
    const previousLanguage = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
    try {
      for (const language of ['en', 'zh-CN']) {
        process.env.WANXIANGSHU_PROVIDER_LANGUAGE = language
        refreshGlobalLanguage()
        const ready = join(directory, `abort-ready-${language}`)
        const controller = new AbortController()
        const modelsBefore = created.length
        const promptsBefore = runtime.prompts.length
        const pending = hooks.tool.run.execute({
          command: nodeCommand(`const fs = require('node:fs'); fs.writeFileSync(${JSON.stringify(`${ready}.pending`)}, String(process.pid)); fs.renameSync(${JSON.stringify(`${ready}.pending`)}, ${JSON.stringify(ready)}); setInterval(() => {}, 1000)`),
          output_budget_bytes: 512,
        }, { sessionID, agent: 'devops', messageID: 'registered-abort-assistant', callID: `abort-${language}`, abort: controller.signal })
        let pid
        try {
          await until(() => existsSync(ready))
          pid = Number(readFileSync(ready, 'utf8'))
          assert.ok(Number.isInteger(pid) && pid > 0)
          assert.equal(alive(pid), true)
          controller.abort()
          const wire = await pending
          assert.match(wire, /stop_requested = "cancellation"/)
          assert.doesNotMatch(wire, /exit_code|SpawnFailed|ExecutionFailed/)
          assert.ok(Buffer.byteLength(wire) <= 512)
          assert.equal(created.length, modelsBefore)
          assert.equal(runtime.prompts.length, promptsBefore)
          await until(() => !alive(pid))
        } finally {
          controller.abort()
          if (!pid && existsSync(ready)) pid = Number(readFileSync(ready, 'utf8'))
          if (pid && alive(pid)) process.kill(pid, 'SIGKILL')
          await pending.catch(() => {})
        }
      }
    } finally {
      if (previousLanguage === undefined) delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
      else process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previousLanguage
      refreshGlobalLanguage()
    }
  })
})

test('WHAT[process-execution-006] Ctrl-Z stops the direct target while its group supervisor can still apply KILL', t =>
  verifyPtyScope(t, 'direct-stop', evidence => {
    assert.equal(evidence.targetStopped, true)
    assert.equal(evidence.anchorRunnable, true)
    assert.equal(evidence.killed, true)
  }))

test('WHAT[process-execution-006] a real PTY keeps job control and drains a separate background group', t =>
  verifyPtyScope(t, 'job-control', evidence => {
    assert.equal(evidence.ctrlZ, true)
    assert.equal(evidence.foregroundResume, true)
    assert.equal(evidence.ctrlC, true)
    assert.equal(evidence.backgroundGroupExited, true)
  }))

test('WHAT[process-execution-006] short job groups can disappear while the live PTY owner drains', t =>
  verifyPtyScope(t, 'churn', evidence => {
    assert.equal(evidence.shortGroupRace, true)
    assert.equal(evidence.allExited, true)
    assert.deepEqual(evidence.exit, { code: null, signal: 'SIGKILL' })
  }))

test('WHAT[process-execution-006] cancellation terminates the group after the target exits while a descendant holds stdout', { timeout: 15000 }, async t => {
  const { verifyDrainingStop } = await import('./support/draining.mjs')
  await verifyDrainingStop(t, 'cancel')
})

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

test('WHAT[process-execution-006] EXEC_011_kill_ack_grace_is_finite_not_MaxTimerWaitMs', () => {
  const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
  const waitSrc = readFileSync(join(root, 'src/Wanxiangshu/Process/NodeProcessWait.fs'), 'utf8')

  assert.match(waitSrc, /let KillAckGraceMs = /, 'named kill-ack constant required')
  assert.match(waitSrc, /waitForSignal child KillAckGraceMs/, 'post-kill wait uses kill-ack, not MaxTimerWaitMs')
  assert.doesNotMatch(
    waitSrc,
    /killSent then[\s\S]{0,80}waitSegment Deadline\.MaxTimerWaitMs/,
    'post-kill must not wait MaxTimerWaitMs',
  )
  assert.doesNotMatch(waitSrc, /ExitCode|128\s*\+|= -1/, 'waiting cannot invent target status')
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

test('WHAT[process-execution-006] EXEC_011_throwing_host_under_cancellation_maps_to_process_cancelled', async () => {
  const explodingHost = async () => {
    throw new Error('host exploded')
  }
  const result = await runWithHostLauncher(explodingHost, cmd, makeEstimate(), CTX, cancelled())

  assert.deepEqual(result, { kind: 'NotStarted', reason: { kind: 'CancelledBeforeStart' } })
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

test('WHAT[process-execution-006] EXEC_011_D_mid_wait_cancellation_kills_once_and_returns_without_hanging_on_exit', async () => {
  const child = childCreate(undefined)
  const deadline = createDeadline(nowIso(), 60_000)
  const token = createCancellationToken(false)
  const wait = waitForExit(child, deadline, token)

  setTimeout(() => cancel(token), 30)
  assert.deepEqual(await within(wait, 2_000, 'mid-wait cancel'), { stopRequested: 'Cancellation' })
  assert.equal(killCount(child), 1, 'mid-wait cancel must Kill exactly once')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const {
  bytes,
  newId,
  ptyIdView,
  registerParentAbort,
  unregisterParentAbort,
  abortParent,
} = await import('../../../dist/Process/Surface.js')

test('WHAT[process-execution-006] PTY_API_abort_parent_invokes_every_registered_callback', () => {
  const parent = 'parent-all'
  const calls = []
  registerParentAbort(parent, () => calls.push('a'))
  registerParentAbort(parent, () => calls.push('b'))

  abortParent(parent)
  assert.deepEqual(calls.sort(), ['a', 'b'])

  abortParent(parent)
  assert.equal(calls.length, 4)
})
test('WHAT[process-execution-006] PTY_API_abort_parent_with_unknown_id_is_a_noop', () => {
  abortParent('parent-never-registered')
})
test('WHAT[process-execution-006] PTY_API_unregister_removes_only_the_matching_token', () => {
  const parent = 'parent-partial'
  const calls = []
  const tokenA = registerParentAbort(parent, () => calls.push('a'))
  registerParentAbort(parent, () => calls.push('b'))

  unregisterParentAbort(parent, tokenA)
  abortParent(parent)
  assert.deepEqual(calls, ['b'])
})
test('WHAT[process-execution-006] PTY_API_unregister_last_callback_drops_the_parent_entry', () => {
  const parent = 'parent-dropped'
  const calls = []
  const token = registerParentAbort(parent, () => calls.push('a'))

  unregisterParentAbort(parent, token)
  abortParent(parent)
  assert.deepEqual(calls, [])
})
test('WHAT[process-execution-006] PTY_API_unregister_with_unknown_parent_or_token_is_a_noop', () => {
  unregisterParentAbort('parent-nope', 1)
  const parent = 'parent-mismatch'
  registerParentAbort(parent, () => {})
  unregisterParentAbort(parent, 99999)
  abortParent(parent)
})
test('WHAT[process-execution-006] PTY_API_tokens_are_monotonic_across_parents', () => {
  const t1 = registerParentAbort('parent-tok-1', () => {})
  const t2 = registerParentAbort('parent-tok-2', () => {})
  assert.ok(t2 > t1, `${t2} > ${t1}`)
})
test('WHAT[process-execution-006] PTY_API_throwing_abort_callback_does_not_block_the_rest', () => {
  const parent = 'parent-throw'
  const calls = []
  registerParentAbort(parent, () => {
    throw new Error('abort exploded')
  })
  registerParentAbort(parent, () => calls.push('after'))

  abortParent(parent)
  assert.deepEqual(calls, ['after'])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { spawn } = await import("node:child_process");

const {
  supervisorCreate,
  supervisorAdd,
  supervisorTryGet,
  supervisorGet,
  supervisorRemove,
  supervisorList,
  supervisorSignalName,
  supervisorEnsureSpawn,
  supervisorSpawnSync,
  supervisorSetSpawn,
  supervisorFailPending,
  supervisorTakePending,
  supervisorDropPending,
  supervisorApplyLive,
  supervisorAttach,
  supervisorCancelAll,
  supervisorPendingEntries,
  pendingEntryView,
  pendingResolve,
  sessionCreate,
  sessionView,
  sessionSetClosed,
  sessionAppendOutput,
  sessionPushPending,
  sessionPushPendingTask,
  createPtyPort,
  portFork,
  portRead,
  portReadResult,
  portAddMailboxSender,
  ptyId,
  ptyIdView,
  ptyCommandRead,
  ptyCommandWrite,
  ptyCommandSignal,
  ptyCommandResize,
  ptyCommandSpawn,
} = await import('../../../dist/Process/Surface.js')
const id = (value) => ptyId(value)
const port = () => createPtyPort({})
const resultError = (reason) => ({ ok: false, error: reason })
const resultOk = { ok: true, value: undefined }
const settle = () => new Promise((resolve) => setImmediate(resolve))
const child = () => spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)'])
const killChild = (value) => {
  if (value && value.exitCode === null && value.signalCode === null) value.kill('SIGKILL')
}
const died = (value) => value.exitCode !== null || value.signalCode !== null
const waitExit = (value) =>
  Promise.race([
    new Promise((resolve) => value.once('exit', resolve)),
    new Promise((resolve) => setTimeout(resolve, 2_000)),
  ])
const fakeTerm = (pid) => {
  const term = {
    pid,
    dataCb: null,
    exitCb: null,
    writes: [],
    resizes: [],
    onData: (cb) => { term.dataCb = cb },
    onExit: (cb) => { term.exitCb = cb },
    onFault: () => {},
    signal: (signal) => process.kill(pid, signal),
    write: (text) => term.writes.push(text),
    resize: (width, height) => term.resizes.push([width, height]),
  }
  return term
}
const portWith = (value) => {
  const p = port()
  portFork(p, 'echo hi', 'devops', id(value), undefined)
  return p
}

test('WHAT[process-execution-006] SUPERVISOR_attach_without_port_entry_kills_the_term', async () => {
  const process = child()
  try {
    const supervisor = supervisorCreate()
    supervisorAttach(supervisor, createPtyPort({}), id('pty-nk'), fakeTerm(process.pid))
    await waitExit(process)
    assert.ok(died(process), 'unregistered attach SIGKILLs the process tree')
  } finally { killChild(process) }
})
test('WHAT[process-execution-006] SUPERVISOR_cancelAll_kills_live_sessions_and_skips_closed_or_null_backends', async () => {
  const process = child()
  try {
    const supervisor = supervisorCreate()
    const live = sessionCreate('pty-l1', fakeTerm(process.pid))
    const closed = sessionCreate('pty-l2', { pid: 2_147_483_647 })
    sessionSetClosed(closed, true)
    supervisorAdd(supervisor, id('pty-l1'), live)
    supervisorAdd(supervisor, id('pty-l2'), closed)
    supervisorAdd(supervisor, id('pty-l3'), sessionCreate('pty-l3', null))
    supervisorCancelAll(supervisor)
    await waitExit(process)
    assert.ok(died(process), 'live session killed')
  } finally { killChild(process) }
})
}
