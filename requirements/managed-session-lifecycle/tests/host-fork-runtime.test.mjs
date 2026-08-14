// Split from tests/unit/session/host-fork-runtime.test.mjs (cutover Wave 2a); owner: managed-session-lifecycle.
//
// HostForkRuntime 成员面：InstallRun/FailRun/MarkReady/IsRetiredHandle/
// CancelAgent（含 AbortSession 级联）+ plain ForkRuntime 面（Fork/AwaitAgent/
// CancelAgent/List/Cancel）。join/await 调用代数已随 SPLIT@cutover 迁
// requirements/delegation/tests/host-fork-join-algebra.test.mjs；permit 校验/
// EXEC-023 迁 requirements/crash-reconciliation/tests/host-fork-runtime-permit.test.mjs。

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { agentJournal, caseOf, listItems, sessionId, toList } from '../../verification-system/tests/support/domain.mjs'

const { HostForkRuntime } = await import('../../../dist/Session/HostForkRuntime.js')
const {
  HostForkRuntime__InstallRun_7AC6F164: installRun,
  HostForkRuntime__FailRun_1B5DABF9: failRun,
  HostForkRuntime__MarkReady_Z397E187E: markReady,
  HostForkRuntime__IsRetiredHandle_Z721C83C5: isRetiredHandle,
  HostForkRuntime__TryChildSession_Z721C83C5: tryChildSession,
  HostForkRuntime__AdoptChild_Z7BE1869F: adoptChild,
  HostForkRuntime__get_PendingRunCount: pendingRunCount,
  HostForkRuntime__get_IsCancelled: runtimeIsCancelled,
  HostForkRuntime__Cancel: cancelRuntime,
} = await import('../../../dist/Session/HostForkRuntime.js')
const { joinAvailable, cancelAgent } = await import('../../../dist/Session/HostForkJoin.js')
const {
  ForkRuntime,
  ForkRuntime__Fork_374A2FD6: forkRun,
  ForkRuntime__AwaitAgent_3B406CA4: forkAwaitAgent,
  ForkRuntime__CancelAgent_Z721C83C5: forkCancelAgent,
  ForkRuntime__List: forkList,
  ForkRuntime__Cancel: forkCancel,
  ForkRuntime__get_ActiveRunCount: forkActiveRunCount,
} = await import('../../../dist/Session/ForkRuntime.js')
const { Role } = await import('../../../dist/Kernel/Roles.js')
const { HandleController_link } = await import('../../../dist/Session/HandleController.js')
const { HandleOwnership } = await import('../../../dist/Kernel/Fact.js')

const PARENT = sessionId('ses_hfrt')

const fakeSessions = () => {
  const calls = []
  return {
    calls,
    CreateChildSession: async () => ({ tag: 0, fields: [sessionId('child-1')] }),
    AbortSession: async (id) => {
      calls.push(['AbortSession', id.fields?.[0] ?? id])
      return { tag: 0, fields: [] }
    },
    SendPrompt: async () => ({ tag: 0, fields: [] }),
    SendPromptAsync: async () => ({ tag: 0, fields: [] }),
    SubscribeTerminal: () => ({ Dispose: () => {} }),
    ListChildren: async () => ({ tag: 0, fields: [toList([])] }),
  }
}

/** Real runtime over a real journal with a fake session host. */
const live = async (behaviour = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-hfrt-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, 'journal must open')
  const sessions = fakeSessions()
  const runtime = new HostForkRuntime(PARENT, sessions, opened.journal)
  return {
    runtime,
    sessions,
    journal: opened.journal,
    cleanup: () => {
      try {
        opened.dispose()
      } catch {}
      rmSync(dir, { recursive: true, force: true })
    },
  }
}

const link = async (j, agentId, child, agent = 'fast-coder', role = Role.Coder) => {
  const result = await HandleController_link(j, PARENT, agentId, child, agent, role, HandleOwnership.DurableParentHandle)
  assert.equal(result.tag, 0, result.tag === 1 ? result.fields[0] : '')
}

const deferred = () => {
  let resolve
  const promise = new Promise((r) => (resolve = r))
  return { promise, resolve }
}

const batchItems = (batch) => [batch.fields[0], ...listItems(batch.fields[1])]

// ── InstallRun / FailRun / MarkReady ─────────────────────────────────────────

