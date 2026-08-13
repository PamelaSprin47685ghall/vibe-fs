// tests/unit/session/host-fork-agent.test.mjs — HostForkAgent Fork/Reuse error
// branches against a real journal: retired/abandoned handle refusal, linkage
// failure aborting the child session, send failure failing the run, cancelled
// runtime, and the reuse-after-join prompt path.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { agentJournal, caseOf, listItems, sessionId, toList, utcOffset } from '../support/domain.mjs'

const { HostForkRuntime } = await import('../../../dist/Session/HostForkRuntime.js')
const hostForkAgentModule = await import('../../../dist/Session/HostForkAgent.js')
const fork = Object.entries(hostForkAgentModule).find(
  ([name, value]) => name.includes('_HostForkRuntime_Fork_') && typeof value === 'function',
)?.[1]
assert.equal(typeof fork, 'function', 'HostForkRuntime Fork export must be discoverable without pinning Fable hash')
const reuse = Object.entries(hostForkAgentModule).find(
  ([name, value]) => name.includes('_HostForkRuntime_Reuse_') && typeof value === 'function',
)?.[1]
assert.equal(typeof reuse, 'function', 'HostForkRuntime Reuse export must be discoverable without pinning Fable hash')
const {
  HostForkRuntime__get_PendingRunCount: pendingRunCount,
  HostForkRuntime__get_PendingRuns: pendingRunsOf,
  HostForkRuntime__FailRun_1B5DABF9: failRun,
  HostForkRuntime__Cancel: cancelRuntime,
} = await import('../../../dist/Session/HostForkRuntime.js')
const { HandleController_link } = await import('../../../dist/Session/HandleController.js')
const { HandleOwnership } = await import('../../../dist/Kernel/Fact.js')
const { Role } = await import('../../../dist/Kernel/Roles.js')
const { drainFromJournal: JoinDrain_drainFromJournal } = await import('../../../dist/Session/JoinDrain.js')

const PARENT = sessionId('ses_hfa')

const fakeSessions = (behaviour = {}) => {
  const calls = []
  return {
    calls,
    CreateChildSession: async (parentId, options) => {
      calls.push(['CreateChildSession', options])
      if (behaviour.createError) return { tag: 1, fields: [behaviour.createError] }
      return { tag: 0, fields: [sessionId('child-1')] }
    },
    AbortSession: async (id) => {
      calls.push(['AbortSession', id.fields?.[0] ?? id])
      return { tag: 0, fields: [] }
    },
    SendPrompt: async (...args) => {
      calls.push(['SendPrompt', ...args])
      if (behaviour.sendError) return { tag: 2, fields: [behaviour.sendError] }
      return { tag: 0, fields: [sessionId('msg-1')] }
    },
    SendPromptAsync: async (...args) => {
      calls.push(['SendPromptAsync', ...args])
      if (behaviour.sendError) return { tag: 2, fields: [behaviour.sendError] }
      return { tag: 0, fields: [sessionId('msg-1')] }
    },
    SubscribeTerminal: () => ({ Dispose: () => {} }),
    ListChildren: async () => ({ tag: 0, fields: [toList([])] }),
  }
}

const live = (behaviour = {}, { disposedJournal = false } = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-hfa-'))
  const opened = agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, 'journal must open')
  if (disposedJournal) opened.dispose()
  const sessions = fakeSessions(behaviour)
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

const link = (j, agentId, child, agent = 'fast-coder') => {
  const result = HandleController_link(j, PARENT, agentId, child, agent, Role.Coder, HandleOwnership.DurableParentHandle)
  assert.equal(result.tag, 0, result.tag === 1 ? result.fields[0] : '')
}

const abandon = async (j, agentId) => {
  const { handleController } = await import('../support/domain.mjs')
  const result = handleController.recordAbandon(j, PARENT, agentId, 'DeadlineExceeded')
  assert.equal(result.ok, true, result.ok ? '' : result.error)
}

const retire = async (j, agentId) => {
  const { agentCompletion, handleCompletionCodec, handleController, handleId } = await import('../support/domain.mjs')
  const sealed = agentCompletion.completedRun({ runId: `run-${agentId}`, agentId, agentName: 'fast-coder', workRecord: 'w' })
  const body = handleCompletionCodec.encodeOutcome(sealed.RunId, sealed.Outcome)
  const recorded = handleController.recordCompletion(j, PARENT, agentId, 'Terminal', body, sessionId('ses_c'))
  assert.equal(recorded.ok, true, recorded.ok ? '' : recorded.error)
  const consumed = handleController.consume(j, PARENT, handleId.agent(agentId))
  assert.equal(consumed.ok, true, consumed.ok ? '' : consumed.error)
}

