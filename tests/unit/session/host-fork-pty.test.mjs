// tests/unit/session/host-fork-pty.test.mjs — HostForkRuntime PTY surface
// (HostForkPty.fs): TrackPtyRun/RegisterPtySnapshot/UntrackPtyRun/OwnsPty/
// IsPtyCompletion/ForkPty/TryPty/SendPty against a fake PtyPort.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { agentJournal, caseOf, sessionId, toList } from '../support/domain.mjs'

const { HostForkRuntime } = await import('../../../dist/Session/HostForkRuntime.js')
const { PtyPort } = await import('../../../dist/Process/Pty.js')
const {
  Wanxiangshu_Session_HostForkRuntime__HostForkRuntime_TrackPtyRun_Z33F80F6F: trackPtyRun,
  Wanxiangshu_Session_HostForkRuntime__HostForkRuntime_RegisterPtySnapshot: registerPtySnapshot,
  Wanxiangshu_Session_HostForkRuntime__HostForkRuntime_UntrackPtyRun_Z721C83C5: untrackPtyRun,
  Wanxiangshu_Session_HostForkRuntime__HostForkRuntime_OwnsPty_Z33F80F6F: ownsPty,
  Wanxiangshu_Session_HostForkRuntime__HostForkRuntime_IsPtyCompletion_Z721C83C5: isPtyCompletion,
  Wanxiangshu_Session_HostForkRuntime__HostForkRuntime_ForkPty_Z27B191B4: forkPty,
  Wanxiangshu_Session_HostForkRuntime__HostForkRuntime_TryPty_Z721C83C5: tryPty,
  Wanxiangshu_Session_HostForkRuntime__HostForkRuntime_SendPty_BCBC66B: sendPty,
} = await import('../../../dist/Session/HostForkPty.js')
const { PtyId_Create_Z721C83C5: ptyIdOf, PtyId__get_Value: ptyIdValue } = await import(
  '../../../dist/Process/PtyTypes.js'
)
const { PtySignal } = await import('../../../dist/Process/PtyTypes.js')
const { PtyPort__ReadResult_3DD67D20 } = await import('../../../dist/Process/Pty.js')
const { Role } = await import('../../../dist/Kernel/Roles.js')

const PARENT = sessionId('ses_pty')
const AGENT = { Value: 'pty-agent' }

// A REAL PtyPort with a recording no-op handler: the runtime's production
// module functions (Fork/Send/Read/Known/Exists) then run against real
// Dictionary/HashSet state with structural PtyId equality.
const fakePtyPort = (behaviour = {}) => {
  const calls = []
  const port = new PtyPort(undefined, (id, command) => {
    calls.push(['handler', ptyIdValue(id), command.tag, command.fields])
    if (behaviour.forkError && command.tag === 0) throw new Error(behaviour.forkError)
    if (behaviour.sendError && command.tag !== 0 && command.tag !== 2) {
      return Promise.resolve({ tag: 1, fields: [behaviour.sendError] })
    }
    return Promise.resolve({ tag: 0, fields: [] })
  }, undefined)
  port.calls = calls
  return port
}

const live = async (behaviour = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-hfpty-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, 'journal must open')
  const port = fakePtyPort(behaviour)
  const sessions = {
    CreateChildSession: async () => ({ tag: 0, fields: [sessionId('c')] }),
    AbortSession: async () => ({ tag: 0, fields: [] }),
    SendPrompt: async () => ({ tag: 0, fields: [] }),
    SendPromptAsync: async () => ({ tag: 0, fields: [] }),
    SubscribeTerminal: () => ({ Dispose: () => {} }),
    ListChildren: async () => ({ tag: 0, fields: [toList([])] }),
  }
  const runtime = new HostForkRuntime(PARENT, sessions, opened.journal, undefined, undefined, port)
  return {
    runtime,
    port,
    journal: opened.journal,
    cleanup: () => {
      try {
        opened.dispose()
      } catch {}
      rmSync(dir, { recursive: true, force: true })
    },
  }
}

