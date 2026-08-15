// Split from tests/unit/execution/join-completion.test.mjs (cutover Wave 2a);
// owner: delegation. Join 完成可靠性（EXEC-017/018）：Join deadline 到点 → TimedOut；
// deadline 前 completion 返回 Ok；terminal 立即 claim run（无 Ready gate）。
// HostEventPort sticky/dedupe 断言 → host-boundary。

import assert from 'node:assert/strict'
import { join } from 'node:path'
import test from 'node:test'
import {
  agentCompletion,
  agentIdOf,
  caseOf,
  completionMailbox,
  fableLibraryDir,
  hostEventPort,
  pendingRunLifecycle,
  roles,
  sessionId,
} from '../../verification-system/tests/support/domain.mjs'

/** Fable Dictionary with TryGetValue tuple shape used by HostForkRunLifecycle. */
const loadDictionary = async () => {
  const candidates = ['MutableMap.js', 'System.Collections.Generic.js', 'MapUtil.js']
  for (const file of candidates) {
    try {
      const mod = await import(join(fableLibraryDir, file))
      const Ctor = mod.Dictionary ?? mod.default?.Dictionary
      if (typeof Ctor === 'function') {
        // Fable Dictionary (MutableMap) requires a comparer for hash-based access;
        // mirror production construction (dist/Process/Pty.js).
        const util = await import(join(fableLibraryDir, 'Util.js'))
        return {
          Dictionary: Ctor,
          comparer: { Equals: util.equals, GetHashCode: (x) => (util.safeHash(x) | 0) },
        }
      }
    } catch {
      // try next
    }
  }
  // Fallback: Map-backed shim matching Fable's TryGetValue tuple emission.
  const DictShim = class {
    constructor() {
      this._m = new Map()
    }
    set_Item(k, v) {
      this._m.set(k, v)
    }
    get_Item(k) {
      return this._m.get(k)
    }
    set(k, v) {
      this._m.set(k, v)
    }
    get(k) {
      return this._m.get(k)
    }
    has(k) {
      return this._m.has(k)
    }
    TryGetValue(k) {
      if (this._m.has(k)) return [true, this._m.get(k)]
      return [false, null]
    }
    Remove(k) {
      return this._m.delete(k)
    }
  }
  return { Dictionary: DictShim }
}

// ── Join deadline ────────────────────────────────────────────────────────────

test('EXEC_join_mailbox_with_no_completion_times_out', async () => {
  const box = completionMailbox.create(() => true)
  const started = Date.now()
  const result = await completionMailbox.join(box, 20)
  const elapsed = Date.now() - started

  assert.ok(elapsed >= 10, `expected ~20ms wait, got ${elapsed}ms`)
  assert.ok(elapsed < 2000, `must not hang; got ${elapsed}ms`)
  assert.equal(caseOf(result), 'Error', 'Error result')
  assert.equal(caseOf(result.fields[0]), 'TimedOut')
})

test('EXEC_join_mailbox_completion_before_deadline_returns_ok', async () => {
  const box = completionMailbox.create(() => true)
  // AgentCompletionOutcome is Session.AgentRole, not Kernel.Role.
  const completion = agentCompletion.completedRun({
    runId: 'run-x',
    agentId: 'agent-x',
    agentName: 'fast-coder',
    role: 'Coder',
    workRecord: 'done',
  })

  const pending = completionMailbox.join(box, 100)
  await new Promise((r) => setTimeout(r, 5))
  completionMailbox.publish(box, completion)

  const result = await pending
  assert.equal(caseOf(result), 'Ok', 'Ok completion')
  assert.equal(agentIdOf(result.fields[0]), 'agent-x')
})

// ── Immediate terminal claim (no Ready gate) ────────────────────────────────

test('EXEC_join_complete_claims_run_immediately_without_Ready', async () => {
  const { Dictionary, comparer } = await loadDictionary()
  // Fable Dictionary (MutableMap) requires an iterable AND a comparer for hash access;
  // the Map-backed shim needs neither.
  const pendingRuns = comparer ? new Dictionary([], comparer) : new Dictionary()
  const gate = {}
  const agentId = 'agent-immediate'
  const child = sessionId('ses_immediate_child')
  const parent = sessionId('ses_immediate_parent')
  const source = pendingRunLifecycle.completionSource()
  const run = {
    Token: {},
    AgentId: agentId,
    ChildId: child,
    Role: roles.of('Coder'),
    Source: source,
    Subscription: undefined,
    Finished: false,
  }

  if (typeof pendingRuns.set_Item === 'function') pendingRuns.set_Item(agentId, run)
  else if (typeof pendingRuns.set === 'function') pendingRuns.set(agentId, run)
  else pendingRuns[agentId] = run

  // Normalize TryGetValue / Remove for non-Fable shims and incomplete Fable maps.
  if (typeof pendingRuns.TryGetValue !== 'function') {
    pendingRuns.TryGetValue = (key) => {
      const has =
        typeof pendingRuns.has === 'function'
          ? pendingRuns.has(key)
          : pendingRuns.get?.(key) !== undefined
      if (!has) return [false, null]
      const value =
        typeof pendingRuns.get_Item === 'function'
          ? pendingRuns.get_Item(key)
          : pendingRuns.get(key)
      return [true, value]
    }
  }
  if (typeof pendingRuns.Remove !== 'function') {
    pendingRuns.Remove = (key) => {
      if (typeof pendingRuns.delete === 'function') return pendingRuns.delete(key)
      return false
    }
  }

  const outcome = hostEventPort.failed('terminal-before-prompt')
  pendingRunLifecycle.complete(gate, pendingRuns, undefined, parent, null, run, outcome, undefined)

  assert.equal(run.Finished, true, 'terminal claims run immediately')

  const agentOutcome = await Promise.race([
    source.get_Task(),
    new Promise((_, reject) => setTimeout(() => reject(new Error('Source never completed')), 1000)),
  ])
  assert.equal(caseOf(agentOutcome), 'AgentFailed')

  // markReady is a no-op after immediate claim; must not throw or re-deliver.
  pendingRunLifecycle.markReady(gate, pendingRuns, undefined, parent, null, run, undefined)
})