test('HFRT_install_run_registers_pending_run_and_child', async () => {
  const liveCtx = await live()
  const run = installRun(liveCtx.runtime, 'ag1', sessionId('ses_c1'), Role.Coder)

  assert.equal(run.AgentId, 'ag1')
  assert.equal(run.ChildId.fields[0], 'ses_c1')
  assert.equal(run.Finished, false)
  assert.equal(pendingRunCount(liveCtx.runtime), 1)
  // The Host-level children map is populated by Fork/AdoptChild/restore, not
  // by InstallRun alone (which binds only the inner ForkRuntime).
  assert.equal(tryChildSession(liveCtx.runtime, 'ag1'), undefined)
  adoptChild(liveCtx.runtime, 'ag1', sessionId('ses_c1'))
  assert.equal(tryChildSession(liveCtx.runtime, 'ag1').fields[0], 'ses_c1')
  assert.equal(runtimeIsCancelled(liveCtx.runtime), false)
  liveCtx.cleanup()
})

test('HFRT_mark_ready_is_noop_and_run_stays_pending', async () => {
  const liveCtx = await live()
  const run = installRun(liveCtx.runtime, 'ag2', sessionId('ses_c2'), Role.Coder)
  markReady(liveCtx.runtime, run)
  assert.equal(pendingRunCount(liveCtx.runtime), 1, 'MarkReady must not settle the run')
  assert.equal(run.Finished, false)
  liveCtx.cleanup()
})

test('HFRT_fail_run_writes_durable_failure_and_settles_source', async () => {
  const liveCtx = await live()
  await link(liveCtx.journal, 'ag3', sessionId('ses_c3'))
  const run = installRun(liveCtx.runtime, 'ag3', sessionId('ses_c3'), Role.Coder)
  failRun(liveCtx.runtime, run, 'boom')

  assert.equal(run.Finished, true)
  assert.equal(pendingRunCount(liveCtx.runtime), 0)
  const outcome = await run.Source.get_Task()
  assert.equal(caseOf(outcome), 'AgentFailed')
  assert.equal(outcome.fields[0].Code, 'ERROR')
  assert.equal(outcome.fields[0].Message, 'boom')

  // The durable completion is joinable through the production drain.
  const joined = await joinAvailable(liveCtx.runtime, 5, new Promise(() => {}))
  assert.equal(joined.tag, 0, joined.tag === 1 ? caseOf(joined.fields[0]) : '')
  const batch = joined.fields[0]
  assert.equal(caseOf(batch), 'ResultsAvailable')
  const items = batchItems(batch.fields[0])
  assert.equal(items.length, 1)
  assert.equal(caseOf(items[0]), 'AgentItem')
  assert.equal(caseOf(items[0].fields[0]), 'AgentFailedItem')
  assert.equal(items[0].fields[0].fields[0].Code, 'ERROR')
  liveCtx.cleanup()
})

test('HFRT_fail_run_cancelled_code_is_CANCELLED', async () => {
  const liveCtx = await live()
  await link(liveCtx.journal, 'ag4', sessionId('ses_c4'))
  const run = installRun(liveCtx.runtime, 'ag4', sessionId('ses_c4'), Role.Coder)
  failRun(liveCtx.runtime, run, 'cancelled')
  const outcome = await run.Source.get_Task()
  assert.equal(outcome.fields[0].Code, 'CANCELLED')
  liveCtx.cleanup()
})

test('HFRT_is_retired_handle_reflects_durable_projection', async () => {
  const liveCtx = await live()
  await link(liveCtx.journal, 'ag5', sessionId('ses_c5'))
  assert.equal(isRetiredHandle(liveCtx.runtime, 'ag5'), false)

  // Retire the handle through the projection CAS path.
  const { handleController, handleCompletionCodec, agentCompletion } = await import('../../verification-system/tests/support/domain.mjs')
  const sealed = agentCompletion.completedRun({ runId: 'run-ag5', agentId: 'ag5', agentName: 'fast-coder', workRecord: 'w' })
  const body = handleCompletionCodec.encodeOutcome(sealed.RunId, sealed.Outcome)
  const recorded = await handleController.recordCompletion(liveCtx.journal, PARENT, 'ag5', 'Terminal', body, sessionId('ses_c5'))
  assert.equal(recorded.ok, true, recorded.ok ? '' : recorded.error)
  const consumed = await handleController.consume(liveCtx.journal, PARENT, await import('../../verification-system/tests/support/domain.mjs').then((m) => m.handleId.agent('ag5')))
  assert.equal(consumed.ok, true, consumed.ok ? '' : consumed.error)
  assert.equal(isRetiredHandle(liveCtx.runtime, 'ag5'), true)
  liveCtx.cleanup()
})

// ── CancelAgent ──────────────────────────────────────────────────────────────

