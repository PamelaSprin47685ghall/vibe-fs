// tests/unit/session/host-fork-runtime.test.mjs — HostForkRuntime member
// surface coverage: InstallRun/FailRun/MarkReady/CancelAgent/Join/JoinAvailable/
// JoinWithPermit/JoinAvailableWithPermit/AwaitAgent/AwaitAgentWithPermit/
// validatePermit error branches, plus the plain ForkRuntime surface
// (Fork/AwaitAgent/CancelAgent/List/Cancel).

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { agentJournal, caseOf, listItems, sessionId, toList } from '../support/domain.mjs'

const { HostForkRuntime } = await import('../../../dist/Session/HostForkRuntime.js')
const {
  HostForkRuntime__InstallRun_7AC6F164: installRun,
  HostForkRuntime__FailRun_1B5DABF9: failRun,
  HostForkRuntime__MarkReady_Z397E187E: markReady,
  HostForkRuntime__CancelAgent_Z721C83C5: cancelAgent,
  HostForkRuntime__Join_71136F3F: joinAny,
  HostForkRuntime__JoinAvailable_Z2FFF68F8: joinAvailable,
  HostForkRuntime__JoinWithPermit_22872FC4: joinWithPermit,
  HostForkRuntime__JoinAvailableWithPermit_76145D53: joinAvailableWithPermit,
  HostForkRuntime__AwaitAgent_3B406CA4: awaitAgent,
  HostForkRuntime__AwaitAgentWithPermit_Z23B24401: awaitAgentWithPermit,
  HostForkRuntime__IsRetiredHandle_Z721C83C5: isRetiredHandle,
  HostForkRuntime__TryChildSession_Z721C83C5: tryChildSession,
  HostForkRuntime__AdoptChild_Z7BE1869F: adoptChild,
  HostForkRuntime__List: listRuntime,
  HostForkRuntime__get_PendingRunCount: pendingRunCount,
  HostForkRuntime__get_PendingCompletionCount: pendingCompletionCount,
  HostForkRuntime__get_IsCancelled: runtimeIsCancelled,
  HostForkRuntime__Cancel: cancelRuntime,
} = await import('../../../dist/Session/HostForkRuntime.js')
const {
  ForkRuntime,
  ForkRuntime__Fork_374A2FD6: forkRun,
  ForkRuntime__AwaitAgent_3B406CA4: forkAwaitAgent,
  ForkRuntime__CancelAgent_Z721C83C5: forkCancelAgent,
  ForkRuntime__List: forkList,
  ForkRuntime__Cancel: forkCancel,
  ForkRuntime__get_ActiveRunCount: forkActiveRunCount,
  ForkRuntime__get_PendingCompletionCount: forkPendingCompletions,
} = await import('../../../dist/Session/ForkRuntime.js')
const { ForkError, AgentStatus } = await import('../../../dist/Session/ForkTypes.js')
const { Role } = await import('../../../dist/Kernel/Roles.js')
const {
  FamilyRecoveryPermit,
} = await import('../../../dist/Domain/SessionRecovery.js')
const { AgentJournalModule_revision, AgentJournalModule_snapshot } = await import(
  '../../../dist/Journal/AgentJournal.js'
)
const { JournalRevisionModule_value } = await import('../../../dist/Kernel/Identity.js')
const { discover } = await import('../../../dist/Journal/RecoveryClosureProjection.js')
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
const live = (behaviour = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-hfrt-'))
  const opened = agentJournal.create({ directory: dir })
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

const link = (j, agentId, child, agent = 'fast-coder', role = Role.Coder) => {
  const result = HandleController_link(j, PARENT, agentId, child, agent, role, HandleOwnership.DurableParentHandle)
  assert.equal(result.tag, 0, result.tag === 1 ? result.fields[0] : '')
}

/** Permit that validates against the journal's CURRENT closure. */
const validPermit = (j) => {
  const sequence = JournalRevisionModule_value(AgentJournalModule_revision(j))
  const closure = discover(PARENT, AgentJournalModule_snapshot(j).AgentProjections, sequence)
  return new FamilyRecoveryPermit(PARENT, sequence, closure.Digest)
}

const deferred = () => {
  let resolve
  const promise = new Promise((r) => (resolve = r))
  return { promise, resolve }
}

const batchItems = (batch) => [batch.fields[0], ...listItems(batch.fields[1])]

// ── InstallRun / FailRun / MarkReady ─────────────────────────────────────────

test('HFRT_install_run_registers_pending_run_and_child', async () => {
  const liveCtx = live()
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
  const liveCtx = live()
  const run = installRun(liveCtx.runtime, 'ag2', sessionId('ses_c2'), Role.Coder)
  markReady(liveCtx.runtime, run)
  assert.equal(pendingRunCount(liveCtx.runtime), 1, 'MarkReady must not settle the run')
  assert.equal(run.Finished, false)
  liveCtx.cleanup()
})

test('HFRT_fail_run_writes_durable_failure_and_settles_source', async () => {
  const liveCtx = live()
  link(liveCtx.journal, 'ag3', sessionId('ses_c3'))
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
  const liveCtx = live()
  link(liveCtx.journal, 'ag4', sessionId('ses_c4'))
  const run = installRun(liveCtx.runtime, 'ag4', sessionId('ses_c4'), Role.Coder)
  failRun(liveCtx.runtime, run, 'cancelled')
  const outcome = await run.Source.get_Task()
  assert.equal(outcome.fields[0].Code, 'CANCELLED')
  liveCtx.cleanup()
})

test('HFRT_is_retired_handle_reflects_durable_projection', async () => {
  const liveCtx = live()
  link(liveCtx.journal, 'ag5', sessionId('ses_c5'))
  assert.equal(isRetiredHandle(liveCtx.runtime, 'ag5'), false)

  // Retire the handle through the projection CAS path.
  const { handleController, handleCompletionCodec, agentCompletion } = await import('../support/domain.mjs')
  const sealed = agentCompletion.completedRun({ runId: 'run-ag5', agentId: 'ag5', agentName: 'fast-coder', workRecord: 'w' })
  const body = handleCompletionCodec.encodeOutcome(sealed.RunId, sealed.Outcome)
  const recorded = handleController.recordCompletion(liveCtx.journal, PARENT, 'ag5', 'Terminal', body, sessionId('ses_c5'))
  assert.equal(recorded.ok, true, recorded.ok ? '' : recorded.error)
  const consumed = handleController.consume(liveCtx.journal, PARENT, await import('../support/domain.mjs').then((m) => m.handleId.agent('ag5')))
  assert.equal(consumed.ok, true, consumed.ok ? '' : consumed.error)
  assert.equal(isRetiredHandle(liveCtx.runtime, 'ag5'), true)
  liveCtx.cleanup()
})

// ── CancelAgent ──────────────────────────────────────────────────────────────

test('HFRT_cancel_agent_fails_pending_run_and_aborts_child', async () => {
  const liveCtx = live()
  link(liveCtx.journal, 'ag6', sessionId('ses_c6'))
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
  const liveCtx = live()
  link(liveCtx.journal, 'ag7', sessionId('ses_c7'))
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

// ── Join / JoinAvailable ─────────────────────────────────────────────────────

test('HFRT_join_available_without_work_is_nothing_to_join', async () => {
  const liveCtx = live()
  const result = await joinAvailable(liveCtx.runtime, 5, new Promise(() => {}))
  assert.equal(result.tag, 1)
  assert.equal(caseOf(result.fields[0]), 'NothingToJoin')
  liveCtx.cleanup()
})

test('HFRT_join_available_with_interrupt_returns_interrupted', async () => {
  const liveCtx = live()
  installRun(liveCtx.runtime, 'ag8', sessionId('ses_c8'), Role.Coder)

  const result = await joinAvailable(liveCtx.runtime, 5, Promise.resolve('DeadlineExpired'))
  assert.equal(result.tag, 0)
  assert.equal(caseOf(result.fields[0]), 'Interrupted')
  assert.equal(result.fields[0].fields[0], 'DeadlineExpired')
  liveCtx.cleanup()
})

test('HFRT_join_single_times_out_when_no_completion_arrives', async () => {
  const liveCtx = live()
  installRun(liveCtx.runtime, 'ag9', sessionId('ses_c9'), Role.Coder)

  const result = await joinAny(liveCtx.runtime, 30)
  assert.equal(result.tag, 1)
  assert.equal(caseOf(result.fields[0]), 'TimedOut')
  liveCtx.cleanup()
})

test('HFRT_join_cancelled_runtime_returns_cancelled', async () => {
  const liveCtx = live()
  cancelRuntime(liveCtx.runtime)
  assert.equal(runtimeIsCancelled(liveCtx.runtime), true)
  const result = await joinAny(liveCtx.runtime, 10)
  assert.equal(result.tag, 1)
  assert.equal(caseOf(result.fields[0]), 'Cancelled')
  liveCtx.cleanup()
})

// ── validatePermit branches (via JoinWithPermit / JoinAvailableWithPermit) ───

test('HFRT_join_with_permit_root_mismatch_is_not_found', async () => {
  const liveCtx = live()
  const permit = new FamilyRecoveryPermit(sessionId('ses_other'), 0n, '')
  const result = await joinWithPermit(liveCtx.runtime, permit)
  assert.equal(result.tag, 1)
  const err = result.fields[0]
  assert.equal(caseOf(err), 'NotFound')
  assert.match(err.fields[0], /family recovery permit root mismatch: permit=ses_other runtime=ses_hfrt/)
  liveCtx.cleanup()
})

test('HFRT_join_with_permit_stale_journal_sequence_is_not_found', async () => {
  const liveCtx = live()
  const current = JournalRevisionModule_value(AgentJournalModule_revision(liveCtx.journal))
  const permit = new FamilyRecoveryPermit(PARENT, current + 1000n, '')
  const result = await joinWithPermit(liveCtx.runtime, permit)
  const err = result.fields[0]
  assert.equal(caseOf(err), 'NotFound')
  assert.match(err.fields[0], new RegExp(`family recovery permit journalSequence stale: permit=${current + 1000n}`))
  liveCtx.cleanup()
})

test('HFRT_join_with_permit_closure_digest_mismatch_is_not_found', async () => {
  const liveCtx = live()
  const sequence = JournalRevisionModule_value(AgentJournalModule_revision(liveCtx.journal))
  const permit = new FamilyRecoveryPermit(PARENT, sequence, 'deadbeef')
  const result = await joinAvailableWithPermit(liveCtx.runtime, permit, 5, new Promise(() => {}))
  const err = result.fields[0]
  assert.equal(caseOf(err), 'NotFound')
  assert.match(err.fields[0], /family recovery permit closureDigest mismatch: permit=deadbeef current=/)
  liveCtx.cleanup()
})

test('HFRT_join_with_valid_permit_passes_validation', async () => {
  const liveCtx = live()
  const permit = validPermit(liveCtx.journal)
  const result = await joinWithPermit(liveCtx.runtime, permit, 10)
  assert.equal(result.tag, 1)
  assert.equal(caseOf(result.fields[0]), 'NothingToJoin', 'valid permit must reach the join body')
  liveCtx.cleanup()
})

// ── AwaitAgent ───────────────────────────────────────────────────────────────

test('HFRT_await_agent_unknown_id_is_error', async () => {
  const liveCtx = live()
  const result = await awaitAgent(liveCtx.runtime, 'ghost')
  assert.equal(result.tag, 1)
  assert.equal(result.fields[0], 'Unknown agent id: ghost')
  liveCtx.cleanup()
})

test('HFRT_await_agent_with_permit_validation_error_maps_to_not_found', async () => {
  const liveCtx = live()
  const permit = new FamilyRecoveryPermit(sessionId('ses_other'), 0n, '')
  const result = await awaitAgentWithPermit(liveCtx.runtime, permit, 'ag9')
  assert.equal(result.tag, 1)
  assert.equal(caseOf(result.fields[0]), 'NotFound')
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
