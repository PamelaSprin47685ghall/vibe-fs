// Split from tests/unit/session/host-fork-runtime.test.mjs (cutover Wave 2a); owner: delegation.
//
// DELEG-013..015 join/await 调用代数：NothingToJoin/Interrupted/TimedOut/Cancelled/
// Unknown agent 结果分支（有界批次、中断 ≠ 错误）。InstallRun/FailRun/CancelAgent/
// ForkRuntime 面已随 SPLIT@cutover 迁
// requirements/managed-session-lifecycle/tests/host-fork-runtime.test.mjs；permit
// 校验/EXEC-023 迁 requirements/crash-reconciliation/tests/host-fork-runtime-permit.test.mjs。

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { agentJournal, caseOf, sessionId, toList } from '../../verification-system/tests/support/domain.mjs'

const hostRuntimeModule = await import('../../../dist/Execution/Delegation/Fork/Host/Runtime.js')
const { HostForkRuntime, HostForkRuntime__get_IsCancelled: runtimeIsCancelled, HostForkRuntime__Cancel: cancelRuntime } = hostRuntimeModule
const installRun = Object.entries(hostRuntimeModule).find(([k]) => k.startsWith('HostForkRuntime__InstallRun_'))?.[1]
const {
  joinAvailable,
  awaitAgent,
} = await import('../../../dist/Execution/Delegation/Fork/Host/Join.js')
const { Role } = await import('../../../dist/Foundation/Roles.js')

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

// ── Join / JoinAvailable ─────────────────────────────────────────────────────

test('WHAT[DELEG-013] HFRT_join_available_without_work_is_nothing_to_join', async () => {
  const liveCtx = await live()
  const result = await joinAvailable(liveCtx.runtime, 5, new Promise(() => {}))
  assert.equal(result.tag, 1)
  assert.equal(caseOf(result.fields[0]), 'NothingToJoin')
  liveCtx.cleanup()
})

test('WHAT[DELEG-015] HFRT_join_available_with_interrupt_returns_interrupted', async () => {
  const liveCtx = await live()
  installRun(liveCtx.runtime, 'ag8', sessionId('ses_c8'), Role.Coder)

  const result = await joinAvailable(liveCtx.runtime, 5, Promise.resolve('DeadlineExpired'))
  assert.equal(result.tag, 0)
  assert.equal(caseOf(result.fields[0]), 'Interrupted')
  assert.equal(result.fields[0].fields[0], 'DeadlineExpired')
  liveCtx.cleanup()
})

test('WHAT[DELEG-013] HFRT_join_cancelled_runtime_returns_cancelled', async () => {
  const liveCtx = await live()
  cancelRuntime(liveCtx.runtime)
  assert.equal(runtimeIsCancelled(liveCtx.runtime), true)
  const result = await joinAvailable(liveCtx.runtime, 5, new Promise(() => {}))
  assert.equal(result.tag, 1)
  assert.equal(caseOf(result.fields[0]), 'Cancelled')
  liveCtx.cleanup()
})

// ── AwaitAgent ───────────────────────────────────────────────────────────────

test('WHAT[DELEG-013] HFRT_await_agent_unknown_id_is_error', async () => {
  const liveCtx = await live()
  const result = await awaitAgent(liveCtx.runtime, 'ghost', [])
  assert.equal(result.tag, 1)
  assert.equal(result.fields[0], 'Unknown agent id: ghost')
  liveCtx.cleanup()
})