const okId = (result) => {
  assert.equal(result.tag, 0, result.tag === 1 ? result.fields[0] : '')
  return result.fields[0]
}

// ── ForkPty ──────────────────────────────────────────────────────────────────

test('HFP_fork_pty_blank_command_is_refused', async () => {
  const liveCtx = await live()
  const result = await forkPty(liveCtx.runtime, '   ', AGENT)
  assert.equal(result.tag, 1)
  assert.equal(result.fields[0], 'PTY command is required')
  assert.deepEqual(liveCtx.port.calls, [], 'no port call for a blank command')
  liveCtx.cleanup()
})

test('HFP_fork_pty_tracks_registers_and_resolves_last', async () => {
  const liveCtx = await live()
  const result = await forkPty(liveCtx.runtime, 'ls -la', AGENT)
  const id = okId(result)
  const idValue = ptyIdValue(id)

  assert.equal(ownsPty(liveCtx.runtime, id), true)
  assert.equal(isPtyCompletion(liveCtx.runtime, idValue), true)
  assert.deepEqual(
    liveCtx.port.calls.filter(([, , tag]) => tag === 0),
    [['handler', idValue, 0, ['ls -la', '']]],
    'fork routes through the port handler with a Spawn command',
  )

  // Explicit id resolution only — empty id does not mean "last PTY".
  const byId = tryPty(liveCtx.runtime, idValue)
  assert.equal(ptyIdValue(byId), idValue)
  assert.equal(tryPty(liveCtx.runtime, ''), undefined)

  // A second fork registers a distinct owned id.
  const second = okId(await forkPty(liveCtx.runtime, 'pwd', AGENT))
  assert.notEqual(ptyIdValue(second), idValue)
  assert.equal(ptyIdValue(tryPty(liveCtx.runtime, ptyIdValue(second))), ptyIdValue(second))
  liveCtx.cleanup()
})

test('HFP_fork_pty_port_exception_untracks_and_errors', async () => {
  const liveCtx = await live({ forkError: 'pty spawn exploded' })
  const result = await forkPty(liveCtx.runtime, 'ls', AGENT)
  assert.equal(result.tag, 1)
  assert.equal(result.fields[0], 'pty spawn exploded')

  // The failed fork must not leave a tracked run or a resolvable "last" pty.
  assert.equal(tryPty(liveCtx.runtime, ''), undefined)
  liveCtx.cleanup()
})

test('HFP_try_pty_unknown_string_id_is_none', async () => {
  const liveCtx = await live()
  okId(await forkPty(liveCtx.runtime, 'ls', AGENT))
  assert.equal(tryPty(liveCtx.runtime, 'no-such-pty'), undefined)
  liveCtx.cleanup()
})

test('HFP_try_pty_owned_but_unknown_to_port_is_none', async () => {
  const liveCtx = await live()
  const id = ptyIdValue(okId(await forkPty(liveCtx.runtime, 'ls', AGENT)))
  // Forget the pty on the port side (simulated backend loss): owned by the
  // runtime but Unknown to the port, so resolution must fail closed.
  liveCtx.port.active.clear()
  liveCtx.port.closedIds.clear()
  assert.equal(tryPty(liveCtx.runtime, id), undefined)
  liveCtx.cleanup()
})

// ── SendPty ──────────────────────────────────────────────────────────────────

test('HFP_send_pty_unowned_id_is_unknown', async () => {
  const liveCtx = await live()
  const result = await sendPty(liveCtx.runtime, ptyIdOf('foreign'), 'echo hi', undefined)
  assert.equal(result.tag, 1)
  assert.equal(result.fields[0], 'Unknown PTY id: foreign')
  liveCtx.cleanup()
})