// ── Fork refusals ────────────────────────────────────────────────────────────

test('HFA_fork_retired_handle_is_refused_before_spawn', async () => {
  const liveCtx = live()
  link(liveCtx.journal, 'hf1', sessionId('ses_c'))
  await retire(liveCtx.journal, 'hf1')

  const result = await fork(liveCtx.runtime, 'hf1', Role.Coder, 'fast-coder', 'do work')
  assert.equal(result.tag, 1)
  assert.equal(result.fields[0], 'RetiredHandle: hf1')
  assert.deepEqual(liveCtx.sessions.calls, [], 'a retired handle must never reach the session host')
  liveCtx.cleanup()
})

test('HFA_fork_abandoned_handle_is_refused_before_spawn', async () => {
  const liveCtx = live()
  link(liveCtx.journal, 'hf2', sessionId('ses_c'))
  await abandon(liveCtx.journal, 'hf2')

  const result = await fork(liveCtx.runtime, 'hf2', Role.Coder, 'fast-coder', 'do work')
  assert.equal(result.tag, 1)
  assert.equal(result.fields[0], 'RetiredHandle: hf2')
  assert.deepEqual(liveCtx.sessions.calls, [])
  liveCtx.cleanup()
})

test('HFA_fork_create_session_failure_surfaces_host_error', async () => {
  const liveCtx = live({ createError: 'host refused' })
  const result = await fork(liveCtx.runtime, 'hf3', Role.Coder, 'fast-coder', 'do work')
  assert.equal(result.tag, 1)
  assert.equal(result.fields[0], 'host refused')
  liveCtx.cleanup()
})

test('HFA_fork_linkage_failure_aborts_the_new_child', async () => {
  // A journal whose writer is gone makes the HandleLinked append fail; the
  // freshly created child session must be aborted and the error surfaced.
  const liveCtx = live({}, { disposedJournal: true })
  const result = await fork(liveCtx.runtime, 'hf4', Role.Coder, 'fast-coder', 'do work')

  assert.equal(result.tag, 1)
  assert.match(result.fields[0], /^Failed to persist HandleLinked: .*Writer is poisoned or disposed/)
  const aborts = liveCtx.sessions.calls.filter(([name]) => name === 'AbortSession')
  assert.equal(aborts.length, 1, 'the unlinked child must be aborted')
  liveCtx.cleanup()
})

test('HFA_fork_send_failure_fails_the_pending_run', async () => {
  const liveCtx = live({ sendError: 'prompt rejected' })
  const result = await fork(liveCtx.runtime, 'hf5', Role.Coder, 'fast-coder', 'do work')

  assert.equal(result.tag, 1)
  assert.equal(result.fields[0], 'prompt rejected')
  assert.equal(pendingRunCount(liveCtx.runtime), 0, 'the run must be failed, not left pending')

  // The failure was written durably: the handle is joinable with a failed item.
  const drained = JoinDrain_drainFromJournal(liveCtx.journal, PARENT, 5, utcOffset('2024-01-01T00:00:00.000Z'))
  assert.equal(drained.tag, 0, drained.tag === 1 ? drained.fields[0] : '')
  const items = listItems(drained.fields[0])
  assert.equal(items.length, 1)
  assert.equal(items[0].AgentId, 'hf5')
  liveCtx.cleanup()
})

test('HFA_fork_cancelled_runtime_is_not_found_and_fails_run', async () => {
  const liveCtx = live()
  cancelRuntime(liveCtx.runtime)
  const result = await fork(liveCtx.runtime, 'hf6', Role.Coder, 'fast-coder', 'do work')
  assert.equal(result.tag, 1)
  assert.equal(result.fields[0], 'Fork runtime is cancelled')
  assert.equal(pendingRunCount(liveCtx.runtime), 0)
  liveCtx.cleanup()
})

// ── Reuse ────────────────────────────────────────────────────────────────────

