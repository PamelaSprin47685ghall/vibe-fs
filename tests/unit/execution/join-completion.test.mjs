// Join completion reliability (Part 1): immediate terminal claim, sticky terminal, Join deadline.

import assert from 'node:assert/strict'
import { readdirSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import {
  caseOf,
  completionMailbox,
  hostEventPort,
  pendingRunLifecycle,
  roles,
  sessionId,
} from '../support/domain.mjs'

const BUILD_ROOT = new URL('../../../dist/', import.meta.url).pathname
const fableLibDir = (() => {
  const root = join(BUILD_ROOT, 'fable_modules')
  const name = readdirSync(root).find((e) => e.startsWith('fable-library-js.'))
  if (!name) throw new Error(`no fable-library-js.* under ${root}`)
  return join(root, name)
})()

/** Fable Dictionary with TryGetValue tuple shape used by HostForkRunLifecycle. */
const loadDictionary = async () => {
  const candidates = ['MutableMap.js', 'System.Collections.Generic.js', 'MapUtil.js']
  for (const file of candidates) {
    try {
      const mod = await import(join(fableLibDir, file))
      const Ctor = mod.Dictionary ?? mod.default?.Dictionary
      if (typeof Ctor === 'function') {
        // Fable Dictionary (MutableMap) requires a comparer for hash-based access;
        // mirror production construction (dist/Process/Pty.js).
        const util = await import(join(fableLibDir, 'Util.js'))
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

// ── sticky terminal ──────────────────────────────────────────────────────────

test('EXEC_join_NotifyTerminal_then_late_SubscribeTerminal_replays_sticky', () => {
  const port = hostEventPort.create()
  const child = sessionId('ses_sticky_child')
  const seen = []

  const delivered = hostEventPort.notify(port, child, hostEventPort.failed('early-terminal'))
  assert.equal(delivered, false, 'hasListeners=false when nobody subscribed yet')

  hostEventPort.subscribe(port, (_sid, outcome) => {
    seen.push(caseOf(outcome))
  })

  assert.equal(seen.length, 1, 'late subscriber must receive sticky terminal once')
  assert.equal(seen[0], 'Failed')
})

test('EXEC_join_Failed_outcomes_are_not_provider_run_deduped', () => {
  const port = hostEventPort.create()
  const child = sessionId('ses_dedupe')
  let count = 0
  hostEventPort.subscribe(port, () => {
    count += 1
  })
  hostEventPort.notify(port, child, hostEventPort.failed('a'))
  hostEventPort.notify(port, child, hostEventPort.failed('b'))
  assert.equal(count, 2)
})

// ── Join deadline ────────────────────────────────────────────────────────────

test('EXEC_join_mailbox_with_no_completion_times_out', async () => {
  const box = completionMailbox.create(() => true)
  const started = Date.now()
  const result = await completionMailbox.join(box, 40)
  const elapsed = Date.now() - started

  assert.ok(elapsed >= 25, `expected ~40ms wait, got ${elapsed}ms`)
  assert.ok(elapsed < 2000, `must not hang; got ${elapsed}ms`)
  assert.equal(result.tag, 1, 'Error result')
  assert.equal(caseOf(result.fields[0]), 'TimedOut')
})

test('EXEC_join_mailbox_completion_before_deadline_returns_ok', async () => {
  const box = completionMailbox.create(() => true)
  const completion = {
    RunId: 'run-x',
    AgentId: 'agent-x',
    AgentName: 'fast-coder',
    Role: roles.of('Coder'),
    Outcome: {
      tag: 1,
      fields: [
        {
          AgentId: 'agent-x',
          ChildSessionId: undefined,
          RunId: 'run-x',
          Role: undefined,
          Code: 'OK',
          Message: 'done',
        },
      ],
    },
    CompletedAt: new Date(),
  }

  const pending = completionMailbox.join(box, 500)
  await new Promise((r) => setTimeout(r, 10))
  completionMailbox.publish(box, completion)

  const result = await pending
  assert.equal(result.tag, 0, 'Ok completion')
  assert.equal(result.fields[0].AgentId, 'agent-x')
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