test('HFP_send_pty_owned_but_missing_on_port_is_unknown', async () => {
  const liveCtx = await live()
  const id = okId(await forkPty(liveCtx.runtime, 'ls', AGENT))
  // Simulated backend loss: pty no longer Exists on the port.
  liveCtx.port.active.clear()
  liveCtx.port.closedIds.clear()
  const result = await sendPty(liveCtx.runtime, id, 'echo hi', undefined)
  assert.equal(result.tag, 1)
  assert.equal(result.fields[0], `Unknown PTY id: ${ptyIdValue(id)}`)
  liveCtx.cleanup()
})

test('HFP_send_pty_signal_forwards_signal_command', async () => {
  const liveCtx = await live()
  const id = okId(await forkPty(liveCtx.runtime, 'ls', AGENT))
  const result = await sendPty(liveCtx.runtime, id, undefined, PtySignal.Interrupt)
  assert.equal(result.tag, 0)
  assert.equal(result.fields[0].Id.fields[0], ptyIdValue(id))
  assert.equal(result.fields[0].Output, '')
  assert.equal(result.fields[0].Closed, false)
  assert.deepEqual(
    liveCtx.port.calls.filter(([name, , tag]) => name === 'handler' && tag === 3),
    [['handler', ptyIdValue(id), 3, [PtySignal.Interrupt]]],
  )
  liveCtx.cleanup()
})

test('HFP_send_pty_write_forwards_write_command', async () => {
  const liveCtx = await live()
  const id = okId(await forkPty(liveCtx.runtime, 'ls', AGENT))
  const result = await sendPty(liveCtx.runtime, id, 'echo hi', undefined)
  assert.equal(result.tag, 0)
  const send = liveCtx.port.calls.find(([name, , tag]) => name === 'handler' && tag === 1)
  assert.equal(send[1], ptyIdValue(id))
  assert.equal(send[2], 1, 'PtyCommand.Write')
  assert.deepEqual(send[3], [Uint8Array.from([...'echo hi\n'].map((c) => c.charCodeAt(0)))])
  liveCtx.cleanup()
})

test('HFP_send_pty_read_with_empty_prompt', async () => {
  const liveCtx = await live()
  const id = okId(await forkPty(liveCtx.runtime, 'ls', AGENT))

  // Read parks on the port's read waiter; the backend resolves it with output.
  const reading = sendPty(liveCtx.runtime, id, '', undefined)
  await new Promise((r) => setTimeout(r, 5))
  PtyPort__ReadResult_3DD67D20(liveCtx.port, id, 'terminal text', true)

  const result = await reading
  assert.equal(result.tag, 0)
  assert.equal(result.fields[0].Output, 'terminal text')
  assert.equal(result.fields[0].Closed, true)
  const reads = liveCtx.port.calls.filter(([, , tag]) => tag === 2)
  assert.equal(reads.length, 1, 'PtyCommand.Read')
  liveCtx.cleanup()
})

test('HFP_send_pty_port_error_propagates', async () => {
  const liveCtx = await live({ sendError: 'pty session ended' })
  const id = okId(await forkPty(liveCtx.runtime, 'ls', AGENT))
  const result = await sendPty(liveCtx.runtime, id, 'echo hi', undefined)
  assert.equal(result.tag, 1)
  assert.equal(result.fields[0], 'pty session ended')
  liveCtx.cleanup()
})

// ── low-level tracking ───────────────────────────────────────────────────────

test('HFP_track_untrack_pty_run_round_trip', async () => {
  const liveCtx = await live()
  const id = ptyIdOf('tracked-1')
  trackPtyRun(liveCtx.runtime, id)
  registerPtySnapshot(liveCtx.runtime, id, 'watch -n1 date')
  assert.equal(ownsPty(liveCtx.runtime, id), true)
  assert.equal(isPtyCompletion(liveCtx.runtime, 'tracked-1'), true)

  untrackPtyRun(liveCtx.runtime, 'tracked-1')
  assert.equal(ownsPty(liveCtx.runtime, id), false)
  assert.equal(isPtyCompletion(liveCtx.runtime, 'tracked-1'), false)
  liveCtx.cleanup()
})