test('HFA_reuse_unknown_agent_id_is_error', async () => {
  const liveCtx = live()
  const result = await reuse(liveCtx.runtime, 'ghost', 'continue please')
  assert.equal(result.tag, 1)
  assert.equal(result.fields[0], 'Unknown agent id: ghost')
  liveCtx.cleanup()
})

test('HFA_reuse_abandoned_handle_is_retired_error', async () => {
  const liveCtx = live()
  link(liveCtx.journal, 'hf7', sessionId('ses_c'))
  await abandon(liveCtx.journal, 'hf7')

  const result = await reuse(liveCtx.runtime, 'hf7', 'continue please')
  assert.equal(result.tag, 1)
  assert.equal(result.fields[0], 'RetiredHandle: hf7')
  liveCtx.cleanup()
})

test('HFA_reuse_after_join_sends_prompt_on_same_child', async () => {
  const liveCtx = live()
  const forked = await fork(liveCtx.runtime, 'hf8', Role.Coder, 'fast-coder', 'first task')
  assert.equal(forked.tag, 0)
  assert.equal(caseOf(forked.fields[0]), 'Created')

  // Settle the first work unit so the reuse takes the idle-existing-child path.
  const run = pendingRunsOf(liveCtx.runtime).get('hf8')
  failRun(liveCtx.runtime, run, 'settled')
  assert.equal(pendingRunCount(liveCtx.runtime), 0)

  const reused = await reuse(liveCtx.runtime, 'hf8', 'continue please')
  assert.equal(reused.tag, 0, reused.tag === 1 ? reused.fields[0] : '')
  assert.equal(caseOf(reused.fields[0]), 'Nudged')
  assert.equal(reused.fields[0].fields[0], 'hf8')
  const prompts = liveCtx.sessions.calls.filter(([name]) => name === 'SendPromptAsync' || name === 'SendPrompt')
  assert.equal(prompts.length, 2, 'first fork + reuse each send a prompt')
  const text = JSON.stringify(prompts)
  assert.match(text, /continue please/)
  assert.equal(liveCtx.sessions.calls.filter(([name]) => name === 'CreateChildSession').length, 1, 'reuse must not spawn')
  liveCtx.cleanup()
})

test('HFA_existing_fork_keeps_deep_agent_when_caller_passes_fast', async () => {
  const liveCtx = live()
  const first = await fork(liveCtx.runtime, 'hf-deep', Role.Coder, 'deep-coder', 'first task')
  assert.equal(first.tag, 0, first.tag === 1 ? first.fields[0] : '')

  const run = pendingRunsOf(liveCtx.runtime).get('hf-deep')
  failRun(liveCtx.runtime, run, 'settled')
  assert.equal(pendingRunCount(liveCtx.runtime), 0)

  const second = await fork(liveCtx.runtime, 'hf-deep', Role.Coder, 'fast-coder', 'continue please')
  assert.equal(second.tag, 0, second.tag === 1 ? second.fields[0] : '')

  const prompts = liveCtx.sessions.calls.filter(([name]) => name === 'SendPromptAsync' || name === 'SendPrompt')
  assert.equal(prompts.length, 2, 'first fork + existing-child each send a prompt')
  const agents = prompts.map((call) => call[3]?.Agent)
  assert.deepEqual(agents, ['deep-coder', 'deep-coder'])
  assert.equal(liveCtx.sessions.calls.filter(([name]) => name === 'CreateChildSession').length, 1)
  liveCtx.cleanup()
})

test('HFA_reuse_keeps_deep_agent', async () => {
  const liveCtx = live()
  const first = await fork(liveCtx.runtime, 'hf-reuse-deep', Role.Coder, 'deep-coder', 'first task')
  assert.equal(first.tag, 0, first.tag === 1 ? first.fields[0] : '')

  const run = pendingRunsOf(liveCtx.runtime).get('hf-reuse-deep')
  failRun(liveCtx.runtime, run, 'settled')

  const reused = await reuse(liveCtx.runtime, 'hf-reuse-deep', 'continue please')
  assert.equal(reused.tag, 0, reused.tag === 1 ? reused.fields[0] : '')

  const prompts = liveCtx.sessions.calls.filter(([name]) => name === 'SendPromptAsync' || name === 'SendPrompt')
  assert.equal(prompts.length, 2)
  const agents = prompts.map((call) => call[3]?.Agent)
  assert.deepEqual(agents, ['deep-coder', 'deep-coder'])
  liveCtx.cleanup()
})