test('HFRT_cancel_agent_fails_pending_run_and_aborts_child', async () => {
  const liveCtx = await live()
  await link(liveCtx.journal, 'ag6', sessionId('ses_c6'))
  const run = installRun(liveCtx.runtime, 'ag6', sessionId('ses_c6'), Role.Coder)
  adoptChild(liveCtx.runtime, 'ag6', sessionId('ses_c6'))
  const source = run.Source.get_Task()
  cancelAgent(liveCtx.runtime, 'ag6')

  const outcome = await source
  assert.equal(caseOf(outcome), 'AgentFailed')
  assert.equal(outcome.fields[0].Code, 'CANCELLED')
  assert.deepEqual(liveCtx.sessions.calls.filter(([name]) => name === 'AbortSession'), [['AbortSession', 'ses_c6']])

  // The durable failure drains as a joinable batch item.
  const joined = await joinAvailable(liveCtx.runtime, 5, new Promise(() => {}))
  assert.equal(joined.tag, 0)
  assert.equal(batchItems(joined.fields[0].fields[0]).length, 1)
  liveCtx.cleanup()
})

test('HFRT_cancel_agent_after_run_settled_skips_fail_run_but_aborts_child', async () => {
  const liveCtx = await live()
  await link(liveCtx.journal, 'ag7', sessionId('ses_c7'))
  const run = installRun(liveCtx.runtime, 'ag7', sessionId('ses_c7'), Role.Coder)
  adoptChild(liveCtx.runtime, 'ag7', sessionId('ses_c7'))
  failRun(liveCtx.runtime, run, 'already done')
  assert.equal(pendingRunCount(liveCtx.runtime), 0)

  // No pending run anymore: CancelAgent must not throw, and the child session
  // is still aborted (the map retains the child until join retires it).
  cancelAgent(liveCtx.runtime, 'ag7')
  assert.deepEqual(liveCtx.sessions.calls.filter(([name]) => name === 'AbortSession'), [['AbortSession', 'ses_c7']])
  liveCtx.cleanup()
})

// ── plain ForkRuntime surface ────────────────────────────────────────────────

test('HFRT_fork_runtime_fork_created_then_list_records_busy', async () => {
  const runtime = new ForkRuntime()
  const result = forkRun(runtime, 'fr1', Role.Coder, 'fast-coder', 'do it')
  assert.equal(caseOf(result), 'Created')
  const [agents] = forkList(runtime)
  const records = listItems(agents)
  assert.equal(records.length, 1)
  assert.equal(records[0].AgentId, 'fr1')
  assert.equal(records[0].Agent, 'fast-coder')
  assert.equal(caseOf(records[0].Status), 'Busy')
  assert.equal(forkActiveRunCount(runtime), 1)
})

test('HFRT_fork_runtime_await_agent_returns_completion', async () => {
  const runtime = new ForkRuntime()
  forkRun(runtime, 'fr2', Role.Coder, 'fast-coder', 'do it')
  const result = await forkAwaitAgent(runtime, 'fr2')
  assert.equal(result.tag, 0)
  const completion = result.fields[0]
  assert.equal(caseOf(completion.Outcome), 'AgentCompleted')
  assert.equal(completion.AgentId, 'fr2')
})

test('HFRT_fork_runtime_await_agent_unknown_and_timeout_are_errors', async () => {
  const runtime = new ForkRuntime()
  const unknown = await forkAwaitAgent(runtime, 'nope')
  assert.equal(unknown.tag, 1)
  assert.equal(unknown.fields[0], 'Unknown agent id: nope')

  const gate = deferred()
  forkRun(runtime, 'fr3', Role.Coder, 'fast-coder', 'do it', () => gate.promise.then(() => undefined))
  const timedOut = await forkAwaitAgent(runtime, 'fr3', 20)
  assert.equal(timedOut.tag, 1)
  assert.equal(timedOut.fields[0], 'await agent timed out: fr3')
  gate.resolve(undefined)
})

test('HFRT_fork_runtime_cancel_agent_marks_run_closed', async () => {
  const runtime = new ForkRuntime()
  forkRun(runtime, 'fr4', Role.Coder, 'fast-coder', 'do it')
  forkCancelAgent(runtime, 'fr4')
  const [agents] = forkList(runtime)
  const records = listItems(agents)
  assert.equal(records[0].AgentId, 'fr4')
  assert.equal(caseOf(records[0].Status), 'Closed')
  assert.equal(forkActiveRunCount(runtime), 0)
})

test('HFRT_fork_runtime_cancel_then_fork_is_not_found', async () => {
  const runtime = new ForkRuntime()
  forkCancel(runtime)
  const result = forkRun(runtime, 'fr5', Role.Coder, 'fast-coder', 'do it')
  assert.equal(caseOf(result), 'NotFound')
})

test('HFRT_fork_runtime_busy_agent_nudges_not_created', async () => {
  const runtime = new ForkRuntime()
  forkRun(runtime, 'fr6', Role.Coder, 'fast-coder', 'do it')
  const second = forkRun(runtime, 'fr6', Role.Coder, 'fast-coder', 'more')
  assert.equal(caseOf(second), 'Nudged')
  const [agents] = forkList(runtime)
  assert.equal(listItems(agents).length, 1)
})
