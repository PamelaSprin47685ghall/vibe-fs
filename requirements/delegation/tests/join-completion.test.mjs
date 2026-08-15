// Split from tests/unit/execution/join-completion.test.mjs (cutover Wave 2a);
// owner: delegation. Terminal 立即 claim run（无 Ready gate）。
// Mailbox single-result Join deadline 语义已随 CLN-06 随 single-result compat 链
// 退役（batch JoinAvailableWithPermit + deadline interrupt 覆盖等价语义）。
// HostEventPort sticky/dedupe 断言 → host-boundary。

import assert from 'node:assert/strict'
import { join } from 'node:path'
import test from 'node:test'
import {
  caseOf,
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
